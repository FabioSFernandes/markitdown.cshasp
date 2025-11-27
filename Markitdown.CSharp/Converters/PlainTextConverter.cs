using System.Text;
using MarkItDown.CSharp.Exceptions;
using Ude;

namespace MarkItDown.CSharp.Converters;

public sealed class PlainTextConverter : DocumentConverter
{
    private static readonly string[] AcceptedMimePrefixes =
    {
        "text/",
        "application/json",
        "application/markdown",
    };

    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".text",
        ".md",
        ".markdown",
        ".json",
        ".jsonl",
    };

    public override bool Accepts(Stream fileStream, StreamInfo streamInfo, ConversionOptions options)
    {
        if (!string.IsNullOrWhiteSpace(streamInfo.Charset))
        {
            return true;
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
        var encoding = ResolveEncoding(streamInfo.Charset);
        var text = await ReadAllTextAsync(fileStream, encoding, cancellationToken).ConfigureAwait(false);
        return new DocumentConverterResult(text);
    }

    private static Encoding ResolveEncoding(string? charset)
    {
        if (!string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.GetEncoding(charset);
        }

        return Encoding.UTF8;
    }

    private static async Task<string> ReadAllTextAsync(
        Stream stream,
        Encoding encoding,
        CancellationToken cancellationToken)
    {
        if (encoding != Encoding.UTF8)
        {
            using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
        var buffer = memory.ToArray();

        if (buffer.Length == 0)
        {
            return string.Empty;
        }

        var detector = new CharsetDetector();
        detector.Feed(buffer, 0, buffer.Length);
        detector.DataEnd();

        var detected = detector.Charset;
        var chosenEncoding = !string.IsNullOrWhiteSpace(detected)
            ? Encoding.GetEncoding(detected)
            : Encoding.UTF8;

        return chosenEncoding.GetString(buffer);
    }
}

