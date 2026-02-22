using MarkItDown.CSharp;

namespace MarkItDown.CSharp.Interfaces;

/// <summary>
/// Contract for document converters. Implement this interface (or inherit <see cref="DocumentConverter"/>)
/// and optionally declare <see cref="SupportedExtensions"/> so the engine routes files by extension automatically.
/// </summary>
public interface IDocumentConverter
{
    /// <summary>
    /// Extensions this converter supports (e.g. ".axd"). When non-null/non-empty, the engine
    /// registers extension → converter and tries these converters first for matching files.
    /// </summary>
    IReadOnlyList<string>? SupportedExtensions { get; }

    /// <summary>
    /// Whether this converter accepts the given stream and stream info.
    /// </summary>
    bool Accepts(Stream fileStream, StreamInfo streamInfo, ConversionOptions options);

    /// <summary>
    /// Converts the stream to markdown.
    /// </summary>
    Task<DocumentConverterResult> ConvertAsync(
        Stream fileStream,
        StreamInfo streamInfo,
        ConversionOptions options,
        CancellationToken cancellationToken = default);
}
