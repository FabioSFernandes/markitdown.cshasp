using System.Text;

namespace MarkItDown.Converters;

/// <summary>
/// Converter for plain text files to Markdown.
/// </summary>
public sealed class PlainTextConverter : IDocumentConverter
{
    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/plain",
        "text/markdown",
        "text/x-markdown",
        "application/json",
        "application/xml",
        "text/xml",
        "text/csv",
        "text/tab-separated-values",
        "application/x-yaml",
        "text/yaml",
        "text/x-python",
        "text/x-java",
        "text/x-csharp",
        "text/javascript",
        "application/javascript",
        "text/x-typescript"
    };

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".md",
        ".markdown",
        ".json",
        ".xml",
        ".csv",
        ".tsv",
        ".yaml",
        ".yml",
        ".py",
        ".java",
        ".cs",
        ".js",
        ".ts",
        ".jsx",
        ".tsx",
        ".c",
        ".cpp",
        ".h",
        ".hpp",
        ".go",
        ".rs",
        ".rb",
        ".php",
        ".swift",
        ".kt",
        ".scala",
        ".sh",
        ".bash",
        ".zsh",
        ".ps1",
        ".bat",
        ".cmd",
        ".sql",
        ".r",
        ".lua",
        ".pl",
        ".pm",
        ".ini",
        ".cfg",
        ".conf",
        ".properties",
        ".env",
        ".gitignore",
        ".dockerignore",
        ".editorconfig",
        ".log"
    };

    /// <inheritdoc />
    public bool Accepts(Stream stream, StreamInfo streamInfo)
    {
        // Check MIME type
        if (streamInfo.MimeType is not null)
        {
            if (SupportedMimeTypes.Contains(streamInfo.MimeType))
            {
                return true;
            }

            // Accept any text/* MIME type
            if (streamInfo.MimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Check extension
        if (streamInfo.Extension is not null && SupportedExtensions.Contains(streamInfo.Extension))
        {
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public DocumentConverterResult Convert(Stream stream, StreamInfo streamInfo)
    {
        // Determine encoding
        var encoding = GetEncoding(streamInfo.Charset);

        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var content = reader.ReadToEnd();

        // For code files, wrap in code blocks
        var extension = streamInfo.Extension?.TrimStart('.').ToLowerInvariant();
        if (IsCodeFile(extension))
        {
            var language = GetLanguageForExtension(extension);
            content = $"```{language}\n{content}\n```";
        }

        return new DocumentConverterResult
        {
            TextContent = content,
            Title = streamInfo.Filename
        };
    }

    private static Encoding GetEncoding(string? charset)
    {
        if (string.IsNullOrEmpty(charset))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(charset);
        }
        catch
        {
            return Encoding.UTF8;
        }
    }

    private static bool IsCodeFile(string? extension)
    {
        if (extension is null) return false;

        return extension switch
        {
            "py" or "java" or "cs" or "js" or "ts" or "jsx" or "tsx" or
            "c" or "cpp" or "h" or "hpp" or "go" or "rs" or "rb" or
            "php" or "swift" or "kt" or "scala" or "sh" or "bash" or
            "zsh" or "ps1" or "bat" or "cmd" or "sql" or "r" or "lua" or
            "pl" or "pm" => true,
            _ => false
        };
    }

    private static string GetLanguageForExtension(string? extension)
    {
        if (extension is null) return "";

        return extension switch
        {
            "py" => "python",
            "java" => "java",
            "cs" => "csharp",
            "js" => "javascript",
            "ts" => "typescript",
            "jsx" => "jsx",
            "tsx" => "tsx",
            "c" => "c",
            "cpp" or "hpp" => "cpp",
            "h" => "c",
            "go" => "go",
            "rs" => "rust",
            "rb" => "ruby",
            "php" => "php",
            "swift" => "swift",
            "kt" => "kotlin",
            "scala" => "scala",
            "sh" or "bash" or "zsh" => "bash",
            "ps1" => "powershell",
            "bat" or "cmd" => "batch",
            "sql" => "sql",
            "r" => "r",
            "lua" => "lua",
            "pl" or "pm" => "perl",
            _ => extension
        };
    }
}
