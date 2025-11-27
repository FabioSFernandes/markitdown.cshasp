using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace MarkItDown.CSharp.Converters.Utilities;

internal static class ExifToolUtility
{
    private static readonly ConcurrentDictionary<string, bool> VerifiedPaths = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<IReadOnlyDictionary<string, string>> ReadMetadataAsync(
        Stream stream,
        string? exifToolPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(exifToolPath))
        {
            return new Dictionary<string, string>();
        }

        await EnsureVersionAsync(exifToolPath!, cancellationToken).ConfigureAwait(false);

        var startInfo = new ProcessStartInfo
        {
            FileName = exifToolPath,
            Arguments = "-json -",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start exiftool.");
        var position = stream.CanSeek ? stream.Position : (long?)null;

        await using (var input = process.StandardInput.BaseStream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            await stream.CopyToAsync(input, cancellationToken).ConfigureAwait(false);
        }
        if (position.HasValue)
        {
            stream.Seek(position.Value, SeekOrigin.Begin);
        }

        var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(output))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            var json = JsonDocument.Parse(output);
            if (json.RootElement.ValueKind == JsonValueKind.Array && json.RootElement.GetArrayLength() > 0)
            {
                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in json.RootElement[0].EnumerateObject())
                {
                    result[property.Name] = property.Value.ToString();
                }

                return result;
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return new Dictionary<string, string>();
    }

    private static async Task EnsureVersionAsync(string exifToolPath, CancellationToken cancellationToken)
    {
        if (VerifiedPaths.ContainsKey(exifToolPath))
        {
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = exifToolPath,
            Arguments = "-ver",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start exiftool.");
        var versionString = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (!Version.TryParse(versionString.Trim(), out var version) || version < new Version(12, 24))
        {
            throw new InvalidOperationException(
                $"ExifTool version {versionString.Trim()} is unsupported. Please upgrade to 12.24 or later.");
        }

        VerifiedPaths[exifToolPath] = true;
    }
}

