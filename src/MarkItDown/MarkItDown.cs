using MarkItDown.Converters;

namespace MarkItDown;

/// <summary>
/// Main class for converting various document formats to Markdown.
/// </summary>
public sealed class MarkItDown
{
    private readonly List<IDocumentConverter> _converters = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkItDown"/> class with default converters.
    /// </summary>
    public MarkItDown()
    {
        // Register default converters
        RegisterConverter(new PlainTextConverter());
        RegisterConverter(new HtmlConverter());
        RegisterConverter(new CsvConverter());
        RegisterConverter(new JsonConverter());
    }

    /// <summary>
    /// Registers a custom converter.
    /// </summary>
    /// <param name="converter">The converter to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when converter is null.</exception>
    public void RegisterConverter(IDocumentConverter converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        _converters.Insert(0, converter); // Insert at the beginning for priority
    }

    /// <summary>
    /// Gets the registered converters.
    /// </summary>
    public IReadOnlyList<IDocumentConverter> Converters => _converters.AsReadOnly();

    /// <summary>
    /// Converts a file to Markdown.
    /// </summary>
    /// <param name="filePath">The path to the file to convert.</param>
    /// <returns>The conversion result containing Markdown content.</returns>
    /// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="NotSupportedException">Thrown when no converter supports the file type.</exception>
    public ConversionResult Convert(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found.", filePath);
        }

        var converter = FindConverter(filePath);
        if (converter == null)
        {
            throw new NotSupportedException($"No converter found for file: {filePath}");
        }

        return converter.Convert(filePath);
    }

    /// <summary>
    /// Converts content from a stream to Markdown.
    /// </summary>
    /// <param name="stream">The stream containing the content to convert.</param>
    /// <param name="fileName">Optional file name to help identify the content type.</param>
    /// <returns>The conversion result containing Markdown content.</returns>
    /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
    /// <exception cref="NotSupportedException">Thrown when no converter supports the content type.</exception>
    public ConversionResult Convert(Stream stream, string? fileName = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        IDocumentConverter? converter = null;
        
        if (!string.IsNullOrEmpty(fileName))
        {
            converter = FindConverter(fileName);
        }

        if (converter == null)
        {
            throw new NotSupportedException($"No converter found for file: {fileName ?? "unknown"}");
        }

        return converter.Convert(stream, fileName);
    }

    /// <summary>
    /// Converts content from a stream to Markdown using a specific MIME type.
    /// </summary>
    /// <param name="stream">The stream containing the content to convert.</param>
    /// <param name="mimeType">The MIME type of the content.</param>
    /// <param name="fileName">Optional file name.</param>
    /// <returns>The conversion result containing Markdown content.</returns>
    /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
    /// <exception cref="NotSupportedException">Thrown when no converter supports the MIME type.</exception>
    public ConversionResult ConvertByMimeType(Stream stream, string mimeType, string? fileName = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrEmpty(mimeType);

        var converter = FindConverterByMimeType(mimeType);
        if (converter == null)
        {
            throw new NotSupportedException($"No converter found for MIME type: {mimeType}");
        }

        return converter.Convert(stream, fileName);
    }

    /// <summary>
    /// Checks if a file type is supported.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <returns>True if the file type is supported; otherwise, false.</returns>
    public bool IsSupported(string filePath)
    {
        return FindConverter(filePath) != null;
    }

    /// <summary>
    /// Checks if a MIME type is supported.
    /// </summary>
    /// <param name="mimeType">The MIME type to check.</param>
    /// <returns>True if the MIME type is supported; otherwise, false.</returns>
    public bool IsMimeTypeSupported(string mimeType)
    {
        return FindConverterByMimeType(mimeType) != null;
    }

    private IDocumentConverter? FindConverter(string filePath)
    {
        return _converters.FirstOrDefault(c => c.CanConvert(filePath));
    }

    private IDocumentConverter? FindConverterByMimeType(string mimeType)
    {
        return _converters.FirstOrDefault(c => c.CanConvertMimeType(mimeType));
    }
}
