using MarkItDown.CSharp;

namespace MarkItDown.CSharp.Interfaces;

/// <summary>
/// Allows registering document converters. Implemented by <see cref="MarkItDown"/> so that
/// plugins and other components can add converters without depending on the concrete engine type.
/// </summary>
public interface IConverterRegistry
{
    /// <summary>
    /// Registers a converter. Lower priority values are tried first. When the converter
    /// declares <see cref="IDocumentConverter.SupportedExtensions"/>, the engine will try
    /// it first for files with those extensions.
    /// </summary>
    void RegisterConverter(IDocumentConverter converter, double priority);
}
