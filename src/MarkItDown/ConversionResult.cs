namespace MarkItDown;

/// <summary>
/// Represents the result of a document conversion operation.
/// </summary>
public sealed class ConversionResult
{
    /// <summary>
    /// Gets the converted markdown content.
    /// </summary>
    public string TextContent { get; }

    /// <summary>
    /// Gets the title of the document, if available.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    /// Gets the original file extension or content type.
    /// </summary>
    public string? SourceType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversionResult"/> class.
    /// </summary>
    /// <param name="textContent">The converted markdown content.</param>
    /// <param name="title">Optional title of the document.</param>
    /// <param name="sourceType">Optional source type identifier.</param>
    public ConversionResult(string textContent, string? title = null, string? sourceType = null)
    {
        TextContent = textContent ?? throw new ArgumentNullException(nameof(textContent));
        Title = title;
        SourceType = sourceType;
    }
}
