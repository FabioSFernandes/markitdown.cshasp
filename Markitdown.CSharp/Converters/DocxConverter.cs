using MarkItDown.CSharp.ConverterUtils.Docx;
using System.Linq;
using MammothDocumentConverter = Mammoth.DocumentConverter;

namespace MarkItDown.CSharp.Converters;

public sealed class DocxConverter : DocumentConverter
{
    private static readonly string[] AcceptedMimePrefixes =
    {
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    };

    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".docx",
    };

    private readonly HtmlConverter _htmlConverter = new();

    public override bool Accepts(Stream fileStream, StreamInfo streamInfo, ConversionOptions options)
    {
        var extension = streamInfo.Extension ?? string.Empty;
        var mimetype = streamInfo.MimeType ?? string.Empty;
        
        var acceptsByExtension = AcceptedExtensions.Contains(extension);
        var acceptsByMime = AcceptedMimePrefixes.Any(prefix => mimetype.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        
        return acceptsByExtension || acceptsByMime;
    }

    public override Task<DocumentConverterResult> ConvertAsync(
        Stream fileStream,
        StreamInfo streamInfo,
        ConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        if (fileStream.CanSeek)
        {
            fileStream.Seek(0, SeekOrigin.Begin);
        }

        // Create a clean copy of the original stream for potential fallback
        var fallbackStream = new MemoryStream();
        fileStream.CopyTo(fallbackStream);
        fallbackStream.Seek(0, SeekOrigin.Begin);

        // Reset original stream position for pre-processing attempt
        if (fileStream.CanSeek)
        {
            fileStream.Seek(0, SeekOrigin.Begin);
        }

        MemoryStream? processed = null;
        Stream? conversionStream = null;
        bool useFallback = false;

        try
        {
            // Try to pre-process the original stream
            processed = DocxPreprocessor.PreProcess(fileStream);
            conversionStream = processed;
        }
        catch (InvalidDataException)
        {
            // Pre-processing failed - use the clean fallback copy
            useFallback = true;
            fallbackStream.Seek(0, SeekOrigin.Begin);
            conversionStream = fallbackStream;
        }
        catch (Exception)
        {
            // Any other exception during pre-processing - use the clean fallback
            useFallback = true;
            fallbackStream.Seek(0, SeekOrigin.Begin);
            conversionStream = fallbackStream;
        }

        try
        {
            var mammothConverter = new MammothDocumentConverter();
            var styleMapOption = options.Get<string>("style_map");
            if (!string.IsNullOrWhiteSpace(styleMapOption))
            {
                mammothConverter = mammothConverter.AddStyleMap(styleMapOption);
            }

            if (conversionStream.CanSeek)
            {
                conversionStream.Seek(0, SeekOrigin.Begin);
            }

            var conversionResult = mammothConverter.ConvertToHtml(conversionStream);
            var htmlContent = conversionResult.Value;
            
            return Task.FromResult(_htmlConverter.ConvertFromString(htmlContent));
        }
        finally
        {
            // Dispose streams appropriately
            if (useFallback)
            {
                // conversionStream is fallbackStream, dispose it
                // processed was not created, so nothing to dispose there
                fallbackStream?.Dispose();
            }
            else
            {
                // conversionStream is processed, dispose it
                processed?.Dispose();
                // Also dispose the unused fallbackStream
                fallbackStream?.Dispose();
            }
        }
    }
}
