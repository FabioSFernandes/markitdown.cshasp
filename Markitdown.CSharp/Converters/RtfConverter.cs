using System.Text;
using MarkItDown.CSharp.Converters.Markdown;
using RtfPipe;

namespace MarkItDown.CSharp.Converters;

public sealed class RtfConverter : DocumentConverter
{
    private static readonly string[] AcceptedMimePrefixes =
    {
        "application/rtf",
        "text/rtf",
    };

    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".rtf",
    };

    private readonly CustomMarkdownConverter _markdownConverter;

    public RtfConverter()
    {
        _markdownConverter = new CustomMarkdownConverter();
    }

    public override bool Accepts(Stream fileStream, StreamInfo streamInfo, ConversionOptions options)
    {
        var extension = streamInfo.Extension ?? string.Empty;
        if (AcceptedExtensions.Contains(extension))
            return true;
        var mimetype = streamInfo.MimeType ?? string.Empty;
        return AcceptedMimePrefixes.Any(prefix => mimetype.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public override async Task<DocumentConverterResult> ConvertAsync(
        Stream fileStream,
        StreamInfo streamInfo,
        ConversionOptions options,
        CancellationToken cancellationToken = default)
    {
#if NETCOREAPP
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
        using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var rtf = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        string html;
        try
        {
            html = Rtf.ToHtml(rtf);
        }
        catch (Exception)
        {
            return new DocumentConverterResult(string.Empty);
        }

        if (string.IsNullOrWhiteSpace(html))
            return new DocumentConverterResult(string.Empty);

        var markdown = _markdownConverter.Convert(WrapHtmlBody(html));
        return new DocumentConverterResult(markdown.Trim());
    }

    private static string WrapHtmlBody(string html)
    {
        if (html.TrimStart().StartsWith("<!", StringComparison.OrdinalIgnoreCase) ||
            html.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase))
            return html;
        return "<html><body>" + html + "</body></html>";
    }
}
