namespace MarkItDown.Exceptions;

/// <summary>
/// Base exception for MarkItDown related errors.
/// </summary>
public class MarkItDownException : Exception
{
    public MarkItDownException()
    {
    }

    public MarkItDownException(string message) : base(message)
    {
    }

    public MarkItDownException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a file format is not supported.
/// </summary>
public class UnsupportedFormatException : MarkItDownException
{
    public UnsupportedFormatException()
    {
    }

    public UnsupportedFormatException(string message) : base(message)
    {
    }

    public UnsupportedFormatException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when file conversion fails.
/// </summary>
public class FileConversionException : MarkItDownException
{
    /// <summary>
    /// Gets the list of failed conversion attempts.
    /// </summary>
    public IReadOnlyList<FailedConversionAttempt> Attempts { get; }

    public FileConversionException(IEnumerable<FailedConversionAttempt> attempts)
        : base("File conversion failed.")
    {
        Attempts = attempts.ToList().AsReadOnly();
    }

    public FileConversionException(string message, IEnumerable<FailedConversionAttempt> attempts)
        : base(message)
    {
        Attempts = attempts.ToList().AsReadOnly();
    }
}

/// <summary>
/// Represents a failed conversion attempt by a specific converter.
/// </summary>
public sealed class FailedConversionAttempt
{
    /// <summary>
    /// Gets the converter that failed.
    /// </summary>
    public required IDocumentConverter Converter { get; init; }

    /// <summary>
    /// Gets the exception that caused the failure.
    /// </summary>
    public required Exception Exception { get; init; }
}
