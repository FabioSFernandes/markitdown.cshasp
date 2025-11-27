using System.Text;
using System.Linq;

namespace MarkItDown.CSharp.Converters;

public sealed class CsvConverter : DocumentConverter
{
    private static readonly string[] AcceptedMimePrefixes =
    {
        "text/csv",
        "application/csv",
    };

    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csv",
    };

    public override bool Accepts(Stream fileStream, StreamInfo streamInfo, ConversionOptions options)
    {
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
        var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        if (lines.Length == 0)
        {
            return new DocumentConverterResult(string.Empty);
        }

        var table = new List<string[]>();
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }
            table.Add(ParseCsvLine(line));
        }

        if (table.Count == 0)
        {
            return new DocumentConverterResult(string.Empty);
        }

        var header = table[0];
        var builder = new StringBuilder();
        builder.AppendLine("| " + string.Join(" | ", header) + " |");
        builder.AppendLine("| " + string.Join(" | ", header.Select(_ => "---")) + " |");

        foreach (var row in table.Skip(1))
        {
            var normalizedRow = NormalizeRow(row, header.Length);
            builder.AppendLine("| " + string.Join(" | ", normalizedRow) + " |");
        }

        return new DocumentConverterResult(builder.ToString().Trim());
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        fields.Add(current.ToString());
        return fields.Select(f => f.Trim()).ToArray();
    }

    private static IEnumerable<string> NormalizeRow(IReadOnlyList<string> row, int columnCount)
    {
        if (row.Count == columnCount)
        {
            return row;
        }

        if (row.Count > columnCount)
        {
            return row.Take(columnCount);
        }

        return row.Concat(Enumerable.Repeat(string.Empty, columnCount - row.Count));
    }
}

