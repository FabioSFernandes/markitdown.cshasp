using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using System.Linq;

namespace MarkItDown.CSharp.Converters;

public sealed class PdfConverter : DocumentConverter
{
    private static readonly string[] AcceptedMimePrefixes =
    {
        "application/pdf",
        "application/x-pdf",
    };

    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
    };

    private const double SpaceGapFactor = 0.7;
    private const double SameLineHeightFactor = 0.45;
    private const double ParagraphGapFactor = 1.4;

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
        using var pdf = PdfDocument.Open(fileStream, new ParsingOptions { UseLenientParsing = true });
        var builder = new System.Text.StringBuilder();

        foreach (var page in pdf.GetPages())
        {
            var pageText = ExtractTextWithLayout(page);
            if (pageText.Length > 0)
            {
                builder.Append(pageText);
                if (!pageText.EndsWith("\n\n", StringComparison.Ordinal))
                {
                    builder.Append("\n\n");
                }
            }
        }

        return Task.FromResult(new DocumentConverterResult(NormalizeMarkdown(builder.ToString())));
    }

    private static string ExtractTextWithLayout(Page page)
    {
        var letters = page.Letters;
        if (letters.Count == 0)
        {
            return page.Text;
        }

        var list = letters.ToList();
        double medianWidth = Median(list, l => l.GlyphRectangle.Width);
        double medianHeight = Median(list, l => l.GlyphRectangle.Height);
        double spaceThreshold = Math.Max(medianWidth * SpaceGapFactor, 1.0);
        double sameLineThreshold = Math.Max(medianHeight * SameLineHeightFactor, 0.5);

        // Group letters into lines by similar baseline (Bottom). PDF Y is bottom-up; higher Bottom = higher on page.
        var lines = new List<List<Letter>>();
        var sortedByY = list.OrderByDescending(l => l.GlyphRectangle.Bottom).ToList();
        double? lineBaseline = null;
        var currentLine = new List<Letter>();

        foreach (var letter in sortedByY)
        {
            var bottom = letter.GlyphRectangle.Bottom;
            if (!lineBaseline.HasValue || Math.Abs(bottom - lineBaseline.Value) <= sameLineThreshold)
            {
                if (currentLine.Count == 0)
                    lineBaseline = bottom;
                currentLine.Add(letter);
            }
            else
            {
                if (currentLine.Count > 0)
                {
                    lines.Add(currentLine);
                    currentLine = new List<Letter>();
                    lineBaseline = bottom;
                }
                currentLine.Add(letter);
            }
        }

        if (currentLine.Count > 0)
            lines.Add(currentLine);

        // Sort each line left-to-right, then output with space and paragraph detection
        double prevLineBottom = double.MinValue;
        var lineGaps = new List<double>();
        var sb = new System.Text.StringBuilder();

        foreach (var lineLetters in lines)
        {
            var orderedLine = lineLetters.OrderBy(l => l.GlyphRectangle.Left).ToList();
            double? prevRight = null;
            double lineBottom = orderedLine.Count > 0 ? orderedLine[0].GlyphRectangle.Bottom : 0;

            if (prevLineBottom != double.MinValue)
            {
                double deltaY = prevLineBottom - lineBottom;
                lineGaps.Add(deltaY);
                double lineHeight = lineGaps.Count <= 20 ? lineGaps.Average() : lineGaps.TakeLast(20).Average();
                sb.Append('\n');
                if (deltaY > lineHeight * ParagraphGapFactor)
                    sb.Append('\n');
            }

            foreach (var letter in orderedLine)
            {
                var rect = letter.GlyphRectangle;
                string value = letter.Value ?? string.Empty;
                if (prevRight.HasValue)
                {
                    double gap = rect.Left - prevRight.Value;
                    if (gap > spaceThreshold)
                        sb.Append(' ');
                }
                sb.Append(value);
                prevRight = rect.Right;
            }

            prevLineBottom = lineBottom;
        }

        return sb.ToString();
    }

    private static double Median(List<Letter> letters, Func<Letter, double> selector)
    {
        if (letters.Count == 0) return 0;
        var sorted = letters.Select(selector).OrderBy(x => x).ToList();
        int m = sorted.Count / 2;
        return sorted.Count % 2 != 0 ? sorted[m] : (sorted[m - 1] + sorted[m]) / 2.0;
    }

    private static string NormalizeMarkdown(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var lines = text.Split('\n').Select(l => l.TrimEnd()).ToList();
        var result = new System.Text.StringBuilder();
        int emptyCount = 0;
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                emptyCount++;
                if (emptyCount <= 2) result.AppendLine();
            }
            else
            {
                emptyCount = 0;
                result.AppendLine(line);
            }
        }
        return result.ToString().Trim();
    }
}
