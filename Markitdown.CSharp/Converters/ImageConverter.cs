using System.Text;
using MarkItDown.CSharp.Converters.Utilities;
using MarkItDown.CSharp.Interfaces;
using System.Linq;
using Tesseract;

namespace MarkItDown.CSharp.Converters;

public sealed class ImageConverter : DocumentConverter
{
    private static readonly string[] AcceptedMimePrefixes =
    {
        "image/jpeg",
        "image/png",
    };

    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
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

    public override async Task<DocumentConverterResult> ConvertAsync(
        Stream fileStream,
        StreamInfo streamInfo,
        ConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder();

        if (fileStream.CanSeek)
        {
            fileStream.Seek(0, SeekOrigin.Begin);
        }

        var tessDataPath = options.Get<string>("tesseract_tessdata_path");
        var tessLang = options.Get<string>("tesseract_lang") ?? "eng";
        var rawExt = streamInfo.Extension ?? ".png";
        if (rawExt.Length > 0 && rawExt[0] != '.')
        {
            rawExt = "." + rawExt;
        }
        var imageExtension = AcceptedExtensions.Contains(rawExt) ? rawExt : ".png";
        var ocrText = await RunOcrIfAvailableAsync(fileStream, tessDataPath, tessLang, imageExtension, cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(ocrText))
        {
            builder.AppendLine("### Text in image (OCR)");
            builder.AppendLine(ocrText.Trim());
            builder.AppendLine();
        }

        if (fileStream.CanSeek)
        {
            fileStream.Seek(0, SeekOrigin.Begin);
        }

        var exiftoolPath = options.Get<string>("exiftool_path");
        var metadata = await ExifToolUtility.ReadMetadataAsync(fileStream, exiftoolPath, cancellationToken)
            .ConfigureAwait(false);

        var interestingFields = new[]
        {
            "ImageSize",
            "Title",
            "Caption",
            "Description",
            "Keywords",
            "Artist",
            "Author",
            "DateTimeOriginal",
            "CreateDate",
            "GPSPosition",
        };

        foreach (var field in interestingFields)
        {
            if (metadata.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                builder.AppendLine($"{field}: {value}");
            }
        }

        var llmClient = options.Get<ILlmClient>("llm_client");
        var llmModel = options.Get<string>("llm_model");
        var llmPrompt = options.Get<string>("llm_prompt") ?? "Write a detailed caption for this image.";

        if (llmClient is not null && !string.IsNullOrWhiteSpace(llmModel))
        {
            var caption = await CreateCaptionAsync(llmClient, llmModel!, llmPrompt!, fileStream, streamInfo, cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(caption))
            {
                builder.AppendLine();
                builder.AppendLine("### Description");
                builder.AppendLine(caption.Trim());
            }
        }

        return new DocumentConverterResult(builder.ToString().Trim());
    }

    private static async Task<string?> RunOcrIfAvailableAsync(
        Stream imageStream,
        string? tessDataPath,
        string lang,
        string fileExtension,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tessDataPath) || !Directory.Exists(tessDataPath))
            return null;

        var tessDataFolder = Path.Combine(tessDataPath.Trim(), "tessdata");
        if (!Directory.Exists(tessDataFolder))
            return null;

        string? tempPath = null;
        try
        {
            if (!imageStream.CanSeek)
            {
                return null;
            }

            imageStream.Seek(0, SeekOrigin.Begin);
            tempPath = Path.Combine(Path.GetTempPath(), $"markitdown_ocr_{Guid.NewGuid():N}{fileExtension}");
            await using (var file = File.Create(tempPath))
            {
                await imageStream.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            }

            return await Task.Run(() =>
            {
                try
                {
                    // TesseractEngine expects the path to the tessdata folder (containing .traineddata files), not its parent.
                    using var engine = new TesseractEngine(tessDataFolder, lang, EngineMode.Default);
                    using var img = Pix.LoadFromFile(tempPath);
                    using var page = engine.Process(img);
                    return page.GetText()?.Trim();
                }
                catch
                {
                    return null;
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (tempPath != null && File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* ignore */ }
            }
        }
    }

    private static async Task<string?> CreateCaptionAsync(
        ILlmClient llmClient,
        string model,
        string prompt,
        Stream stream,
        StreamInfo info,
        CancellationToken cancellationToken)
    {
        var position = stream.Position;
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
        stream.Seek(position, SeekOrigin.Begin);
        copy.Seek(0, SeekOrigin.Begin);

        var request = new LlmCaptionRequest(copy, info, model, prompt);
        return await llmClient.CreateCaptionAsync(request, cancellationToken).ConfigureAwait(false);
    }
}

