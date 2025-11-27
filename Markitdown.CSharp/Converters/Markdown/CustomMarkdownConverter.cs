using System;
using System.Text;
using HtmlAgilityPack;
using System.Linq;
using ReverseMarkdown;

namespace MarkItDown.CSharp.Converters.Markdown;

public sealed class CustomMarkdownConverter
{
    private readonly CustomMarkdownOptions _options;
    private readonly Converter _converter;

    public CustomMarkdownConverter(CustomMarkdownOptions? options = null)
    {
        _options = options ?? new CustomMarkdownOptions();

        var config = new Config
        {
            GithubFlavored = true,
            UnknownTags = Config.UnknownTagsOption.Bypass,
            RemoveComments = true,
            SmartHrefHandling = true,
        };

        _converter = new Converter(config);
    }

    public string Convert(HtmlNode node)
    {
        var document = new HtmlDocument();
        document.LoadHtml(node.OuterHtml);
        Preprocess(document);
        var markdown = _converter.Convert(document.DocumentNode.OuterHtml);
        markdown = EnsureHeadingSpacing(markdown);
        return markdown.Trim();
    }

    public string Convert(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        Preprocess(document);
        var markdown = _converter.Convert(document.DocumentNode.OuterHtml);
        markdown = EnsureHeadingSpacing(markdown);
        return markdown.Trim();
    }

    private void Preprocess(HtmlDocument document)
    {
        RemoveScriptsAndStyles(document);
        HandleAnchors(document);
        HandleImages(document);
        HandleInputs(document);
    }

    private static void RemoveScriptsAndStyles(HtmlDocument document)
    {
        foreach (var node in document.DocumentNode.SelectNodes("//script|//style") ?? Enumerable.Empty<HtmlNode>())
        {
            node.Remove();
        }
    }

    private void HandleAnchors(HtmlDocument document)
    {
        foreach (var anchor in document.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>())
        {
            if (anchor.ParentNode?.Name.Equals("pre", StringComparison.OrdinalIgnoreCase) == true)
            {
                continue;
            }

            var href = anchor.GetAttributeValue("href", string.Empty);
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            if (!Uri.TryCreate(Uri.UnescapeDataString(href), UriKind.RelativeOrAbsolute, out var parsed))
            {
                anchor.Attributes.Remove("href");
                continue;
            }

            if (parsed.IsAbsoluteUri && parsed.Scheme is not ("http" or "https" or "file"))
            {
                anchor.Attributes.Remove("href");
                continue;
            }

            var escapedPath = parsed.IsAbsoluteUri
                ? parsed.GetComponents(UriComponents.HttpRequestUrl, UriFormat.UriEscaped)
                : Uri.EscapeUriString(parsed.ToString());

            anchor.SetAttributeValue("href", escapedPath);
        }
    }

    private void HandleImages(HtmlDocument document)
    {
        foreach (var image in document.DocumentNode.SelectNodes("//img") ?? Enumerable.Empty<HtmlNode>())
        {
            var src = image.GetAttributeValue("src", string.Empty);
            if (string.IsNullOrWhiteSpace(src))
            {
                src = image.GetAttributeValue("data-src", string.Empty);
            }

            if (string.IsNullOrWhiteSpace(src))
            {
                continue;
            }

            if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && !_options.KeepDataUris)
            {
                var commaIndex = src.IndexOf(',');
                src = commaIndex > 0 ? src[..commaIndex] + "..." : "data:...";
                image.SetAttributeValue("src", src);
            }
        }
    }

    private static void HandleInputs(HtmlDocument document)
    {
        foreach (var input in document.DocumentNode.SelectNodes("//input") ?? Enumerable.Empty<HtmlNode>())
        {
            var type = input.GetAttributeValue("type", string.Empty);
            if (!type.Equals("checkbox", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var isChecked = input.Attributes["checked"] != null;
            input.ParentNode?.InsertBefore(
                HtmlTextNode.CreateNode(isChecked ? "[x] " : "[ ] "),
                input);
            input.Remove();
        }
    }

    private static string EnsureHeadingSpacing(string markdown)
    {
        var builder = new StringBuilder();
        var lines = markdown.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith("#", StringComparison.Ordinal) && (i == 0 || lines[i - 1].Length > 0))
            {
                builder.AppendLine();
            }

            builder.AppendLine(line);
        }

        return builder.ToString();
    }
}

public sealed record CustomMarkdownOptions
{
    public bool KeepDataUris { get; init; }
}

