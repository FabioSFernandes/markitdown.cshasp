using System.Text;
using MarkItDown.Converters;

namespace MarkItDown.Tests;

public class HtmlConverterTests
{
    private readonly HtmlConverter _converter = new();

    [Theory]
    [InlineData(".html", true)]
    [InlineData(".htm", true)]
    [InlineData(".xhtml", true)]
    [InlineData(".txt", false)]
    [InlineData(".pdf", false)]
    public void CanConvert_WithVariousExtensions_ReturnsExpectedResult(string extension, bool expected)
    {
        // Arrange
        var filePath = $"test{extension}";

        // Act
        var result = _converter.CanConvert(filePath);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("text/html", true)]
    [InlineData("application/xhtml+xml", true)]
    [InlineData("text/plain", false)]
    [InlineData("application/json", false)]
    public void CanConvertMimeType_WithVariousTypes_ReturnsExpectedResult(string mimeType, bool expected)
    {
        // Arrange & Act
        var result = _converter.CanConvertMimeType(mimeType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_WithHeadings_ConvertsToMarkdownHeadings()
    {
        // Arrange
        var html = "<h1>Title</h1><h2>Subtitle</h2>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));

        // Act
        var result = _converter.Convert(stream, "test.html");

        // Assert
        Assert.Contains("# Title", result.TextContent);
        Assert.Contains("## Subtitle", result.TextContent);
    }

    [Fact]
    public void Convert_WithParagraph_ConvertsToMarkdownParagraph()
    {
        // Arrange
        var html = "<p>This is a paragraph.</p>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));

        // Act
        var result = _converter.Convert(stream, "test.html");

        // Assert
        Assert.Contains("This is a paragraph.", result.TextContent);
    }

    [Fact]
    public void Convert_WithBoldText_ConvertsToMarkdownBold()
    {
        // Arrange
        var html = "<strong>Bold text</strong>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));

        // Act
        var result = _converter.Convert(stream, "test.html");

        // Assert
        Assert.Contains("**Bold text**", result.TextContent);
    }

    [Fact]
    public void Convert_WithItalicText_ConvertsToMarkdownItalic()
    {
        // Arrange
        var html = "<em>Italic text</em>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));

        // Act
        var result = _converter.Convert(stream, "test.html");

        // Assert
        Assert.Contains("*Italic text*", result.TextContent);
    }

    [Fact]
    public void Convert_WithLink_ConvertsToMarkdownLink()
    {
        // Arrange
        var html = "<a href=\"https://example.com\">Example</a>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));

        // Act
        var result = _converter.Convert(stream, "test.html");

        // Assert
        Assert.Contains("[Example](https://example.com)", result.TextContent);
    }

    [Fact]
    public void Convert_WithImage_ConvertsToMarkdownImage()
    {
        // Arrange
        var html = "<img src=\"image.png\" alt=\"Test Image\">";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));

        // Act
        var result = _converter.Convert(stream, "test.html");

        // Assert
        Assert.Contains("![Test Image](image.png)", result.TextContent);
    }

    [Fact]
    public void Convert_WithUnorderedList_ConvertsToMarkdownList()
    {
        // Arrange
        var html = "<ul><li>Item 1</li><li>Item 2</li></ul>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));

        // Act
        var result = _converter.Convert(stream, "test.html");

        // Assert
        Assert.Contains("- Item 1", result.TextContent);
        Assert.Contains("- Item 2", result.TextContent);
    }

    [Fact]
    public void Convert_WithOrderedList_ConvertsToMarkdownList()
    {
        // Arrange
        var html = "<ol><li>First</li><li>Second</li></ol>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));

        // Act
        var result = _converter.Convert(stream, "test.html");

        // Assert
        Assert.Contains("1. First", result.TextContent);
        Assert.Contains("2. Second", result.TextContent);
    }

    [Fact]
    public void Convert_WithCodeBlock_ConvertsToMarkdownCodeBlock()
    {
        // Arrange
        var html = "<pre><code>var x = 1;</code></pre>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));

        // Act
        var result = _converter.Convert(stream, "test.html");

        // Assert
        Assert.Contains("```", result.TextContent);
        Assert.Contains("var x = 1;", result.TextContent);
    }

    [Fact]
    public void Convert_WithInlineCode_ConvertsToMarkdownInlineCode()
    {
        // Arrange
        var html = "<p>Use <code>console.log()</code> for debugging.</p>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));

        // Act
        var result = _converter.Convert(stream, "test.html");

        // Assert
        Assert.Contains("`console.log()`", result.TextContent);
    }

    [Fact]
    public void Convert_WithBlockquote_ConvertsToMarkdownBlockquote()
    {
        // Arrange
        var html = "<blockquote>This is a quote.</blockquote>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));

        // Act
        var result = _converter.Convert(stream, "test.html");

        // Assert
        Assert.Contains("> This is a quote.", result.TextContent);
    }

    [Fact]
    public void Convert_WithTable_ConvertsToMarkdownTable()
    {
        // Arrange
        var html = @"
            <table>
                <tr><th>Name</th><th>Age</th></tr>
                <tr><td>John</td><td>30</td></tr>
                <tr><td>Jane</td><td>25</td></tr>
            </table>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));

        // Act
        var result = _converter.Convert(stream, "test.html");

        // Assert
        Assert.Contains("| Name | Age |", result.TextContent);
        Assert.Contains("| --- | --- |", result.TextContent);
        Assert.Contains("| John | 30 |", result.TextContent);
        Assert.Contains("| Jane | 25 |", result.TextContent);
    }

    [Fact]
    public void Convert_WithTitle_ExtractsTitle()
    {
        // Arrange
        var html = "<html><head><title>Test Page</title></head><body>Content</body></html>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));

        // Act
        var result = _converter.Convert(stream, "test.html");

        // Assert
        Assert.Equal("Test Page", result.Title);
    }

    [Fact]
    public void Convert_WithHorizontalRule_ConvertsToMarkdownRule()
    {
        // Arrange
        var html = "<p>Before</p><hr><p>After</p>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));

        // Act
        var result = _converter.Convert(stream, "test.html");

        // Assert
        Assert.Contains("---", result.TextContent);
    }
}
