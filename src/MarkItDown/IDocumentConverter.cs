namespace MarkItDown;

/// <summary>
/// Defines the interface for document converters.
/// </summary>
public interface IDocumentConverter
{
    /// <summary>
    /// Determines whether this converter can handle the specified file.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <returns>True if this converter can handle the file; otherwise, false.</returns>
    bool CanConvert(string filePath);

    /// <summary>
    /// Determines whether this converter can handle content of the specified MIME type.
    /// </summary>
    /// <param name="mimeType">The MIME type of the content.</param>
    /// <returns>True if this converter can handle the content; otherwise, false.</returns>
    bool CanConvertMimeType(string mimeType);

    /// <summary>
    /// Converts the specified file to markdown.
    /// </summary>
    /// <param name="filePath">The path to the file to convert.</param>
    /// <returns>The conversion result containing markdown content.</returns>
    ConversionResult Convert(string filePath);

    /// <summary>
    /// Converts content from a stream to markdown.
    /// </summary>
    /// <param name="stream">The stream containing the content to convert.</param>
    /// <param name="fileName">Optional file name to help identify the content type.</param>
    /// <returns>The conversion result containing markdown content.</returns>
    ConversionResult Convert(Stream stream, string? fileName = null);
}
