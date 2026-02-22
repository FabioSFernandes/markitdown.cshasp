using MarkItDown.CSharp;

namespace MarkItDown.CSharp.Interfaces;

/// <summary>
/// Engine that can convert a stream to markdown using the registered converters.
/// Implemented by <see cref="MarkItDown"/>. Use this interface in converters that need to
/// recursively convert inner content (e.g. entries inside a ZIP or EPUB).
/// </summary>
public interface IConversionEngine
{
    /// <summary>
    /// Converts the stream to markdown using the current converter set and options.
    /// </summary>
    Task<DocumentConverterResult> ConvertStreamAsync(
        Stream stream,
        StreamInfo? streamInfo = null,
        ConversionOptions? options = null,
        CancellationToken cancellationToken = default);
}
