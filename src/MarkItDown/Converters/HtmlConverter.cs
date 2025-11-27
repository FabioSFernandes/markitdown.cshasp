using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace MarkItDown.Converters;

/// <summary>
/// Converter for HTML files to Markdown.
/// </summary>
public sealed partial class HtmlConverter : DocumentConverterBase
{
    /// <inheritdoc />
    protected override IReadOnlyCollection<string> SupportedExtensions { get; } =
        new[] { ".html", ".htm", ".xhtml" };

    /// <inheritdoc />
    protected override IReadOnlyCollection<string> SupportedMimeTypes { get; } =
        new[] { "text/html", "application/xhtml+xml" };

    /// <inheritdoc />
    public override ConversionResult Convert(Stream stream, string? fileName = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var html = ReadAllText(stream);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var title = ExtractTitle(doc);
        var markdown = ConvertNodeToMarkdown(doc.DocumentNode);
        
        // Clean up the markdown output
        markdown = CleanupMarkdown(markdown);

        return new ConversionResult(markdown, title, "text/html");
    }

    private static string? ExtractTitle(HtmlDocument doc)
    {
        var titleNode = doc.DocumentNode.SelectSingleNode("//title");
        return titleNode?.InnerText?.Trim();
    }

    private static string ConvertNodeToMarkdown(HtmlNode node)
    {
        var sb = new StringBuilder();
        ConvertNode(node, sb, new ConversionContext());
        return sb.ToString();
    }

    private static void ConvertNode(HtmlNode node, StringBuilder sb, ConversionContext context)
    {
        switch (node.NodeType)
        {
            case HtmlNodeType.Text:
                var text = WebUtility.HtmlDecode(node.InnerText);
                if (!string.IsNullOrWhiteSpace(text) || context.PreserveWhitespace)
                {
                    sb.Append(context.PreserveWhitespace ? text : NormalizeWhitespace(text));
                }
                break;

            case HtmlNodeType.Element:
                ConvertElement(node, sb, context);
                break;

            case HtmlNodeType.Document:
                foreach (var child in node.ChildNodes)
                {
                    ConvertNode(child, sb, context);
                }
                break;
        }
    }

    private static void ConvertElement(HtmlNode node, StringBuilder sb, ConversionContext context)
    {
        var tagName = node.Name.ToLowerInvariant();

        switch (tagName)
        {
            case "h1":
            case "h2":
            case "h3":
            case "h4":
            case "h5":
            case "h6":
                var level = int.Parse(tagName.Substring(1));
                sb.AppendLine();
                sb.Append(new string('#', level) + " ");
                ConvertChildren(node, sb, context);
                sb.AppendLine();
                break;

            case "p":
                sb.AppendLine();
                ConvertChildren(node, sb, context);
                sb.AppendLine();
                break;

            case "br":
                sb.AppendLine();
                break;

            case "strong":
            case "b":
                sb.Append("**");
                ConvertChildren(node, sb, context);
                sb.Append("**");
                break;

            case "em":
            case "i":
                sb.Append("*");
                ConvertChildren(node, sb, context);
                sb.Append("*");
                break;

            case "code":
                if (!context.InPreBlock)
                {
                    sb.Append('`');
                    ConvertChildren(node, sb, context);
                    sb.Append('`');
                }
                else
                {
                    ConvertChildren(node, sb, context);
                }
                break;

            case "pre":
                sb.AppendLine();
                sb.AppendLine("```");
                ConvertChildren(node, sb, context with { PreserveWhitespace = true, InPreBlock = true });
                sb.AppendLine();
                sb.AppendLine("```");
                break;

            case "a":
                var href = node.GetAttributeValue("href", "");
                var linkText = GetInnerText(node);
                if (!string.IsNullOrEmpty(href))
                {
                    sb.Append($"[{linkText}]({href})");
                }
                else
                {
                    sb.Append(linkText);
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
                ConvertListItems(node, sb, context, false);
                break;

            case "ol":
                sb.AppendLine();
                ConvertListItems(node, sb, context, true);
                break;

            case "li":
                ConvertChildren(node, sb, context);
                break;

            case "blockquote":
                sb.AppendLine();
                var quoteContent = new StringBuilder();
                ConvertChildren(node, quoteContent, context);
                var quoteLines = quoteContent.ToString().Trim().Split('\n');
                foreach (var line in quoteLines)
                {
                    sb.AppendLine($"> {line.Trim()}");
                }
                break;

            case "hr":
                sb.AppendLine();
                sb.AppendLine("---");
                break;

            case "table":
                sb.AppendLine();
                ConvertTable(node, sb);
                break;

            case "script":
            case "style":
            case "head":
            case "meta":
            case "link":
                // Skip these elements
                break;

            default:
                ConvertChildren(node, sb, context);
                break;
        }
    }

    private static void ConvertChildren(HtmlNode node, StringBuilder sb, ConversionContext context)
    {
        foreach (var child in node.ChildNodes)
        {
            ConvertNode(child, sb, context);
        }
    }

    private static void ConvertListItems(HtmlNode node, StringBuilder sb, ConversionContext context, bool ordered)
    {
        var counter = 1;
        foreach (var child in node.ChildNodes)
        {
            if (child.Name.Equals("li", StringComparison.OrdinalIgnoreCase))
            {
                var prefix = ordered ? $"{counter}. " : "- ";
                sb.Append(new string(' ', context.ListDepth * 2));
                sb.Append(prefix);
                
                var itemSb = new StringBuilder();
                ConvertChildren(child, itemSb, context with { ListDepth = context.ListDepth + 1 });
                sb.AppendLine(itemSb.ToString().Trim());
                
                counter++;
            }
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
            var cells = row.SelectNodes(".//th | .//td");
            if (cells == null) continue;

            sb.Append("| ");
            sb.Append(string.Join(" | ", cells.Select(c => GetInnerText(c).Replace("|", "\\|"))));
            sb.AppendLine(" |");

            if (isFirstRow)
            {
                sb.Append("| ");
                sb.Append(string.Join(" | ", cells.Select(_ => "---")));
                sb.AppendLine(" |");
                isFirstRow = false;
            }
        }
        sb.AppendLine();
    }

    private static string GetInnerText(HtmlNode node)
    {
        return WebUtility.HtmlDecode(node.InnerText).Trim();
    }

    private static string NormalizeWhitespace(string text)
    {
        return MultipleWhitespaceRegex().Replace(text, " ");
    }

    private static string CleanupMarkdown(string markdown)
    {
        // Remove excessive blank lines (more than 2 consecutive)
        markdown = ExcessiveNewlinesRegex().Replace(markdown, "\n\n");
        
        // Trim the result
        return markdown.Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleWhitespaceRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessiveNewlinesRegex();

    private record ConversionContext(
        bool PreserveWhitespace = false,
        bool InPreBlock = false,
        int ListDepth = 0);
}
