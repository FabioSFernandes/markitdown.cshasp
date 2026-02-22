using MarkItDown.CSharp;

namespace MarkItDown.CSharp.Interfaces;

public interface IMarkItDownPlugin
{
    void RegisterConverters(IConverterRegistry registry, ConversionOptions options);
}

