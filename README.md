# MarkItDown.NET

A .NET library for converting various document formats to Markdown. This project is a .NET rewrite of Microsoft's [markitdown](https://github.com/microsoft/markitdown) Python library.

## Features

- **Multiple Format Support**: Convert various document formats to Markdown
  - Plain Text (`.txt`, `.text`, `.log`, `.md`, `.markdown`)
  - HTML (`.html`, `.htm`, `.xhtml`)
  - CSV (`.csv`)
  - JSON (`.json`)
- **Extensible Architecture**: Easy to add custom converters
- **Stream-based API**: Convert from files or streams
- **MIME Type Support**: Convert content based on MIME types

## Installation

```bash
dotnet add package MarkItDown
```

## Quick Start

### Basic Usage

```csharp
using MarkItDown;

var markItDown = new MarkItDown();

// Convert a file
var result = markItDown.Convert("document.html");
Console.WriteLine(result.TextContent);

// Convert from a stream
using var stream = File.OpenRead("data.csv");
var csvResult = markItDown.Convert(stream, "data.csv");
Console.WriteLine(csvResult.TextContent);

// Convert by MIME type
using var htmlStream = new MemoryStream(Encoding.UTF8.GetBytes("<h1>Hello</h1>"));
var htmlResult = markItDown.ConvertByMimeType(htmlStream, "text/html");
Console.WriteLine(htmlResult.TextContent);
```

### Working with Results

```csharp
var result = markItDown.Convert("report.html");

// Get the markdown content
string markdown = result.TextContent;

// Get the document title (if available)
string? title = result.Title;

// Get the source type
string? sourceType = result.SourceType;
```

### Check Supported Formats

```csharp
var markItDown = new MarkItDown();

// Check if a file type is supported
bool isSupported = markItDown.IsSupported("document.html"); // true
bool isPdfSupported = markItDown.IsSupported("document.pdf"); // false

// Check if a MIME type is supported
bool isMimeSupported = markItDown.IsMimeTypeSupported("text/csv"); // true
```

### Custom Converters

You can create and register custom converters:

```csharp
public class XmlConverter : DocumentConverterBase
{
    protected override IReadOnlyCollection<string> SupportedExtensions => 
        new[] { ".xml" };
    
    protected override IReadOnlyCollection<string> SupportedMimeTypes => 
        new[] { "application/xml", "text/xml" };

    public override ConversionResult Convert(Stream stream, string? fileName = null)
    {
        // Your conversion logic here
        var content = ReadAllText(stream);
        var markdown = ConvertXmlToMarkdown(content);
        return new ConversionResult(markdown, title: null, sourceType: "application/xml");
    }

    private string ConvertXmlToMarkdown(string xml)
    {
        // Implementation
    }
}

// Register the custom converter
var markItDown = new MarkItDown();
markItDown.RegisterConverter(new XmlConverter());
```

## API Reference

### MarkItDown Class

| Method | Description |
|--------|-------------|
| `Convert(string filePath)` | Converts a file to Markdown |
| `Convert(Stream stream, string? fileName)` | Converts content from a stream |
| `ConvertByMimeType(Stream stream, string mimeType, string? fileName)` | Converts content using MIME type |
| `IsSupported(string filePath)` | Checks if a file type is supported |
| `IsMimeTypeSupported(string mimeType)` | Checks if a MIME type is supported |
| `RegisterConverter(IDocumentConverter converter)` | Registers a custom converter |

### ConversionResult Class

| Property | Type | Description |
|----------|------|-------------|
| `TextContent` | `string` | The converted Markdown content |
| `Title` | `string?` | Optional document title |
| `SourceType` | `string?` | Original content type identifier |

### Built-in Converters

| Converter | Extensions | MIME Types |
|-----------|------------|------------|
| `PlainTextConverter` | `.txt`, `.text`, `.log`, `.md`, `.markdown` | `text/plain`, `text/markdown` |
| `HtmlConverter` | `.html`, `.htm`, `.xhtml` | `text/html`, `application/xhtml+xml` |
| `CsvConverter` | `.csv` | `text/csv`, `application/csv` |
| `JsonConverter` | `.json` | `application/json`, `text/json` |

## Building from Source

```bash
# Clone the repository
git clone https://github.com/FabioSFernandes/markitdown.cshasp.git
cd markitdown.cshasp

# Build the solution
dotnet build

# Run tests
dotnet test
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [Microsoft MarkItDown](https://github.com/microsoft/markitdown) - The original Python implementation
- [HtmlAgilityPack](https://html-agility-pack.net/) - Used for HTML parsing
