namespace MarkItDown.CSharp.Interfaces;

public interface IMarkItDownPlugin
{
    void RegisterConverters(MarkItDown markItDown, ConversionOptions options);
}

