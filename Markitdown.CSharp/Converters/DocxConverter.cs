using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using MarkItDown.CSharp.ConverterUtils.Docx;
using System.Linq;

namespace MarkItDown.CSharp.Converters;

public sealed class DocxConverter : DocumentConverter
{
    private static readonly string[] AcceptedMimePrefixes =
    {
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    };

    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".docx",
    };

    // Word can save with either ECMA or ISO OOXML namespace; we support both.
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace WIso = "http://purl.oclc.org/ooxml/wordprocessingml/main";

    public override bool Accepts(Stream fileStream, StreamInfo streamInfo, ConversionOptions options)
    {
        var extension = streamInfo.Extension ?? string.Empty;
        var mimetype = streamInfo.MimeType ?? string.Empty;
        var acceptsByExtension = AcceptedExtensions.Contains(extension);
        var acceptsByMime = AcceptedMimePrefixes.Any(prefix => mimetype.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return acceptsByExtension || acceptsByMime;
    }

    public override Task<DocumentConverterResult> ConvertAsync(
        Stream fileStream,
        StreamInfo streamInfo,
        ConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        if (fileStream.CanSeek)
            fileStream.Seek(0, SeekOrigin.Begin);

        // SDK and ZipArchive require a seekable stream. Some DOCX open in Word but fail with
        // strict ZipArchive ("End of Central Directory not found"); try Open XML SDK first.
        Stream streamToUse = fileStream;
        MemoryStream? streamCopy = null;
        if (!fileStream.CanSeek)
        {
            streamCopy = new MemoryStream();
            fileStream.CopyTo(streamCopy);
            streamCopy.Position = 0;
            streamToUse = streamCopy;
        }

        try
        {
            // 1) Try Open XML SDK first (often more tolerant of ZIP variants that Word accepts)
            var result = TryOpenWithSdk(streamToUse);
            if (result is not null)
                return Task.FromResult(result);

            if (streamToUse.CanSeek)
                streamToUse.Seek(0, SeekOrigin.Begin);

            // 2) Fallback: preprocess (math) then ZipArchive
            MemoryStream? processed = null;
            try
            {
                processed = DocxPreprocessor.PreProcess(streamToUse);
                streamToUse = processed;
            }
            catch
            {
                if (streamToUse.CanSeek)
                    streamToUse.Seek(0, SeekOrigin.Begin);
            }

            try
            {
                using var archive = new ZipArchive(streamToUse, ZipArchiveMode.Read, leaveOpen: true);
                var documentEntry = archive.GetEntry("word/document.xml");
                if (documentEntry is null)
                    return Task.FromResult(new DocumentConverterResult(string.Empty));

                using var docStream = documentEntry.Open();
                return Task.FromResult(ExtractMarkdownFromDocumentXml(docStream));
            }
            finally
            {
                processed?.Dispose();
            }
        }
        finally
        {
            streamCopy?.Dispose();
        }
    }

    /// <summary>
    /// Tries to open the DOCX with DocumentFormat.OpenXml (WordprocessingDocument).
    /// Returns the conversion result if successful; null if the stream is not valid for the SDK (e.g. ZIP format the SDK cannot read).
    /// </summary>
    private static DocumentConverterResult? TryOpenWithSdk(Stream stream)
    {
        if (!stream.CanSeek)
            return null;

        try
        {
            using var wordDoc = WordprocessingDocument.Open(stream, false);
            var mainPart = wordDoc.MainDocumentPart;
            if (mainPart is null)
                return new DocumentConverterResult(string.Empty);

            using var docStream = mainPart.GetStream();
            return ExtractMarkdownFromDocumentXml(docStream);
        }
        catch (IOException)
        {
            return null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static DocumentConverterResult ExtractMarkdownFromDocumentXml(Stream docStream)
    {
        var doc = XDocument.Load(docStream);
        var wordNs = doc.Root?.Name.Namespace ?? W;
        var body = doc.Root?.Element(wordNs + "body")
            ?? doc.Root?.Element(W + "body")
            ?? doc.Root?.Element(WIso + "body");
        if (body is null)
            return new DocumentConverterResult(string.Empty);

        var blocks = new List<string>();
        foreach (var node in body.Elements())
        {
            var name = node.Name.LocalName;
            if (name == "p")
            {
                var parts = new List<string>();
                foreach (var run in node.Elements().Where(e => e.Name.LocalName == "r"))
                {
                    var runText = string.Join("", run.Elements().Where(e => e.Name.LocalName == "t").Select(t => t.Value ?? ""));
                    if (runText.Length > 0)
                        parts.Add(runText);
                    if (run.Elements().Any(e => e.Name.LocalName == "br"))
                        parts.Add("\n");
                }
                var text = string.Join("", parts).Replace("\n\n\n", "\n\n").Trim();
                if (text.Length > 0)
                    blocks.Add(text);
            }
            else if (name == "tbl")
            {
                var tableMd = ConvertTableToMarkdown(node);
                if (tableMd.Length > 0)
                    blocks.Add(tableMd);
            }
        }

        var markdown = string.Join("\n\n", blocks);
        return new DocumentConverterResult(markdown.Trim());
    }

    private static string ConvertTableToMarkdown(XElement tbl)
    {
        var rows = tbl.Elements().Where(e => e.Name.LocalName == "tr").ToList();
        if (rows.Count == 0)
            return string.Empty;

        var cellTexts = rows.Select(tr =>
            tr.Elements().Where(e => e.Name.LocalName == "tc")
                .Select(tc => string.Join(" ", tc.Descendants().Where(e => e.Name.LocalName == "t").Select(t => t.Value ?? "")).Trim())
                .ToList()
        ).ToList();

        if (cellTexts.Any(c => c.Count == 0))
            return string.Empty;
        var colCount = cellTexts[0].Count;
        if (cellTexts.Any(c => c.Count != colCount))
            return string.Empty;

        var header = "| " + string.Join(" | ", cellTexts[0]) + " |";
        var separator = "| " + string.Join(" | ", Enumerable.Range(0, colCount).Select(_ => "---")) + " |";
        var bodyRows = cellTexts.Skip(1).Select(r => "| " + string.Join(" | ", r) + " |");
        return header + "\n" + separator + "\n" + string.Join("\n", bodyRows);
    }
}
