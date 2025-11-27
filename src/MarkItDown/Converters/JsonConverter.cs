using System.Text;
using System.Text.Json;

namespace MarkItDown.Converters;

/// <summary>
/// Converter for JSON files to Markdown.
/// </summary>
public sealed class JsonConverter : DocumentConverterBase
{
    /// <inheritdoc />
    protected override IReadOnlyCollection<string> SupportedExtensions { get; } =
        new[] { ".json" };

    /// <inheritdoc />
    protected override IReadOnlyCollection<string> SupportedMimeTypes { get; } =
        new[] { "application/json", "text/json" };

    /// <inheritdoc />
    public override ConversionResult Convert(Stream stream, string? fileName = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var content = ReadAllText(stream);
        var markdown = ConvertJsonToMarkdown(content);
        var title = string.IsNullOrEmpty(fileName) ? null : Path.GetFileNameWithoutExtension(fileName);

        return new ConversionResult(markdown, title, "application/json");
    }

    private static string ConvertJsonToMarkdown(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var sb = new StringBuilder();
            ConvertElement(doc.RootElement, sb, 0);
            return sb.ToString().Trim();
        }
        catch (JsonException)
        {
            // If parsing fails, return the raw content as a code block
            return $"```json\n{json}\n```";
        }
    }

    private static void ConvertElement(JsonElement element, StringBuilder sb, int depth)
    {
        var indent = new string(' ', depth * 2);

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                ConvertObject(element, sb, depth);
                break;

            case JsonValueKind.Array:
                ConvertArray(element, sb, depth);
                break;

            case JsonValueKind.String:
                sb.Append(element.GetString());
                break;

            case JsonValueKind.Number:
                sb.Append(element.GetRawText());
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                sb.Append(element.GetBoolean() ? "true" : "false");
                break;

            case JsonValueKind.Null:
                sb.Append("null");
                break;
        }
    }

    private static void ConvertObject(JsonElement element, StringBuilder sb, int depth)
    {
        var indent = new string(' ', depth * 2);
        var isFirstProperty = true;

        foreach (var property in element.EnumerateObject())
        {
            if (!isFirstProperty)
            {
                sb.AppendLine();
            }
            isFirstProperty = false;

            if (property.Value.ValueKind == JsonValueKind.Object ||
                property.Value.ValueKind == JsonValueKind.Array)
            {
                sb.AppendLine($"{indent}**{property.Name}:**");
                ConvertElement(property.Value, sb, depth + 1);
            }
            else
            {
                sb.Append($"{indent}- **{property.Name}:** ");
                ConvertElement(property.Value, sb, depth);
                sb.AppendLine();
            }
        }
    }

    private static void ConvertArray(JsonElement element, StringBuilder sb, int depth)
    {
        var indent = new string(' ', depth * 2);
        var index = 0;

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array)
            {
                sb.AppendLine($"{indent}{index + 1}.");
                ConvertElement(item, sb, depth + 1);
            }
            else
            {
                sb.Append($"{indent}- ");
                ConvertElement(item, sb, depth);
                sb.AppendLine();
            }
            index++;
        }
    }
}
