using System.Diagnostics.CodeAnalysis;
using MarkItDown.CSharp;
using MarkItDown.CSharp.Exceptions;
using MarkItDownClient = MarkItDown.CSharp.MarkItDown;

return await MkTool.RunAsync(args);

internal static class MkTool
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || HasHelpFlag(args))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        var source = args[0];
        var destination = args.Length > 1 ? args[1] : null;

        try
        {
            using var markItDown = new MarkItDownClient();
            Console.WriteLine($"[DEBUG] Starting conversion of: {source}");
            var result = await ConvertAsync(markItDown, source).ConfigureAwait(false);
            Console.WriteLine($"[DEBUG] Conversion completed, markdown length: {result.Markdown.Length}");
            destination ??= BuildDefaultOutputPath(source);
            var fullDestination = Path.GetFullPath(destination);
            Console.WriteLine($"[DEBUG] Output destination: {fullDestination}");
            var outputDirectory = Path.GetDirectoryName(fullDestination);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }
            await File.WriteAllTextAsync(fullDestination, result.Markdown).ConfigureAwait(false);
            Console.WriteLine($"[DEBUG] File written successfully");

            Console.WriteLine($"Converted '{source}' -> '{fullDestination}'.");
            if (!string.IsNullOrWhiteSpace(result.Title))
            {
                Console.WriteLine($"Detected title: {result.Title}");
            }

            return 0;
        }
        catch (MissingDependencyException ex)
        {
            Console.Error.WriteLine($"Missing dependency: {ex.Message}");
        }
        catch (UnsupportedFormatException)
        {
            Console.Error.WriteLine("Could not detect a supported document type for the provided input.");
        }
        catch (MarkItDownException ex)
        {
            var errorLog = Path.Combine(Path.GetDirectoryName(source) ?? Directory.GetCurrentDirectory(), "mktool_error.log");
            var errorDetails = $@"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MarkItDownException converting: {source}
Message: {ex.Message}
Exception Type: {ex.GetType().FullName}
";
            
            if (ex is FileConversionException fce)
            {
                errorDetails += $"Failed attempts: {fce.Attempts.Count}\n";
                foreach (var attempt in fce.Attempts)
                {
                    if (attempt.Exception is null)
                    {
                        continue;
                    }
                    errorDetails += $"\n--- {attempt.Converter.GetType().Name} exception ---\n";
                    errorDetails += $"{attempt.Exception}\n";
                }
            }
            errorDetails += "---\n";
            File.AppendAllText(errorLog, errorDetails);
            
            Console.Error.WriteLine("Conversion error:");
            Console.Error.WriteLine(ex.Message.TrimEnd());

            if (ex is FileConversionException fce2)
            {
                foreach (var attempt in fce2.Attempts)
                {
                    if (attempt.Exception is null)
                    {
                        continue;
                    }

                    Console.Error.WriteLine();
                    Console.Error.WriteLine($"--- {attempt.Converter.GetType().Name} exception ---");
                    Console.Error.WriteLine(attempt.Exception);
                }
            }
            Console.Error.WriteLine($"Error details written to: {errorLog}");
        }
        catch (Exception ex)
        {
            var errorLog = Path.Combine(Path.GetDirectoryName(source) ?? Directory.GetCurrentDirectory(), "mktool_error.log");
            var errorDetails = $@"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Error converting: {source}
Exception Type: {ex.GetType().FullName}
Message: {ex.Message}
Stack Trace:
{ex.StackTrace}
---
";
            File.AppendAllText(errorLog, errorDetails);
            
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            Console.Error.WriteLine($"Exception type: {ex.GetType().FullName}");
            Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
            Console.Error.WriteLine($"Error details written to: {errorLog}");
        }

        return 1;
    }

    private static async Task<DocumentConverterResult> ConvertAsync(MarkItDownClient markItDown, string source)
    {
        if (File.Exists(source) || LooksLikeLocalPath(source))
        {
            var localPath = Path.GetFullPath(source);
            if (!File.Exists(localPath))
            {
                throw new FileNotFoundException($"File '{localPath}' was not found.", localPath);
            }

            return await markItDown.ConvertLocalAsync(localPath).ConfigureAwait(false);
        }

        if (TryGetSupportedUri(source, out var uri))
        {
            if (string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            {
                var localPath = uri.LocalPath;
                if (!File.Exists(localPath))
                {
                    throw new FileNotFoundException($"File '{localPath}' was not found.", localPath);
                }

                return await markItDown.ConvertLocalAsync(localPath).ConfigureAwait(false);
            }

            return await markItDown.ConvertUriAsync(uri.ToString()).ConfigureAwait(false);
        }

        throw new ArgumentException("Source must be a valid file path or a supported absolute URI.", nameof(source));
    }

    private static string BuildDefaultOutputPath(string source)
    {
        if (File.Exists(source))
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(source)) ?? Directory.GetCurrentDirectory();
            var stem = Path.GetFileNameWithoutExtension(source);
            stem = SanitizeFileName(stem);
            return Path.Combine(directory, $"{stem}.md");
        }

        if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            if (string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase) && File.Exists(uri.LocalPath))
            {
                var directory = Path.GetDirectoryName(uri.LocalPath) ?? Directory.GetCurrentDirectory();
                var stem = SanitizeFileName(Path.GetFileNameWithoutExtension(uri.LocalPath));
                return Path.Combine(directory, $"{stem}.md");
            }

            var safeHost = SanitizeFileName(string.IsNullOrWhiteSpace(uri.Host) ? "output" : uri.Host);
            var slug = SanitizeFileName(uri.AbsolutePath.Trim('/').Replace('/', '_'));
            var fileName = string.IsNullOrWhiteSpace(slug) ? safeHost : $"{safeHost}_{slug}";
            return Path.Combine(Directory.GetCurrentDirectory(), $"{fileName}.md");
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "output.md");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  mktool <source-path-or-uri> [output-path]");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  mktool C:\\docs\\report.docx");
        Console.WriteLine("  mktool https://example.com/file.pdf output.md");
    }

    private static bool HasHelpFlag(IEnumerable<string> args) =>
        args.Any(arg => arg is "-h" or "--help" or "/?");

    private static bool LooksLikeLocalPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.StartsWith(@"\\") || value.StartsWith("//"))
        {
            return true;
        }

        return value.Length > 1 && value[1] == ':' && char.IsLetter(value[0]);
    }

    private static bool TryGetSupportedUri(string value, [NotNullWhen(true)] out Uri? uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var candidate) && IsSupportedScheme(candidate.Scheme))
        {
            uri = candidate;
            return true;
        }

        uri = null;
        return false;
    }

    private static bool IsSupportedScheme(string scheme) =>
        scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
        scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
        scheme.Equals(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase) ||
        scheme.Equals("data", StringComparison.OrdinalIgnoreCase);

    private static string SanitizeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "output";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var buffer = new char[name.Length];
        var index = 0;
        foreach (var ch in name)
        {
            buffer[index++] = Array.IndexOf(invalid, ch) >= 0 ? '_' : ch;
        }

        return new string(buffer, 0, index).Trim('_');
    }
}
