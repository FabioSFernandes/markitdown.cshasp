using UglyToad.PdfPig;
using System.Linq;

namespace MarkItDown.CSharp.Converters;

public sealed class PdfConverter : DocumentConverter
{
    private static readonly string[] AcceptedMimePrefixes =
    {
        "application/pdf",
        "application/x-pdf",
    };

    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
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
        using var pdf = PdfDocument.Open(fileStream, new ParsingOptions { UseLenientParsing = true });
        var builder = new System.Text.StringBuilder();

        foreach (var page in pdf.GetPages())
        {
            builder.AppendLine(page.Text);
        }

        return Task.FromResult(new DocumentConverterResult(builder.ToString().Trim()));
    }
}

