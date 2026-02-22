using System.Text;
using MarkItDown.CSharp.Interfaces;

namespace MarkItDown.CSharp.Exceptions;

public sealed class FileConversionException : MarkItDownException
{
    public FileConversionException(string message)
        : base(message)
    {
    }

    public FileConversionException(
        string? message = null,
        IReadOnlyList<FailedConversionAttempt>? attempts = null)
        : base(BuildMessage(message, attempts))
    {
        Attempts = attempts ?? Array.Empty<FailedConversionAttempt>();
    }

    public IReadOnlyList<FailedConversionAttempt> Attempts { get; } = Array.Empty<FailedConversionAttempt>();

    private static string BuildMessage(string? message, IReadOnlyList<FailedConversionAttempt>? attempts)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            return message!;
        }

        if (attempts is null || attempts.Count == 0)
        {
            return "File conversion failed.";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"File conversion failed after {attempts.Count} attempts:");
        foreach (var attempt in attempts)
        {
            var converterName = attempt.Converter?.GetType().Name ?? "UnknownConverter";
            if (attempt.Exception is null)
            {
                builder.AppendLine($" - {converterName} provided no exception information.");
            }
            else
            {
                builder.AppendLine(
                    $" - {converterName} threw {attempt.Exception.GetType().Name}: {attempt.Exception.Message}");
            }
        }

        return builder.ToString();
    }
}

public sealed class FailedConversionAttempt
{
    public FailedConversionAttempt(IDocumentConverter converter, Exception? exception)
    {
        Converter = converter;
        Exception = exception;
    }

    public IDocumentConverter Converter { get; }

    public Exception? Exception { get; }
}

