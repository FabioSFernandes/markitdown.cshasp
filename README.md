# MarkItDown.NET

A .NET rewrite of Microsoft's [MarkItDown](https://github.com/microsoft/markitdown) Python tool - a lightweight utility for converting various files to Markdown for use with LLMs and related text analysis pipelines.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Overview

MarkItDown.NET converts common file types to Markdown, preserving important document structure and content (headings, lists, tables, links, etc.). While the output is human-readable, it is primarily designed for text analysis tools and LLM consumption.

## Supported Formats

Currently supported:
- **HTML** - Full HTML to Markdown conversion including headings, lists, tables, links, images, code blocks
- **Plain Text** - Direct text passthrough with optional code block wrapping for source code files
- **Source Code** - Python, C#, JavaScript, TypeScript, Java, Go, Rust, Ruby, PHP, and many more (wrapped in Markdown code blocks)

Coming soon:
- PDF
- Word (DOCX)
- Excel (XLSX)
- PowerPoint (PPTX)
- Images (EXIF metadata)
- CSV, JSON, XML
- And more!

## Installation

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later

### From Source
```bash
git clone https://github.com/FabioSFernandes/markitdown.cshasp.git
cd markitdown.cshasp
dotnet build
```

## Usage

### Command-Line Interface

Convert a file:
```bash
dotnet run --project src/MarkItDown.Cli -- document.html
```

Convert and save to file:
```bash
dotnet run --project src/MarkItDown.Cli -- document.html -o document.md
```

Pipe content:
```bash
cat page.html | dotnet run --project src/MarkItDown.Cli -- -
```

Show help:
```bash
dotnet run --project src/MarkItDown.Cli -- --help
```

### Library Usage

Basic usage in C#:

```csharp
using MarkItDown;

var markItDown = new MarkItDown();

// Convert from file path
var result = markItDown.Convert("document.html");
Console.WriteLine(result.TextContent);

// Convert from stream
using var stream = File.OpenRead("document.html");
var streamInfo = new StreamInfo { Extension = ".html", MimeType = "text/html" };
result = markItDown.ConvertStream(stream, streamInfo);
Console.WriteLine(result.TextContent);
```

Register custom converters:

```csharp
using MarkItDown;

var markItDown = new MarkItDown(enableBuiltins: false);
markItDown.RegisterConverter(new MyCustomConverter());

var result = markItDown.Convert("custom.xyz");
```

## Project Structure

```
markitdown.cshasp/
├── src/
│   ├── MarkItDown/           # Core library
│   │   ├── Converters/       # File format converters
│   │   ├── MarkItDown.cs     # Main conversion orchestrator
│   │   ├── StreamInfo.cs     # Stream metadata
│   │   └── ...
│   └── MarkItDown.Cli/       # Command-line interface
├── tests/
│   └── MarkItDown.Tests/     # Unit tests
└── MarkItDown.sln            # Solution file
```

## API Reference

### MarkItDown Class

The main class for document conversion.

```csharp
// Constructor
var markItDown = new MarkItDown(enableBuiltins: true);

// Convert methods
DocumentConverterResult Convert(string path, StreamInfo? streamInfo = null);
DocumentConverterResult ConvertLocal(string path, StreamInfo? streamInfo = null);
DocumentConverterResult ConvertStream(Stream stream, StreamInfo? streamInfo = null);
DocumentConverterResult ConvertUri(Uri uri, StreamInfo? streamInfo = null);

// Register custom converter
void RegisterConverter(IDocumentConverter converter, float priority = 0.0f);
```

### IDocumentConverter Interface

Implement this interface to create custom converters:

```csharp
public interface IDocumentConverter
{
    bool Accepts(Stream stream, StreamInfo streamInfo);
    DocumentConverterResult Convert(Stream stream, StreamInfo streamInfo);
}
```

### DocumentConverterResult

Result of a conversion operation:

```csharp
public class DocumentConverterResult
{
    public string? Title { get; set; }
    public required string TextContent { get; set; }
}
```

### StreamInfo

Metadata about the stream being converted:

```csharp
public class StreamInfo
{
    public string? MimeType { get; init; }
    public string? Charset { get; init; }
    public string? Filename { get; init; }
    public string? Extension { get; init; }
    public string? LocalPath { get; init; }
    public string? Url { get; init; }
}
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

### Running Tests

```bash
dotnet test
```

### Building

```bash
dotnet build
```

## Why Markdown?

Markdown is extremely close to plain text with minimal markup, but still provides a way to represent important document structure. LLMs like GPT-4 natively "speak" Markdown and have been trained on vast amounts of Markdown-formatted text. Markdown conventions are also highly token-efficient.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Credits

This project is a .NET port of [Microsoft's MarkItDown](https://github.com/microsoft/markitdown) Python library.

