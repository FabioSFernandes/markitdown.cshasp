using System.Text;
using System.Text.Json;
using System.Linq;

namespace MarkItDown.CSharp.Converters;

public sealed class IpynbConverter : DocumentConverter
{
    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ipynb",
    };

    private static readonly string[] CandidateMimes =
    {
        "application/json",
    };

    public override bool Accepts(Stream fileStream, StreamInfo streamInfo, ConversionOptions options)
    {
        var extension = streamInfo.Extension ?? string.Empty;
        if (AcceptedExtensions.Contains(extension))
        {
            return true;
        }

        var mimetype = streamInfo.MimeType ?? string.Empty;
        if (CandidateMimes.Any(prefix => mimetype.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            var position = fileStream.Position;
            try
            {
                using var reader = new StreamReader(fileStream, Encoding.UTF8, leaveOpen: true);
                var content = reader.ReadToEnd();
                return content.Contains("\"nbformat\"", StringComparison.Ordinal);
            }
            finally
            {
                fileStream.Seek(position, SeekOrigin.Begin);
            }
        }

        return false;
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
        var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        var json = JsonDocument.Parse(content);
        var builder = new StringBuilder();
        string? title = null;

        if (json.RootElement.TryGetProperty("cells", out var cells) && cells.ValueKind == JsonValueKind.Array)
        {
            foreach (var cell in cells.EnumerateArray())
            {
                var cellType = cell.GetProperty("cell_type").GetString();
                var source = cell.TryGetProperty("source", out var srcElement) ? srcElement : default;
                var lines = ExtractLines(source);

                if (cellType == "markdown")
                {
                    var text = string.Join("", lines);
                    builder.AppendLine(text.TrimEnd());

                    if (title is null)
                    {
                        var heading = lines.FirstOrDefault(line => line.StartsWith("# "));
                        if (heading is not null)
                        {
                            title = heading.TrimStart('#', ' ');
                        }
                    }
                }
                else if (cellType == "code")
                {
                    builder.AppendLine("```python");
                    builder.AppendLine(string.Join("", lines));
                    builder.AppendLine("```");
                }
                else if (cellType == "raw")
                {
                    builder.AppendLine("```");
                    builder.AppendLine(string.Join("", lines));
                    builder.AppendLine("```");
                }

                builder.AppendLine();
            }
        }

        var metadata = json.RootElement.TryGetProperty("metadata", out var metadataElement)
            ? metadataElement
            : default;
        if (metadata.ValueKind == JsonValueKind.Object &&
            metadata.TryGetProperty("title", out var titleElement) &&
            titleElement.ValueKind == JsonValueKind.String)
        {
            title = titleElement.GetString();
        }

        return new DocumentConverterResult(builder.ToString().Trim(), title);
    }

    private static IEnumerable<string> ExtractLines(JsonElement sourceElement)
    {
        return sourceElement.ValueKind switch
        {
            JsonValueKind.Array => sourceElement.EnumerateArray().Select(line => line.GetString() ?? string.Empty),
            JsonValueKind.String => new[] { sourceElement.GetString() ?? string.Empty },
            _ => Array.Empty<string>(),
        };
    }
}

