using System.Text;
using MarkItDown.CSharp.Converters.Utilities;
using MarkItDown.CSharp.Interfaces;
using System.Linq;

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

