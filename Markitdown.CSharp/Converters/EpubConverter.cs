using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Linq;

namespace MarkItDown.CSharp.Converters;

public sealed class EpubConverter : DocumentConverter
{
    private static readonly string[] AcceptedMimePrefixes =
    {
        "application/epub",
        "application/epub+zip",
        "application/x-epub+zip",
    };

    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".epub",
    };

    private static readonly Dictionary<string, string> MimeTypeMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html",
        [".xhtml"] = "application/xhtml+xml",
    };

    private readonly HtmlConverter _htmlConverter = new();

    public override bool Accepts(Stream fileStream, StreamInfo streamInfo, ConversionOptions options)
    {
        var extension = streamInfo.Extension ?? string.Empty;
        if (AcceptedExtensions.Contains(extension))
        {
            return true;
        }

        var mimetype = streamInfo.MimeType ?? string.Empty;
        return AcceptedMimePrefixes.Any(prefix => mimetype.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public override async Task<DocumentConverterResult> ConvertAsync(
        Stream fileStream,
        StreamInfo streamInfo,
        ConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: true);
        var opfPath = GetOpfPath(archive);
        var opfDocument = LoadXml(archive, opfPath);

        var metadata = ExtractMetadata(opfDocument);
        var manifest = BuildManifest(opfDocument);
        var spine = BuildSpine(opfDocument, manifest, Path.GetDirectoryName(opfPath) ?? string.Empty);

        var builder = new StringBuilder();
        foreach (var item in metadata)
        {
            if (!string.IsNullOrWhiteSpace(item.Value))
            {
                builder.AppendLine($"**{item.Key}:** {item.Value}");
            }
        }

        builder.AppendLine();

        foreach (var file in spine)
        {
            var entry = archive.GetEntry(file);
            if (entry is null)
            {
                continue;
            }

            await using var entryStream = entry.Open();
            using var memory = new MemoryStream();
            await entryStream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
            memory.Seek(0, SeekOrigin.Begin);

            var extension = Path.GetExtension(file);
            var mimetype = MimeTypeMapping.GetValueOrDefault(extension, "text/html");

            var result = _htmlConverter.ConvertFromString(Encoding.UTF8.GetString(memory.ToArray()));
            builder.AppendLine(result.Markdown.Trim());
            builder.AppendLine();
        }

        metadata.TryGetValue("Title", out var title);
        return new DocumentConverterResult(builder.ToString().Trim(), title);
    }

    private static string GetOpfPath(ZipArchive archive)
    {
        var containerEntry = archive.GetEntry("META-INF/container.xml")
            ?? throw new InvalidOperationException("Invalid EPUB: missing META-INF/container.xml");

        using var stream = containerEntry.Open();
        var doc = new XmlDocument();
        doc.Load(stream);

        var rootfile = doc.GetElementsByTagName("rootfile")[0] as XmlElement
            ?? throw new InvalidOperationException("Invalid EPUB: rootfile element missing.");

        var path = rootfile.GetAttribute("full-path");
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Invalid EPUB: full-path attribute missing.");
        }

        return path;
    }

    private static XmlDocument LoadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path) ?? throw new InvalidOperationException($"EPUB missing {path}");
        using var stream = entry.Open();
        var doc = new XmlDocument();
        doc.Load(stream);
        return doc;
    }

    private static Dictionary<string, string?> ExtractMetadata(XmlDocument opf)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Title"] = GetSingle(opf, "dc:title"),
            ["Authors"] = string.Join(", ", GetMany(opf, "dc:creator")),
            ["Language"] = GetSingle(opf, "dc:language"),
            ["Publisher"] = GetSingle(opf, "dc:publisher"),
            ["Date"] = GetSingle(opf, "dc:date"),
            ["Description"] = GetSingle(opf, "dc:description"),
            ["Identifier"] = GetSingle(opf, "dc:identifier"),
        };

        return data;
    }

    private static Dictionary<string, string> BuildManifest(XmlDocument opf)
    {
        var manifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var items = opf.GetElementsByTagName("item");
        foreach (XmlElement item in items)
        {
            manifest[item.GetAttribute("id")] = item.GetAttribute("href");
        }

        return manifest;
    }

    private static IEnumerable<string> BuildSpine(XmlDocument opf, Dictionary<string, string> manifest, string basePath)
    {
        var paths = new List<string>();
        var itemRefs = opf.GetElementsByTagName("itemref");
        foreach (XmlElement itemRef in itemRefs)
        {
            var idRef = itemRef.GetAttribute("idref");
            if (manifest.TryGetValue(idRef, out var relativePath))
            {
                paths.Add(Path.Combine(basePath, relativePath).Replace('\\', '/'));
            }
        }

        return paths;
    }

    private static string? GetSingle(XmlDocument doc, string tag) =>
        doc.GetElementsByTagName(tag).Cast<XmlElement>().FirstOrDefault()?.InnerText?.Trim();

    private static IEnumerable<string> GetMany(XmlDocument doc, string tag) =>
        doc.GetElementsByTagName(tag).Cast<XmlElement>().Select(e => e.InnerText.Trim()).Where(s => s.Length > 0);
}

