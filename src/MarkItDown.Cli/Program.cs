using MarkItDown;

return MarkItDownCli.Run(args);

internal static class MarkItDownCli
{
    public static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
        {
            PrintHelp();
            return 0;
        }

        if (args[0] == "--version" || args[0] == "-v")
        {
            PrintVersion();
            return 0;
        }

        var inputPath = args[0];
        string? outputPath = null;

        // Parse arguments
        for (int i = 1; i < args.Length; i++)
        {
            if ((args[i] == "-o" || args[i] == "--output") && i + 1 < args.Length)
            {
                outputPath = args[i + 1];
                i++;
            }
        }

        // Check for piped input
        if (inputPath == "-")
        {
            return ConvertFromStdin(outputPath);
        }

        // Convert file
        return ConvertFile(inputPath, outputPath);
    }

    private static int ConvertFile(string inputPath, string? outputPath)
    {
        try
        {
            if (!File.Exists(inputPath))
            {
                Console.Error.WriteLine($"Error: File not found: {inputPath}");
                return 1;
            }

            var markItDown = new MarkItDown.MarkItDown();
            var result = markItDown.Convert(inputPath);

            if (outputPath is not null)
            {
                File.WriteAllText(outputPath, result.TextContent);
                Console.WriteLine($"Converted {inputPath} to {outputPath}");
            }
            else
            {
                Console.WriteLine(result.TextContent);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int ConvertFromStdin(string? outputPath)
    {
        try
        {
            using var stdin = Console.OpenStandardInput();
            using var memoryStream = new MemoryStream();
            stdin.CopyTo(memoryStream);
            memoryStream.Position = 0;

            var markItDown = new MarkItDown.MarkItDown();
            var result = markItDown.ConvertStream(memoryStream);

            if (outputPath is not null)
            {
                File.WriteAllText(outputPath, result.TextContent);
            }
            else
            {
                Console.WriteLine(result.TextContent);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            MarkItDown - Convert files to Markdown

            Usage:
              markitdown <input-file> [options]
              markitdown - [options]          (read from stdin)
              cat file | markitdown -         (pipe content)

            Options:
              -o, --output <file>    Write output to file instead of stdout
              -h, --help             Show this help message
              -v, --version          Show version information

            Examples:
              markitdown document.html
              markitdown document.html -o document.md
              cat page.html | markitdown -
            """);
    }

    private static void PrintVersion()
    {
        var assembly = typeof(MarkItDownCli).Assembly;
        var version = assembly.GetName().Version?.ToString() ?? "1.0.0";
        Console.WriteLine($"MarkItDown CLI version {version}");
    }
}
