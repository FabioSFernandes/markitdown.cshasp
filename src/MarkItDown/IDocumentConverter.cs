namespace MarkItDown;

/// <summary>
/// Interface for document converters that convert various file formats to Markdown.
/// </summary>
public interface IDocumentConverter
{
    /// <summary>
    /// Determines whether this converter can handle the given stream.
    /// </summary>
    /// <param name="stream">The input stream to check.</param>
    /// <param name="streamInfo">Information about the stream.</param>
    /// <returns>True if this converter can handle the stream; otherwise, false.</returns>
    bool Accepts(Stream stream, StreamInfo streamInfo);

    /// <summary>
    /// Converts the given stream to Markdown.
    /// </summary>
    /// <param name="stream">The input stream to convert.</param>
    /// <param name="streamInfo">Information about the stream.</param>
    /// <returns>The conversion result containing Markdown text.</returns>
    DocumentConverterResult Convert(Stream stream, StreamInfo streamInfo);
}
