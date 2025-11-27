using System.Text;
using MarkItDown.Converters;

namespace MarkItDown.Tests;

public class PlainTextConverterTests
{
    private readonly PlainTextConverter _converter = new();

    [Theory]
    [InlineData(".txt", true)]
    [InlineData(".text", true)]
    [InlineData(".log", true)]
    [InlineData(".md", true)]
    [InlineData(".markdown", true)]
    [InlineData(".html", false)]
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
    [InlineData("text/plain", true)]
    [InlineData("text/markdown", true)]
    [InlineData("text/html", false)]
    [InlineData("application/json", false)]
    public void CanConvertMimeType_WithVariousTypes_ReturnsExpectedResult(string mimeType, bool expected)
    {
        // Arrange & Act
        var result = _converter.CanConvertMimeType(mimeType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_WithPlainTextContent_ReturnsContentUnchanged()
    {
        // Arrange
        var content = "Hello, World!\nThis is a test.";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        // Act
        var result = _converter.Convert(stream, "test.txt");

        // Assert
        Assert.Equal(content, result.TextContent);
        Assert.Equal("test", result.Title);
        Assert.Equal("text/plain", result.SourceType);
    }

    [Fact]
    public void Convert_WithNullStream_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => _converter.Convert((Stream)null!, "test.txt"));
    }

    [Fact]
    public void Convert_WithoutFileName_HasNullTitle()
    {
        // Arrange
        var content = "Hello, World!";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        // Act
        var result = _converter.Convert(stream);

        // Assert
        Assert.Equal(content, result.TextContent);
        Assert.Null(result.Title);
    }
}
