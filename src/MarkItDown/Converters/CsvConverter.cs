using System.Text;

namespace MarkItDown.Converters;

/// <summary>
/// Converter for CSV files to Markdown tables.
/// </summary>
public sealed class CsvConverter : DocumentConverterBase
{
    /// <inheritdoc />
    protected override IReadOnlyCollection<string> SupportedExtensions { get; } =
        new[] { ".csv" };

    /// <inheritdoc />
    protected override IReadOnlyCollection<string> SupportedMimeTypes { get; } =
        new[] { "text/csv", "application/csv" };

    /// <summary>
    /// Gets or sets the delimiter used to separate values.
    /// </summary>
    public char Delimiter { get; set; } = ',';

    /// <inheritdoc />
    public override ConversionResult Convert(Stream stream, string? fileName = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var content = ReadAllText(stream);
        var lines = ParseCsvLines(content);
        
        if (lines.Count == 0)
        {
            return new ConversionResult(string.Empty, null, "text/csv");
        }

        var markdown = ConvertToMarkdownTable(lines);
        var title = string.IsNullOrEmpty(fileName) ? null : Path.GetFileNameWithoutExtension(fileName);

        return new ConversionResult(markdown, title, "text/csv");
    }

    private List<string[]> ParseCsvLines(string content)
    {
        var lines = new List<string[]>();
        var rows = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var row in rows)
        {
            var trimmedRow = row.TrimEnd('\r');
            if (!string.IsNullOrWhiteSpace(trimmedRow))
            {
                lines.Add(ParseCsvRow(trimmedRow));
            }
        }
        
        return lines;
    }

    private string[] ParseCsvRow(string row)
    {
        var fields = new List<string>();
        var currentField = new StringBuilder();
        var inQuotes = false;
        var i = 0;

        while (i < row.Length)
        {
            var c = row[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Check for escaped quote
                    if (i + 1 < row.Length && row[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i += 2;
                        continue;
                    }
                    inQuotes = false;
                }
                else
                {
                    currentField.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == Delimiter)
                {
                    fields.Add(currentField.ToString().Trim());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            i++;
        }

        // Add the last field
        fields.Add(currentField.ToString().Trim());

        return fields.ToArray();
    }

    private static string ConvertToMarkdownTable(List<string[]> lines)
    {
        if (lines.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        var columnCount = lines.Max(l => l.Length);

        // Header row
        var header = lines[0];
        sb.Append("| ");
        sb.Append(string.Join(" | ", PadOrTruncate(header, columnCount)));
        sb.AppendLine(" |");

        // Separator row
        sb.Append("| ");
        sb.Append(string.Join(" | ", Enumerable.Repeat("---", columnCount)));
        sb.AppendLine(" |");

        // Data rows
        for (int i = 1; i < lines.Count; i++)
        {
            sb.Append("| ");
            sb.Append(string.Join(" | ", PadOrTruncate(lines[i], columnCount)));
            sb.AppendLine(" |");
        }

        return sb.ToString().TrimEnd();
    }

    private static string[] PadOrTruncate(string[] row, int columnCount)
    {
        var result = new string[columnCount];
        for (int i = 0; i < columnCount; i++)
        {
            result[i] = i < row.Length ? EscapePipe(row[i]) : string.Empty;
        }
        return result;
    }

    private static string EscapePipe(string value)
    {
        return value.Replace("|", "\\|");
    }
}
