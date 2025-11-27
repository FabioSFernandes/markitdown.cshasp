using System.Text;
using HtmlAgilityPack;
using System.Linq;
using MarkItDown.CSharp.Converters.Markdown;

namespace MarkItDown.CSharp.Converters;

public sealed class WikipediaConverter : DocumentConverter
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

    private readonly CustomMarkdownConverter _markdownConverter = new();

    public override bool Accepts(Stream fileStream, StreamInfo streamInfo, ConversionOptions options)
    {
        var url = streamInfo.Url ?? string.Empty;
        if (!IsWikipediaUrl(url))
        {
            return false;
        }

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

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var body = doc.DocumentNode.SelectSingleNode("//*[@id='mw-content-text']") ?? doc.DocumentNode;
        var titleNode = doc.DocumentNode.SelectSingleNode("//*[@class='mw-page-title-main']") ??
                        doc.DocumentNode.SelectSingleNode("//title");
        var title = titleNode?.InnerText?.Trim();

        var markdown = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(title))
        {
            markdown.AppendLine($"# {title}");
            markdown.AppendLine();
        }

        markdown.AppendLine(_markdownConverter.Convert(body));

        return new DocumentConverterResult(markdown.ToString().Trim(), title);
    }

    private static bool IsWikipediaUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        return parsed.Host.EndsWith(".wikipedia.org", StringComparison.OrdinalIgnoreCase);
    }
}

