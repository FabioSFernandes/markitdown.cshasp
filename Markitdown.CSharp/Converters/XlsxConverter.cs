using System.Text;
using ClosedXML.Excel;
using System.Linq;

namespace MarkItDown.CSharp.Converters;

public sealed class XlsxConverter : DocumentConverter
{
    private static readonly string[] AcceptedMimePrefixes =
    {
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    };

    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx",
    };

    private readonly HtmlConverter _htmlConverter = new();

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

    public override Task<DocumentConverterResult> ConvertAsync(
        Stream fileStream,
        StreamInfo streamInfo,
        ConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var builder = new StringBuilder();

        foreach (var worksheet in workbook.Worksheets)
        {
            builder.AppendLine($"## {worksheet.Name}");
            builder.AppendLine(ConvertWorksheetToMarkdown(worksheet));
            builder.AppendLine();
        }

        return Task.FromResult(new DocumentConverterResult(builder.ToString().Trim()));
    }

    private string ConvertWorksheetToMarkdown(IXLWorksheet worksheet)
    {
        var table = new StringBuilder();
        var rows = worksheet.RangeUsed()?.RowsUsed().ToList();
        if (rows is null || rows.Count == 0)
        {
            return string.Empty;
        }

        var header = rows.First().Cells().Select(c => Escape(c.GetString())).ToArray();
        table.AppendLine("| " + string.Join(" | ", header) + " |");
        table.AppendLine("| " + string.Join(" | ", header.Select(_ => "---")) + " |");

        foreach (var row in rows.Skip(1))
        {
            var cells = row.Cells(1, header.Length).Select(c => Escape(c.GetString()));
            table.AppendLine("| " + string.Join(" | ", cells) + " |");
        }

        return table.ToString().Trim();
    }

    private static string Escape(string value) => value.Replace("\r", " ").Replace("\n", " ");
}

