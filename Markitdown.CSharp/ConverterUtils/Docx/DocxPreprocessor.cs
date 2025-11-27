using System.IO.Compression;
using System.Xml.Linq;
using MarkItDown.CSharp.ConverterUtils.Docx.Math;
using System.Linq;

namespace MarkItDown.CSharp.ConverterUtils.Docx;

internal static class DocxPreprocessor
{
    private static readonly string[] ProcessableEntries =
    {
        "word/document.xml",
        "word/footnotes.xml",
        "word/endnotes.xml",
    };

    private static readonly XNamespace WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly OmmlToLatexConverter MathConverter = new();

    public static MemoryStream PreProcess(Stream input)
    {
        var output = new MemoryStream();
        using var inputArchive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        using var outputArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);

        foreach (var entry in inputArchive.Entries)
        {
            var outEntry = outputArchive.CreateEntry(entry.FullName, CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            using var destination = outEntry.Open();

            if (ProcessableEntries.Contains(entry.FullName))
            {
                using var processed = Transform(entryStream);
                processed.CopyTo(destination);
            }
            else
            {
                entryStream.CopyTo(destination);
            }
        }

        output.Seek(0, SeekOrigin.Begin);
        return output;
    }

    private static MemoryStream Transform(Stream xmlStream)
    {
        var document = XDocument.Load(xmlStream);
        var mathNodes = document
            .Descendants()
            .Where(e => e.Name.LocalName is "oMath" or "oMathPara")
            .ToList();

        foreach (var math in mathNodes)
        {
            try
            {
                var latex = MathConverter.Convert(math);
                var textElement = new XElement(WordNamespace + "t", latex);
                var run = new XElement(WordNamespace + "r", textElement);

                if (math.Name.LocalName == "oMathPara")
                {
                    var paragraph = new XElement(WordNamespace + "p", run);
                    math.ReplaceWith(paragraph);
                }
                else
                {
                    math.ReplaceWith(run);
                }
            }
            catch
            {
                // If conversion fails, leave the math as-is
            }
        }

        var memory = new MemoryStream();
        document.Save(memory);
        memory.Seek(0, SeekOrigin.Begin);
        return memory;
    }
}

