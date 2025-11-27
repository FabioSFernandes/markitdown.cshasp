namespace MarkItDown;

/// <summary>
/// Represents the result of a document conversion to Markdown.
/// </summary>
public sealed class DocumentConverterResult
{
    /// <summary>
    /// Gets or sets the title of the document.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the converted Markdown text content.
    /// </summary>
    public required string TextContent { get; set; }
}
