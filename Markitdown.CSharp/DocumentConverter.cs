using MarkItDown.CSharp.Interfaces;

namespace MarkItDown.CSharp;

public abstract class DocumentConverter : IDocumentConverter
{
    /// <inheritdoc />
    public virtual IReadOnlyList<string>? SupportedExtensions => null;

    /// <inheritdoc />
    public abstract bool Accepts(Stream fileStream, StreamInfo streamInfo, ConversionOptions options);

    /// <inheritdoc />
    public abstract Task<DocumentConverterResult> ConvertAsync(
        Stream fileStream,
        StreamInfo streamInfo,
        ConversionOptions options,
        CancellationToken cancellationToken = default);
}

public sealed class DocumentConverterResult
{
    public DocumentConverterResult(string markdown, string? title = null)
    {
        Markdown = markdown;
        Title = title;
    }

    public string Markdown { get; set; }

    public string? Title { get; }

    public string TextContent
    {
        get => Markdown;
        set => Markdown = value;
    }

    public override string ToString() => Markdown;
}

