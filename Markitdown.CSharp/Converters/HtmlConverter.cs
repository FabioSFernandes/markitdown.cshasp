using System.Text;
using HtmlAgilityPack;
using MarkItDown.CSharp.Converters.Markdown;

namespace MarkItDown.CSharp.Converters;

public sealed class HtmlConverter : DocumentConverter
{
    private static readonly string[] AcceptedMimePrefixes =
    {
        "text/html",
        "application/xhtml",
    };

    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".html",
        ".htm",
    };

    private readonly CustomMarkdownConverter _markdownConverter;

    public HtmlConverter(CustomMarkdownOptions? options = null)
    {
        _markdownConverter = new CustomMarkdownConverter(options);
    }

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
        var encodingName = streamInfo.Charset ?? "utf-8";
        var encoding = Encoding.GetEncoding(encodingName);

        using var reader = new StreamReader(fileStream, encoding, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var html = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        return ConvertFromString(html);
    }

    public DocumentConverterResult ConvertFromString(string html, string? url = null)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var body = document.DocumentNode.SelectSingleNode("//body") ?? document.DocumentNode;
        var markdown = _markdownConverter.Convert(body);
        var title = document.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();

        return new DocumentConverterResult(markdown, title);
    }
}

