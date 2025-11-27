using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.AspNetCore.WebUtilities;
using System.Linq;

namespace MarkItDown.CSharp.Converters;

public sealed class YouTubeConverter : DocumentConverter
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

    public override bool Accepts(Stream fileStream, StreamInfo streamInfo, ConversionOptions options)
    {
        var url = streamInfo.Url ?? string.Empty;
        if (!url.StartsWith("https://www.youtube.com/watch?", StringComparison.OrdinalIgnoreCase))
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

        var metadata = ExtractMetadata(doc);
        var builder = new StringBuilder();
        builder.AppendLine("# YouTube");

        var title = GetFirst(metadata, "title", "og:title", "name") ?? doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();
        if (!string.IsNullOrWhiteSpace(title))
        {
            builder.AppendLine($"\n## {title}");
        }

        var stats = new List<string>();
        if (GetFirst(metadata, "interactionCount") is { } views)
        {
            stats.Add($"- **Views:** {views}");
        }
        if (GetFirst(metadata, "keywords") is { } keywords)
        {
            stats.Add($"- **Keywords:** {keywords}");
        }
        if (GetFirst(metadata, "duration") is { } duration)
        {
            stats.Add($"- **Runtime:** {duration}");
        }
        if (stats.Count > 0)
        {
            builder.AppendLine("\n### Video Metadata");
            builder.AppendLine(string.Join("\n", stats));
        }

        var description = GetDescription(doc, metadata);
        if (!string.IsNullOrWhiteSpace(description))
        {
            builder.AppendLine("\n### Description");
            builder.AppendLine(description.Trim());
        }

        var transcript = await FetchTranscriptAsync(streamInfo.Url, options, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(transcript))
        {
            builder.AppendLine("\n### Transcript");
            builder.AppendLine(transcript.Trim());
        }

        return new DocumentConverterResult(builder.ToString().Trim(), title);
    }

    private static Dictionary<string, string> ExtractMetadata(HtmlDocument doc)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var meta in doc.DocumentNode.SelectNodes("//meta") ?? Enumerable.Empty<HtmlNode>())
        {
            var key = meta.GetAttributeValue("itemprop", null)
                      ?? meta.GetAttributeValue("property", null)
                      ?? meta.GetAttributeValue("name", null);
            var content = meta.GetAttributeValue("content", null);

            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(content))
            {
                metadata[key] = content;
            }
        }

        if (doc.DocumentNode.SelectSingleNode("//title") is { } titleNode && !string.IsNullOrWhiteSpace(titleNode.InnerText))
        {
            metadata["title"] = titleNode.InnerText.Trim();
        }

        return metadata;
    }

    private static string GetDescription(HtmlDocument doc, Dictionary<string, string> metadata)
    {
        if (GetFirst(metadata, "description", "og:description") is { } desc)
        {
            return desc;
        }

        var scriptNodes = doc.DocumentNode.SelectNodes("//script") ?? new HtmlNodeCollection(null);
        foreach (var script in scriptNodes)
        {
            var content = script.InnerText;
            if (string.IsNullOrWhiteSpace(content) || !content.Contains("ytInitialData", StringComparison.Ordinal))
            {
                continue;
            }

            var match = Regex.Match(content, @"var ytInitialData = ({.*?});", RegexOptions.Singleline);
            if (!match.Success)
            {
                continue;
            }

            try
            {
                using var json = JsonDocument.Parse(match.Groups[1].Value);
                if (json.RootElement.TryGetProperty("contents", out var contents) &&
                    contents.TryGetProperty("twoColumnWatchNextResults", out var twoCol) &&
                    twoCol.TryGetProperty("results", out var results) &&
                    results.TryGetProperty("results", out var resultsInner) &&
                    resultsInner.TryGetProperty("contents", out var contentsArray) &&
                    contentsArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in contentsArray.EnumerateArray())
                    {
                        if (element.TryGetProperty("videoSecondaryInfoRenderer", out var infoRenderer) &&
                            infoRenderer.TryGetProperty("description", out var description))
                        {
                            return string.Concat(description.GetProperty("runs").EnumerateArray().Select(run => run.GetProperty("text").GetString()));
                        }
                    }
                }
            }
            catch
            {
                // Ignore parsing errors
            }
        }

        return string.Empty;
    }

    private static string? GetFirst(Dictionary<string, string> metadata, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static async Task<string?> FetchTranscriptAsync(string? url, ConversionOptions options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            return null;
        }

        var query = QueryHelpers.ParseQuery(parsed.Query);
        if (!query.TryGetValue("v", out var videoIds))
        {
            return null;
        }

        var videoId = videoIds.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return null;
        }

        var languages = options.Get<IReadOnlyList<string>>("youtube_transcript_languages")
                        ?? new[] { "en" };

        var httpClient = options.Get<HttpClient>("http_client") ?? new HttpClient();
        foreach (var lang in languages)
        {
            var requestUrl = $"https://youtubetranscript.com/?format=json&lang={lang}&server_vid2={videoId}";
            try
            {
                using var response = await httpClient.GetAsync(requestUrl, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using var json = JsonDocument.Parse(content);
                if (json.RootElement.TryGetProperty("transcripts", out var transcripts) &&
                    transcripts.ValueKind == JsonValueKind.Array &&
                    transcripts.GetArrayLength() > 0)
                {
                    var transcriptUrl = transcripts[0].GetProperty("url").GetString();
                    if (string.IsNullOrWhiteSpace(transcriptUrl))
                    {
                        continue;
                    }

                    using var transcriptResponse = await httpClient.GetAsync(transcriptUrl, cancellationToken).ConfigureAwait(false);
                    if (!transcriptResponse.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    var transcriptJson = await transcriptResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    using var transcriptDoc = JsonDocument.Parse(transcriptJson);
                    if (transcriptDoc.RootElement.TryGetProperty("text", out var textElement) &&
                        textElement.ValueKind == JsonValueKind.Array)
                    {
                        var transcriptBuilder = new StringBuilder();
                        foreach (var segment in textElement.EnumerateArray())
                        {
                            transcriptBuilder.Append(segment.GetString());
                            transcriptBuilder.Append(' ');
                        }

                        return transcriptBuilder.ToString().Trim();
                    }
                }
            }
            catch
            {
                // Ignore failures per language
            }
        }

        return null;
    }
}

