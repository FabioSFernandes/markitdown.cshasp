using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;

namespace MarkItDown.Converters;

/// <summary>
/// Converter for HTML files to Markdown.
/// </summary>
public sealed partial class HtmlConverter : IDocumentConverter
{
    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/html",
        "application/xhtml+xml"
    };

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".html",
        ".htm",
        ".xhtml"
    };

    /// <inheritdoc />
    public bool Accepts(Stream stream, StreamInfo streamInfo)
    {
        if (streamInfo.MimeType is not null && SupportedMimeTypes.Contains(streamInfo.MimeType))
        {
            return true;
        }

        if (streamInfo.Extension is not null && SupportedExtensions.Contains(streamInfo.Extension))
        {
            return true;
        }

        // Try to detect HTML content by examining the stream
        if (streamInfo.MimeType is null && streamInfo.Extension is null)
        {
            return LooksLikeHtml(stream);
        }

        return false;
    }

    private static bool LooksLikeHtml(Stream stream)
    {
        var position = stream.Position;
        try
        {
            using var reader = new StreamReader(stream, leaveOpen: true);
            var buffer = new char[1024];
            var read = reader.Read(buffer, 0, buffer.Length);
            if (read == 0) return false;

            var content = new string(buffer, 0, read).TrimStart();

            // Check for common HTML indicators
            return content.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                   content.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
                   content.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) ||
                   (content.Contains('<') && (
                       content.Contains("<head", StringComparison.OrdinalIgnoreCase) ||
                       content.Contains("<body", StringComparison.OrdinalIgnoreCase) ||
                       content.Contains("<div", StringComparison.OrdinalIgnoreCase) ||
                       content.Contains("<p>", StringComparison.OrdinalIgnoreCase) ||
                       content.Contains("<h1", StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            stream.Position = position;
        }
    }

    /// <inheritdoc />
    public DocumentConverterResult Convert(Stream stream, StreamInfo streamInfo)
    {
        var doc = new HtmlDocument();
        doc.Load(stream, detectEncodingFromByteOrderMarks: true);

        var title = ExtractTitle(doc);
        var markdown = ConvertToMarkdown(doc);

        return new DocumentConverterResult
        {
            Title = title,
            TextContent = markdown
        };
    }

    private static string? ExtractTitle(HtmlDocument doc)
    {
        var titleNode = doc.DocumentNode.SelectSingleNode("//title");
        return titleNode?.InnerText?.Trim();
    }

    private static string ConvertToMarkdown(HtmlDocument doc)
    {
        var body = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
        var sb = new StringBuilder();
        ConvertNode(body, sb, 0);
        return NormalizeWhitespace(sb.ToString());
    }

    private static void ConvertNode(HtmlNode node, StringBuilder sb, int listDepth)
    {
        switch (node.NodeType)
        {
            case HtmlNodeType.Text:
                var text = HttpUtility.HtmlDecode(node.InnerText);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    // Preserve leading/trailing space if it exists in the original
                    var hasLeadingSpace = node.InnerText.Length > 0 && char.IsWhiteSpace(node.InnerText[0]);
                    var hasTrailingSpace = node.InnerText.Length > 0 && char.IsWhiteSpace(node.InnerText[^1]);
                    var trimmed = text.Trim();

                    if (hasLeadingSpace && sb.Length > 0 && sb[^1] != ' ' && sb[^1] != '\n')
                    {
                        sb.Append(' ');
                    }
                    sb.Append(trimmed);
                    if (hasTrailingSpace)
                    {
                        sb.Append(' ');
                    }
                }
                break;

            case HtmlNodeType.Element:
                ConvertElement(node, sb, listDepth);
                break;

            default:
                foreach (var child in node.ChildNodes)
                {
                    ConvertNode(child, sb, listDepth);
                }
                break;
        }
    }

    private static void ConvertElement(HtmlNode node, StringBuilder sb, int listDepth)
    {
        var tagName = node.Name.ToLowerInvariant();

        switch (tagName)
        {
            case "h1":
                sb.AppendLine();
                sb.Append("# ");
                ConvertChildren(node, sb, listDepth);
                sb.AppendLine();
                sb.AppendLine();
                break;

            case "h2":
                sb.AppendLine();
                sb.Append("## ");
                ConvertChildren(node, sb, listDepth);
                sb.AppendLine();
                sb.AppendLine();
                break;

            case "h3":
                sb.AppendLine();
                sb.Append("### ");
                ConvertChildren(node, sb, listDepth);
                sb.AppendLine();
                sb.AppendLine();
                break;

            case "h4":
                sb.AppendLine();
                sb.Append("#### ");
                ConvertChildren(node, sb, listDepth);
                sb.AppendLine();
                sb.AppendLine();
                break;

            case "h5":
                sb.AppendLine();
                sb.Append("##### ");
                ConvertChildren(node, sb, listDepth);
                sb.AppendLine();
                sb.AppendLine();
                break;

            case "h6":
                sb.AppendLine();
                sb.Append("###### ");
                ConvertChildren(node, sb, listDepth);
                sb.AppendLine();
                sb.AppendLine();
                break;

            case "p":
                sb.AppendLine();
                ConvertChildren(node, sb, listDepth);
                sb.AppendLine();
                sb.AppendLine();
                break;

            case "br":
                sb.AppendLine();
                break;

            case "hr":
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                break;

            case "strong":
            case "b":
                sb.Append("**");
                ConvertChildren(node, sb, listDepth);
                sb.Append("**");
                break;

            case "em":
            case "i":
                sb.Append('*');
                ConvertChildren(node, sb, listDepth);
                sb.Append('*');
                break;

            case "u":
                sb.Append("<u>");
                ConvertChildren(node, sb, listDepth);
                sb.Append("</u>");
                break;

            case "s":
            case "strike":
            case "del":
                sb.Append("~~");
                ConvertChildren(node, sb, listDepth);
                sb.Append("~~");
                break;

            case "code":
                if (node.ParentNode?.Name.ToLowerInvariant() != "pre")
                {
                    sb.Append('`');
                    sb.Append(HttpUtility.HtmlDecode(node.InnerText));
                    sb.Append('`');
                }
                else
                {
                    sb.Append(HttpUtility.HtmlDecode(node.InnerText));
                }
                break;

            case "pre":
                sb.AppendLine();
                sb.AppendLine("```");
                ConvertChildren(node, sb, listDepth);
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine();
                break;

            case "blockquote":
                sb.AppendLine();
                var quoteContent = new StringBuilder();
                ConvertChildren(node, quoteContent, listDepth);
                var lines = quoteContent.ToString().Split('\n');
                foreach (var line in lines)
                {
                    sb.Append("> ");
                    sb.AppendLine(line);
                }
                sb.AppendLine();
                break;

            case "a":
                var href = node.GetAttributeValue("href", "");
                var linkText = new StringBuilder();
                ConvertChildren(node, linkText, listDepth);
                var linkTextStr = linkText.ToString().Trim();
                if (!string.IsNullOrEmpty(href))
                {
                    sb.Append($"[{linkTextStr}]({href})");
                }
                else
                {
                    sb.Append(linkTextStr);
                }
                break;

            case "img":
                var src = node.GetAttributeValue("src", "");
                var alt = node.GetAttributeValue("alt", "");
                if (!string.IsNullOrEmpty(src))
                {
                    sb.Append($"![{alt}]({src})");
                }
                break;

            case "ul":
                sb.AppendLine();
                foreach (var li in node.ChildNodes.Where(n => n.Name.ToLowerInvariant() == "li"))
                {
                    var indent = new string(' ', listDepth * 2);
                    sb.Append(indent);
                    sb.Append("- ");
                    ConvertChildren(li, sb, listDepth + 1);
                    sb.AppendLine();
                }
                sb.AppendLine();
                break;

            case "ol":
                sb.AppendLine();
                var index = 1;
                foreach (var li in node.ChildNodes.Where(n => n.Name.ToLowerInvariant() == "li"))
                {
                    var indent = new string(' ', listDepth * 2);
                    sb.Append(indent);
                    sb.Append($"{index}. ");
                    ConvertChildren(li, sb, listDepth + 1);
                    sb.AppendLine();
                    index++;
                }
                sb.AppendLine();
                break;

            case "table":
                sb.AppendLine();
                ConvertTable(node, sb);
                sb.AppendLine();
                break;

            case "script":
            case "style":
            case "noscript":
            case "nav":
            case "footer":
            case "header":
                // Skip these elements
                break;

            case "div":
            case "span":
            case "section":
            case "article":
            case "main":
            case "aside":
            default:
                ConvertChildren(node, sb, listDepth);
                break;
        }
    }

    private static void ConvertChildren(HtmlNode node, StringBuilder sb, int listDepth)
    {
        foreach (var child in node.ChildNodes)
        {
            ConvertNode(child, sb, listDepth);
        }
    }

    private static void ConvertTable(HtmlNode tableNode, StringBuilder sb)
    {
        var rows = tableNode.SelectNodes(".//tr");
        if (rows == null || rows.Count == 0)
            return;

        var isFirstRow = true;
        foreach (var row in rows)
        {
            var cells = row.SelectNodes("th|td");
            if (cells == null || cells.Count == 0)
                continue;

            sb.Append("| ");
            foreach (var cell in cells)
            {
                var cellContent = new StringBuilder();
                ConvertChildren(cell, cellContent, 0);
                sb.Append(cellContent.ToString().Trim().Replace("|", "\\|"));
                sb.Append(" | ");
            }
            sb.AppendLine();

            if (isFirstRow)
            {
                sb.Append("| ");
                foreach (var _ in cells)
                {
                    sb.Append("--- | ");
                }
                sb.AppendLine();
                isFirstRow = false;
            }
        }
    }

    private static string NormalizeWhitespace(string text)
    {
        // Replace multiple newlines with double newlines
        text = MultipleNewlinesRegex().Replace(text, "\n\n");

        // Trim each line
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].TrimEnd();
        }

        return string.Join("\n", lines).Trim();
    }

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleNewlinesRegex();
}
