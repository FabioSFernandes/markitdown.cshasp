using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Drawing;
using MarkItDown.CSharp.Interfaces;
using System.Linq;
using P = DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;
using PlaceholderValues = DocumentFormat.OpenXml.Presentation.PlaceholderValues;

namespace MarkItDown.CSharp.Converters;

public sealed class PptxConverter : DocumentConverter
{
    private static readonly string[] AcceptedMimePrefixes =
    {
        "application/vnd.openxmlformats-officedocument.presentationml",
    };

    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pptx",
    };

    public override bool Accepts(Stream fileStream, StreamInfo streamInfo, ConversionOptions options)
    {
        var extension = streamInfo.Extension ?? string.Empty;
        if (AcceptedExtensions.Contains(extension))
        {
            return true;
        }

        var mimetype = streamInfo.MimeType ?? string.Empty;
        return AcceptedMimePrefixes.Any(prefix => mimetype.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public override Task<DocumentConverterResult> ConvertAsync(
        Stream fileStream,
        StreamInfo streamInfo,
        ConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder();
        using var presentation = PresentationDocument.Open(fileStream, false);
        var presentationPart = presentation.PresentationPart ?? throw new InvalidOperationException("Invalid PPTX.");
        var slideIds = presentationPart.Presentation.SlideIdList?.ChildElements.OfType<SlideId>().ToList()
                       ?? new List<SlideId>();

        var llmClient = options.Get<ILlmClient>("llm_client");
        var llmModel = options.Get<string>("llm_model");
        var llmPrompt = options.Get<string>("llm_prompt") ?? "Write a detailed caption for this image.";
        var keepDataUris = options.Get<bool>("keep_data_uris");

        for (var index = 0; index < slideIds.Count; index++)
        {
            var slideId = slideIds[index];
            var slidePart = (SlidePart)presentationPart.GetPartById(slideId.RelationshipId!);
            builder.AppendLine($"\n<!-- Slide number: {index + 1} -->");
            builder.AppendLine(ProcessSlide(slidePart, llmClient, llmModel, llmPrompt, keepDataUris, cancellationToken).Result);

            if (slidePart.NotesSlidePart?.NotesSlide is { } notesSlide)
            {
                var notes = string.Join(
                    "\n",
                    notesSlide.Descendants<A.Text>()
                        .Select(t => t.Text)
                        .Where(text => !string.IsNullOrWhiteSpace(text)));

                if (!string.IsNullOrWhiteSpace(notes))
                {
                    builder.AppendLine("\n### Notes");
                    builder.AppendLine(notes.Trim());
                }
            }
        }

        return Task.FromResult(new DocumentConverterResult(builder.ToString().Trim()));
    }

    private async Task<string> ProcessSlide(
        SlidePart slidePart,
        ILlmClient? llmClient,
        string? llmModel,
        string llmPrompt,
        bool keepDataUris,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var shapes = slidePart.Slide.Descendants<P.Shape>().ToList();
        var pictures = slidePart.Slide.Descendants<P.Picture>().ToList();
        var graphicFrames = slidePart.Slide.Descendants<P.GraphicFrame>().ToList();

        foreach (var shape in shapes)
        {
            var text = ExtractText(shape.TextBody);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (IsTitle(shape))
            {
                builder.AppendLine($"# {text.Trim()}");
            }
            else
            {
                builder.AppendLine(text.Trim());
            }
        }

        foreach (var frame in graphicFrames)
        {
            var table = frame.Graphic?.GraphicData?.GetFirstChild<DocumentFormat.OpenXml.Drawing.Table>();
            if (table is not null)
            {
                builder.AppendLine(ConvertTable(table));
            }
        }

        foreach (var picture in pictures)
        {
            var description = await DescribePictureAsync(
                picture,
                slidePart,
                llmClient,
                llmModel,
                llmPrompt,
                keepDataUris,
                cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(description))
            {
                builder.AppendLine(description);
            }
        }

        return builder.ToString().Trim();
    }

    private static string ExtractText(OpenXmlCompositeElement? textBody)
    {
        if (textBody is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var paragraph in textBody.Descendants<A.Paragraph>())
        {
            var paragraphText = string.Concat(paragraph.Descendants<A.Text>().Select(t => t.Text));
            builder.AppendLine(paragraphText);
        }

        return builder.ToString().Trim();
    }

    private static bool IsTitle(P.Shape shape)
    {
        var placeholder = shape.NonVisualShapeProperties?
            .ApplicationNonVisualDrawingProperties?
            .GetFirstChild<PlaceholderShape>();

        var type = placeholder?.Type?.Value;
        return type == PlaceholderValues.Title || type == PlaceholderValues.CenteredTitle;
    }

    private static string ConvertTable(DocumentFormat.OpenXml.Drawing.Table table)
    {
        var rows = table.Elements<A.TableRow>()
            .Select(r => r.Elements<A.TableCell>().Select(c => ExtractCellText(c.TextBody)).ToList())
            .ToList();
        if (rows.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var header = rows[0];
        builder.AppendLine("| " + string.Join(" | ", header) + " |");
        builder.AppendLine("| " + string.Join(" | ", header.Select(_ => "---")) + " |");

        foreach (var row in rows.Skip(1))
        {
            builder.AppendLine("| " + string.Join(" | ", row) + " |");
        }

        return builder.ToString();
    }

    private static string ExtractCellText(A.TextBody? textBody)
    {
        if (textBody is null)
        {
            return string.Empty;
        }

        var lines = textBody.Descendants<DocumentFormat.OpenXml.Drawing.Text>().Select(t => t.Text);
        return string.Join(" ", lines).Trim();
    }

    private static async Task<string?> DescribePictureAsync(
        P.Picture picture,
        SlidePart slidePart,
        ILlmClient? llmClient,
        string? model,
        string prompt,
        bool keepDataUris,
        CancellationToken cancellationToken)
    {
        var blip = picture.BlipFill?.Blip;
        if (blip?.Embed?.Value is null)
        {
            return null;
        }

        var imagePart = (ImagePart)slidePart.GetPartById(blip.Embed.Value);
        await using var imageStream = imagePart.GetStream();
        using var memory = new MemoryStream();
        await imageStream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
        memory.Seek(0, SeekOrigin.Begin);

        var altText = picture.NonVisualPictureProperties?
            .NonVisualDrawingProperties?
            .Description ?? picture.NonVisualPictureProperties?.NonVisualDrawingProperties?.Name ?? "Image";

        string? llmDescription = null;
        if (llmClient is not null && !string.IsNullOrWhiteSpace(model))
        {
            memory.Seek(0, SeekOrigin.Begin);
            var request = new LlmCaptionRequest(memory, new StreamInfo { MimeType = imagePart.ContentType }, model!, prompt);
            llmDescription = await llmClient.CreateCaptionAsync(request, cancellationToken).ConfigureAwait(false);
        }

        string imageReference;
        memory.Seek(0, SeekOrigin.Begin);
        if (keepDataUris)
        {
            var base64 = Convert.ToBase64String(memory.ToArray());
            imageReference = $"data:{imagePart.ContentType};base64,{base64}";
        }
        else
        {
            imageReference = picture.NonVisualPictureProperties?.NonVisualDrawingProperties?.Name ?? "image.jpg";
        }

        var caption = llmDescription ?? altText;
        return $"![{caption}]({imageReference})";
    }
}

