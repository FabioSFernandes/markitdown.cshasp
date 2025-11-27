using System.IO.Compression;
using System.Text;
using MarkItDown.CSharp.Exceptions;
using System.Linq;

namespace MarkItDown.CSharp.Converters;

public sealed class ZipConverter : DocumentConverter
{
    private static readonly string[] AcceptedMimePrefixes =
    {
        "application/zip",
    };

    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip",
    };

    private readonly MarkItDown _markItDown;

    public ZipConverter(MarkItDown markItDown)
    {
        _markItDown = markItDown;
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
        var builder = new StringBuilder();
        var filePath = streamInfo.Url ?? streamInfo.LocalPath ?? streamInfo.FileName ?? "archive.zip";
        builder.AppendLine($"Content from the zip file `{filePath}`:\n");

        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: true);

        foreach (var entry in archive.Entries)
        {
            if (entry.Length == 0)
            {
                continue;
            }

            using var entryStream = entry.Open();
            using var buffer = new MemoryStream();
            await entryStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            buffer.Seek(0, SeekOrigin.Begin);

            var entryInfo = new StreamInfo
            {
                Extension = Path.GetExtension(entry.FullName),
                FileName = Path.GetFileName(entry.FullName),
            };

            try
            {
                var result = await _markItDown.ConvertStreamAsync(buffer, entryInfo, options, cancellationToken)
                    .ConfigureAwait(false);

                builder.AppendLine($"## File: {entry.FullName}\n");
                builder.AppendLine(result.Markdown);
                builder.AppendLine();
            }
            catch (UnsupportedFormatException)
            {
                // Ignore files we can't process
            }
            catch (FileConversionException)
            {
                // Skip failed conversions
            }
        }

        return new DocumentConverterResult(builder.ToString().Trim());
    }
}

