namespace MarkItDown.CSharp.Exceptions;

public class MarkItDownException : Exception
{
    public MarkItDownException()
    {
    }

    public MarkItDownException(string message)
        : base(message)
    {
    }

    public MarkItDownException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

