using System.Text.RegularExpressions;
using MarkItDown.Converters;
using MarkItDown.Exceptions;

namespace MarkItDown;

/// <summary>
/// Represents the priority level for converter registration.
/// Lower values are tried first.
/// </summary>
public static class ConverterPriority
{
    /// <summary>
    /// Priority for specific file format converters (e.g., .docx, .pdf, .xlsx).
    /// </summary>
    public const float SpecificFileFormat = 0.0f;

    /// <summary>
    /// Priority for generic file format converters (e.g., text/*, html).
    /// </summary>
    public const float GenericFileFormat = 10.0f;
}

/// <summary>
/// Registration information for a document converter.
/// </summary>
internal sealed class ConverterRegistration
{
    public required IDocumentConverter Converter { get; init; }
    public required float Priority { get; init; }
}

/// <summary>
/// A simple document reader that converts various file types to Markdown.
/// </summary>
public sealed partial class MarkItDown
{
    private readonly List<ConverterRegistration> _converters = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkItDown"/> class with default converters.
    /// </summary>
    public MarkItDown() : this(enableBuiltins: true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkItDown"/> class.
    /// </summary>
    /// <param name="enableBuiltins">Whether to enable built-in converters.</param>
    public MarkItDown(bool enableBuiltins)
    {
        if (enableBuiltins)
        {
            EnableBuiltins();
        }
    }

    /// <summary>
    /// Enables and registers built-in converters.
    /// </summary>
    public void EnableBuiltins()
    {
        // Register converters in order from most generic to most specific
        // Later registrations have higher priority (tried first)
        RegisterConverter(new PlainTextConverter(), ConverterPriority.GenericFileFormat);
        RegisterConverter(new HtmlConverter(), ConverterPriority.GenericFileFormat);
    }

    /// <summary>
    /// Registers a document converter with the specified priority.
    /// </summary>
    /// <param name="converter">The converter to register.</param>
    /// <param name="priority">The priority for this converter. Lower values are tried first.</param>
    public void RegisterConverter(IDocumentConverter converter, float priority = ConverterPriority.SpecificFileFormat)
    {
        // Insert at the beginning so that later registrations have higher priority
        _converters.Insert(0, new ConverterRegistration
        {
            Converter = converter,
            Priority = priority
        });
    }

    /// <summary>
    /// Converts a file at the specified path to Markdown.
    /// </summary>
    /// <param name="path">The path to the file to convert.</param>
    /// <param name="streamInfo">Optional stream information.</param>
    /// <returns>The conversion result.</returns>
    public DocumentConverterResult Convert(string path, StreamInfo? streamInfo = null)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme == "http" || uri.Scheme == "https")
            {
                return ConvertUri(uri, streamInfo);
            }
            if (uri.Scheme == "file")
            {
                return ConvertLocal(uri.LocalPath, streamInfo);
            }
        }

        // Assume it's a local file path
        return ConvertLocal(path, streamInfo);
    }

    /// <summary>
    /// Converts a local file to Markdown.
    /// </summary>
    /// <param name="path">The path to the local file.</param>
    /// <param name="streamInfo">Optional stream information.</param>
    /// <returns>The conversion result.</returns>
    public DocumentConverterResult ConvertLocal(string path, StreamInfo? streamInfo = null)
    {
        var baseInfo = new StreamInfo
        {
            LocalPath = path,
            Extension = Path.GetExtension(path),
            Filename = Path.GetFileName(path)
        };

        if (streamInfo is not null)
        {
            baseInfo = baseInfo.CopyAndUpdate(streamInfo);
        }

        using var fileStream = File.OpenRead(path);
        var guesses = GetStreamInfoGuesses(fileStream, baseInfo);
        return ConvertInternal(fileStream, guesses);
    }

    /// <summary>
    /// Converts a stream to Markdown.
    /// </summary>
    /// <param name="stream">The stream to convert.</param>
    /// <param name="streamInfo">Optional stream information.</param>
    /// <returns>The conversion result.</returns>
    public DocumentConverterResult ConvertStream(Stream stream, StreamInfo? streamInfo = null)
    {
        // Ensure stream is seekable
        Stream seekableStream;
        if (!stream.CanSeek)
        {
            var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            seekableStream = memoryStream;
        }
        else
        {
            seekableStream = stream;
        }

        var guesses = GetStreamInfoGuesses(seekableStream, streamInfo ?? new StreamInfo());
        return ConvertInternal(seekableStream, guesses);
    }

    /// <summary>
    /// Converts a URI to Markdown.
    /// </summary>
    /// <param name="uri">The URI to convert.</param>
    /// <param name="streamInfo">Optional stream information.</param>
    /// <returns>The conversion result.</returns>
    public DocumentConverterResult ConvertUri(Uri uri, StreamInfo? streamInfo = null)
    {
        if (uri.Scheme == "file")
        {
            return ConvertLocal(uri.LocalPath, streamInfo);
        }

        if (uri.Scheme != "http" && uri.Scheme != "https")
        {
            throw new ArgumentException($"Unsupported URI scheme: {uri.Scheme}");
        }

        using var httpClient = new HttpClient();
        using var response = httpClient.GetAsync(uri).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        var baseInfo = new StreamInfo
        {
            Url = uri.ToString(),
            Filename = Path.GetFileName(uri.LocalPath),
            Extension = Path.GetExtension(uri.LocalPath)
        };

        // Try to get MIME type from response
        if (response.Content.Headers.ContentType?.MediaType is { } mediaType)
        {
            baseInfo = baseInfo.CopyAndUpdate(new StreamInfo { MimeType = mediaType });
        }

        if (streamInfo is not null)
        {
            baseInfo = baseInfo.CopyAndUpdate(streamInfo);
        }

        using var stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
        var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        memoryStream.Position = 0;

        var guesses = GetStreamInfoGuesses(memoryStream, baseInfo);
        return ConvertInternal(memoryStream, guesses);
    }

    private DocumentConverterResult ConvertInternal(Stream stream, List<StreamInfo> guesses)
    {
        var failedAttempts = new List<FailedConversionAttempt>();

        // Sort converters by priority
        var sortedConverters = _converters.OrderBy(r => r.Priority).ToList();
        var initialPosition = stream.Position;

        foreach (var guess in guesses.Concat(new[] { new StreamInfo() }))
        {
            foreach (var registration in sortedConverters)
            {
                var converter = registration.Converter;

                // Check if converter accepts this stream
                bool accepts;
                try
                {
                    accepts = converter.Accepts(stream, guess);
                }
                catch
                {
                    accepts = false;
                }
                finally
                {
                    stream.Position = initialPosition;
                }

                if (!accepts) continue;

                // Try to convert
                try
                {
                    var result = converter.Convert(stream, guess);
                    result.TextContent = NormalizeContent(result.TextContent);
                    return result;
                }
                catch (Exception ex)
                {
                    failedAttempts.Add(new FailedConversionAttempt
                    {
                        Converter = converter,
                        Exception = ex
                    });
                }
                finally
                {
                    stream.Position = initialPosition;
                }
            }
        }

        if (failedAttempts.Count > 0)
        {
            throw new FileConversionException(failedAttempts);
        }

        throw new UnsupportedFormatException(
            "Could not convert stream to Markdown. No converter attempted a conversion, suggesting that the filetype is simply not supported.");
    }

    private List<StreamInfo> GetStreamInfoGuesses(Stream stream, StreamInfo baseGuess)
    {
        var guesses = new List<StreamInfo>();

        // Enhance base guess with MIME type from extension if not set
        var enhanced = baseGuess;
        if (enhanced.MimeType is null && enhanced.Extension is not null)
        {
            var mimeType = GetMimeTypeFromExtension(enhanced.Extension);
            if (mimeType is not null)
            {
                enhanced = enhanced.CopyAndUpdate(new StreamInfo { MimeType = mimeType });
            }
        }

        // If MIME type is set but extension isn't, try to guess extension
        if (enhanced.MimeType is not null && enhanced.Extension is null)
        {
            var extension = GetExtensionFromMimeType(enhanced.MimeType);
            if (extension is not null)
            {
                enhanced = enhanced.CopyAndUpdate(new StreamInfo { Extension = extension });
            }
        }

        guesses.Add(enhanced);
        return guesses;
    }

    private static string? GetMimeTypeFromExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".txt" => "text/plain",
            ".html" or ".htm" => "text/html",
            ".xhtml" => "application/xhtml+xml",
            ".xml" => "application/xml",
            ".json" => "application/json",
            ".md" or ".markdown" => "text/markdown",
            ".csv" => "text/csv",
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".doc" => "application/msword",
            ".xls" => "application/vnd.ms-excel",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".mp4" => "video/mp4",
            ".zip" => "application/zip",
            ".epub" => "application/epub+zip",
            _ => null
        };
    }

    private static string? GetExtensionFromMimeType(string mimeType)
    {
        return mimeType.ToLowerInvariant() switch
        {
            "text/plain" => ".txt",
            "text/html" => ".html",
            "application/xhtml+xml" => ".xhtml",
            "application/xml" or "text/xml" => ".xml",
            "application/json" => ".json",
            "text/markdown" => ".md",
            "text/csv" => ".csv",
            "application/pdf" => ".pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ".pptx",
            "application/msword" => ".doc",
            "application/vnd.ms-excel" => ".xls",
            "application/vnd.ms-powerpoint" => ".ppt",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/gif" => ".gif",
            "image/svg+xml" => ".svg",
            "audio/mpeg" => ".mp3",
            "audio/wav" => ".wav",
            "video/mp4" => ".mp4",
            "application/zip" => ".zip",
            "application/epub+zip" => ".epub",
            _ => null
        };
    }

    private static string NormalizeContent(string content)
    {
        // Trim trailing whitespace from each line
        var lines = content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].TrimEnd();
        }
        content = string.Join("\n", lines);

        // Replace multiple newlines with double newlines
        content = MultipleNewlinesRegex().Replace(content, "\n\n");

        return content.Trim();
    }

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleNewlinesRegex();
}
