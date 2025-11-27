namespace MarkItDown.CSharp.Exceptions;

public sealed class UnsupportedFormatException : MarkItDownException
{
    public UnsupportedFormatException(string message)
        : base(message)
    {
    }
}

