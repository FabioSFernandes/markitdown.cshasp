using System.Data;
using System.Text;
using ExcelDataReader;
using System.Linq;

namespace MarkItDown.CSharp.Converters;

public sealed class XlsConverter : DocumentConverter
{
    private static readonly string[] AcceptedMimePrefixes =
    {
        "application/vnd.ms-excel",
        "application/excel",
    };

    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xls",
    };

    static XlsConverter()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

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
        using var reader = ExcelReaderFactory.CreateReader(fileStream);
        var result = reader.AsDataSet();
        var builder = new StringBuilder();

        foreach (DataTable table in result.Tables)
        {
            builder.AppendLine($"## {table.TableName}");
            builder.AppendLine(ConvertTableToMarkdown(table));
            builder.AppendLine();
        }

        return await Task.FromResult(new DocumentConverterResult(builder.ToString().Trim())).ConfigureAwait(false);
    }

    private static string ConvertTableToMarkdown(DataTable table)
    {
        if (table.Rows.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var headers = table.Columns.Cast<DataColumn>().Select(c => Escape(c.ColumnName)).ToArray();
        builder.AppendLine("| " + string.Join(" | ", headers) + " |");
        builder.AppendLine("| " + string.Join(" | ", headers.Select(_ => "---")) + " |");

        foreach (DataRow row in table.Rows)
        {
            var values = table.Columns.Cast<DataColumn>()
                .Select(c => Escape(row[c]?.ToString() ?? string.Empty));
            builder.AppendLine("| " + string.Join(" | ", values) + " |");
        }

        return builder.ToString().Trim();
    }

    private static string Escape(string value) => value.Replace("\r", " ").Replace("\n", " ");
}

