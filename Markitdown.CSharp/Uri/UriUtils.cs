using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace MarkItDown.CSharp.Utilities;

public static class UriUtils
{
    public static (string? NetLoc, string Path) FileUriToPath(string fileUri)
    {
        if (!fileUri.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Not a file URI", nameof(fileUri));
        }

        var uri = new Uri(fileUri);
        var netLoc = string.IsNullOrWhiteSpace(uri.Host) ? null : uri.Host;
        var localPath = uri.LocalPath;
        return (netLoc, Path.GetFullPath(Uri.UnescapeDataString(localPath)));
    }

    public static (string? MimeType, Dictionary<string, string> Attributes, byte[] Content) ParseDataUri(string uri)
    {
        if (!uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Not a data URI", nameof(uri));
        }

        var commaIndex = uri.IndexOf(',');
        if (commaIndex < 0)
        {
            throw new FormatException("Malformed data URI, missing ',' separator.");
        }

        var meta = uri.Substring(5, commaIndex - 5);
        var dataSegment = uri[(commaIndex + 1)..];
        var parts = meta.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var isBase64 = parts.Count > 0 && parts[^1].Equals("base64", StringComparison.OrdinalIgnoreCase);
        if (isBase64)
        {
            parts.RemoveAt(parts.Count - 1);
        }

        string? mimeType = null;
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (parts.Count > 0 && parts[0].Contains('/'))
        {
            mimeType = parts[0];
            parts.RemoveAt(0);
        }

        foreach (var part in parts)
        {
            var equalsIndex = part.IndexOf('=');
            if (equalsIndex < 0)
            {
                attributes[part] = string.Empty;
            }
            else
            {
                attributes[part[..equalsIndex]] = part[(equalsIndex + 1)..];
            }
        }

        var content = isBase64
            ? Convert.FromBase64String(dataSegment)
            : Encoding.UTF8.GetBytes(Uri.UnescapeDataString(dataSegment));

        return (mimeType, attributes, content);
    }
}

