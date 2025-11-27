namespace MarkItDown.Converters;

/// <summary>
/// Base class for document converters providing common functionality.
/// </summary>
public abstract class DocumentConverterBase : IDocumentConverter
{
    /// <summary>
    /// Gets the file extensions supported by this converter.
    /// </summary>
    protected abstract IReadOnlyCollection<string> SupportedExtensions { get; }

    /// <summary>
    /// Gets the MIME types supported by this converter.
    /// </summary>
    protected abstract IReadOnlyCollection<string> SupportedMimeTypes { get; }

    /// <inheritdoc />
    public virtual bool CanConvert(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }

    /// <inheritdoc />
    public virtual bool CanConvertMimeType(string mimeType)
    {
        if (string.IsNullOrEmpty(mimeType))
            return false;

        return SupportedMimeTypes.Contains(mimeType.ToLowerInvariant());
    }

    /// <inheritdoc />
    public virtual ConversionResult Convert(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found.", filePath);

        using var stream = File.OpenRead(filePath);
        return Convert(stream, Path.GetFileName(filePath));
    }

    /// <inheritdoc />
    public abstract ConversionResult Convert(Stream stream, string? fileName = null);

    /// <summary>
    /// Reads all text from a stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>The text content of the stream.</returns>
    protected static string ReadAllText(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Gets the file extension from a file name.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The file extension in lowercase, or an empty string if not available.</returns>
    protected static string GetExtension(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return string.Empty;

        return Path.GetExtension(fileName).ToLowerInvariant();
    }
}
