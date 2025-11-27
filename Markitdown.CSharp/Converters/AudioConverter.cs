using System.Text;
using MarkItDown.CSharp.Converters.Utilities;
using MarkItDown.CSharp.Exceptions;
using MarkItDown.CSharp.Interfaces;
using System.Linq;

namespace MarkItDown.CSharp.Converters;

public sealed class AudioConverter : DocumentConverter
{
    private static readonly string[] AcceptedMimePrefixes =
    {
        "audio/x-wav",
        "audio/mpeg",
        "video/mp4",
    };

    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav",
        ".mp3",
        ".m4a",
        ".mp4",
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
        var builder = new StringBuilder();
        var exiftoolPath = options.Get<string>("exiftool_path");
        var metadata = await ExifToolUtility.ReadMetadataAsync(fileStream, exiftoolPath, cancellationToken)
            .ConfigureAwait(false);

        var fields = new[]
        {
            "Title",
            "Artist",
            "Author",
            "Band",
            "Album",
            "Genre",
            "Track",
            "DateTimeOriginal",
            "CreateDate",
            "NumChannels",
            "SampleRate",
            "AvgBytesPerSec",
            "BitsPerSample",
        };

        foreach (var field in fields)
        {
            if (metadata.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                builder.AppendLine($"{field}: {value}");
            }
        }

        var format = ResolveFormat(streamInfo);
        if (format is not null)
        {
            var transcriptionService = options.Get<IAudioTranscriptionService>("audio_transcription_service");
            if (transcriptionService is not null)
            {
                var transcript = await TranscribeAsync(transcriptionService, fileStream, format, cancellationToken)
                    .ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(transcript))
                {
                    builder.AppendLine();
                    builder.AppendLine("### Audio Transcript");
                    builder.AppendLine(transcript);
                }
            }
        }

        return new DocumentConverterResult(builder.ToString().Trim());
    }

    private static string? ResolveFormat(StreamInfo streamInfo)
    {
        var extension = streamInfo.Extension?.ToLowerInvariant();
        return extension switch
        {
            ".wav" => "wav",
            ".mp3" => "mp3",
            ".mp4" or ".m4a" => "mp4",
            _ => streamInfo.MimeType switch
            {
                "audio/x-wav" => "wav",
                "audio/mpeg" => "mp3",
                "video/mp4" => "mp4",
                _ => null,
            },
        };
    }

    private static async Task<string?> TranscribeAsync(
        IAudioTranscriptionService service,
        Stream stream,
        string format,
        CancellationToken cancellationToken)
    {
        var position = stream.Position;
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
        stream.Seek(position, SeekOrigin.Begin);
        copy.Seek(0, SeekOrigin.Begin);

        return await service.TranscribeAsync(copy, format, cancellationToken).ConfigureAwait(false);
    }
}

