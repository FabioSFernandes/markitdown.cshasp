using System.Text;
using System.Xml;
using HtmlAgilityPack;
using System.Linq;
using MarkItDown.CSharp.Converters.Markdown;

namespace MarkItDown.CSharp.Converters;

public sealed class RssConverter : DocumentConverter
{
    private static readonly string[] PreciseMimePrefixes =
    {
        "application/rss",
        "application/rss+xml",
        "application/atom",
        "application/atom+xml",
    };

    private static readonly string[] CandidateMimePrefixes =
    {
        "text/xml",
        "application/xml",
    };

    private static readonly HashSet<string> PreciseExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".rss",
        ".atom",
    };

    private static readonly HashSet<string> CandidateExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xml",
    };

    private readonly CustomMarkdownConverter _markdownConverter = new();

    public override bool Accepts(Stream fileStream, StreamInfo streamInfo, ConversionOptions options)
    {
        var extension = streamInfo.Extension ?? string.Empty;
        if (PreciseExtensions.Contains(extension))
        {
            return true;
        }

        var mimetype = streamInfo.MimeType ?? string.Empty;
        if (PreciseMimePrefixes.Any(prefix => mimetype.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (CandidateExtensions.Contains(extension) ||
            CandidateMimePrefixes.Any(prefix => mimetype.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return CheckXml(fileStream);
        }

        return false;
    }

    public override async Task<DocumentConverterResult> ConvertAsync(
        Stream fileStream,
        StreamInfo streamInfo,
        ConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        var xmlDoc = new XmlDocument();
        fileStream.Seek(0, SeekOrigin.Begin);
        xmlDoc.Load(fileStream);

        var feedType = GetFeedType(xmlDoc);

        return feedType switch
        {
            FeedType.Rss => ParseRss(xmlDoc),
            FeedType.Atom => ParseAtom(xmlDoc),
            _ => throw new InvalidOperationException("Unknown feed type."),
        };
    }

    private static bool CheckXml(Stream fileStream)
    {
        var position = fileStream.Position;
        try
        {
            var doc = new XmlDocument();
            fileStream.Seek(0, SeekOrigin.Begin);
            doc.Load(fileStream);
            return GetFeedType(doc) != FeedType.Unknown;
        }
        catch
        {
            return false;
        }
        finally
        {
            fileStream.Seek(position, SeekOrigin.Begin);
        }
    }

    private static FeedType GetFeedType(XmlDocument doc)
    {
        if (doc.GetElementsByTagName("rss").Count > 0)
        {
            return FeedType.Rss;
        }

        var feeds = doc.GetElementsByTagName("feed");
        if (feeds.Count > 0)
        {
            var feed = feeds[0] as XmlElement;
            if (feed?.GetElementsByTagName("entry").Count > 0)
            {
                return FeedType.Atom;
            }
        }

        return FeedType.Unknown;
    }

    private DocumentConverterResult ParseAtom(XmlDocument doc)
    {
        var feed = (XmlElement)doc.GetElementsByTagName("feed")[0];
        var title = GetText(feed, "title");
        var subtitle = GetText(feed, "subtitle");
        var entries = feed.GetElementsByTagName("entry");

        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(title))
        {
            builder.AppendLine($"# {title}");
        }

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            builder.AppendLine(subtitle);
        }

        foreach (XmlElement entry in entries)
        {
            var entryTitle = GetText(entry, "title");
            var summary = GetText(entry, "summary");
            var updated = GetText(entry, "updated");
            var content = GetText(entry, "content");

            if (!string.IsNullOrWhiteSpace(entryTitle))
            {
                builder.AppendLine($"\n## {entryTitle}");
            }

            if (!string.IsNullOrWhiteSpace(updated))
            {
                builder.AppendLine($"Updated on: {updated}");
            }

            if (!string.IsNullOrWhiteSpace(summary))
            {
                builder.AppendLine(ParseContent(summary));
            }

            if (!string.IsNullOrWhiteSpace(content))
            {
                builder.AppendLine(ParseContent(content));
            }
        }

        return new DocumentConverterResult(builder.ToString().Trim(), title);
    }

    private DocumentConverterResult ParseRss(XmlDocument doc)
    {
        var rss = (XmlElement)doc.GetElementsByTagName("rss")[0];
        var channels = rss.GetElementsByTagName("channel");
        if (channels.Count == 0)
        {
            throw new InvalidOperationException("RSS feed does not contain a channel element.");
        }

        var channel = (XmlElement)channels[0];
        var title = GetText(channel, "title");
        var description = GetText(channel, "description");

        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(title))
        {
            builder.AppendLine($"# {title}");
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            builder.AppendLine(description);
        }

        foreach (XmlElement item in channel.GetElementsByTagName("item"))
        {
            var itemTitle = GetText(item, "title");
            var summary = GetText(item, "description");
            var pubDate = GetText(item, "pubDate");
            var content = GetText(item, "content:encoded");

            if (!string.IsNullOrWhiteSpace(itemTitle))
            {
                builder.AppendLine($"\n## {itemTitle}");
            }

            if (!string.IsNullOrWhiteSpace(pubDate))
            {
                builder.AppendLine($"Published on: {pubDate}");
            }

            if (!string.IsNullOrWhiteSpace(summary))
            {
                builder.AppendLine(ParseContent(summary));
            }

            if (!string.IsNullOrWhiteSpace(content))
            {
                builder.AppendLine(ParseContent(content));
            }
        }

        return new DocumentConverterResult(builder.ToString().Trim(), title);
    }

    private string ParseContent(string content)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(content);
            return _markdownConverter.Convert(doc.DocumentNode);
        }
        catch
        {
            return content;
        }
    }

    private static string? GetText(XmlElement parent, string tagName)
    {
        var nodes = parent.GetElementsByTagName(tagName);
        if (nodes.Count == 0)
        {
            return null;
        }

        var first = nodes[0];
        var text = first.InnerText;
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private enum FeedType
    {
        Unknown,
        Rss,
        Atom,
    }
}

