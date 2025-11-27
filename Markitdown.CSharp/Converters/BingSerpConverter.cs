using System.Text;
using HtmlAgilityPack;
using Microsoft.AspNetCore.WebUtilities;
using System.Linq;
using MarkItDown.CSharp.Converters.Markdown;

namespace MarkItDown.CSharp.Converters;

public sealed class BingSerpConverter : DocumentConverter
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
        if (!url.StartsWith("https://www.bing.com/search?q=", StringComparison.OrdinalIgnoreCase))
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

        var query = ExtractQuery(streamInfo.Url ?? string.Empty);
        var builder = new StringBuilder();
        builder.AppendLine($"## A Bing search for '{query}' found the following results:");
        builder.AppendLine();

        var results = doc.DocumentNode.SelectNodes("//*[@class='b_algo']") ?? new HtmlNodeCollection(null);
        foreach (var result in results)
        {
            RewriteRedirectLinks(result);
            var markdown = _markdownConverter.Convert(result);
            var normalized = string.Join("\n", markdown.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0));

            if (!string.IsNullOrWhiteSpace(normalized))
            {
                builder.AppendLine(normalized);
                builder.AppendLine();
            }
        }

        var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();

        return new DocumentConverterResult(builder.ToString().Trim(), title);
    }

    private static void RewriteRedirectLinks(HtmlNode resultNode)
    {
        foreach (var link in resultNode.Descendants("a").Where(a => a.Attributes["href"] != null))
        {
            var href = link.GetAttributeValue("href", string.Empty);
            if (!Uri.TryCreate(href, UriKind.Absolute, out var parsed))
            {
                continue;
            }

            var query = QueryHelpers.ParseQuery(parsed.Query);
            var redirect = query.TryGetValue("u", out var values) ? values.FirstOrDefault() : null;
            if (string.IsNullOrWhiteSpace(redirect))
            {
                continue;
            }

            try
            {
                // Trim the leading '1' and decode Base64 (Bing prefix)
                var trimmed = redirect.Length > 2 ? redirect[2..] : redirect;
                var bytes = Convert.FromBase64String(PadBase64(trimmed));
                var resolved = Encoding.UTF8.GetString(bytes);
                link.SetAttributeValue("href", resolved);
            }
            catch
            {
                // Ignore decoding errors
            }
        }
    }

    private static string PadBase64(string input)
    {
        return input.PadRight(input.Length + (4 - input.Length % 4) % 4, '=');
    }

    private static string ExtractQuery(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            return string.Empty;
        }

        var query = QueryHelpers.ParseQuery(parsed.Query);
        return query.TryGetValue("q", out var values) ? values.FirstOrDefault() ?? string.Empty : string.Empty;
    }
}

