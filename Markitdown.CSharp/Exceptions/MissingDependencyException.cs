namespace MarkItDown.CSharp.Exceptions;

public sealed class MissingDependencyException : MarkItDownException
{
    public MissingDependencyException(string message)
        : base(message)
    {
    }

    public MissingDependencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

