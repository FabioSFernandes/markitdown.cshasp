namespace MarkItDown.Converters;

/// <summary>
/// Converter for plain text files.
/// </summary>
public sealed class PlainTextConverter : DocumentConverterBase
{
    /// <inheritdoc />
    protected override IReadOnlyCollection<string> SupportedExtensions { get; } =
        new[] { ".txt", ".text", ".log", ".md", ".markdown" };

    /// <inheritdoc />
    protected override IReadOnlyCollection<string> SupportedMimeTypes { get; } =
        new[] { "text/plain", "text/markdown" };

    /// <inheritdoc />
    public override ConversionResult Convert(Stream stream, string? fileName = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var content = ReadAllText(stream);
        var title = string.IsNullOrEmpty(fileName) ? null : Path.GetFileNameWithoutExtension(fileName);

        return new ConversionResult(content, title, "text/plain");
    }
}
