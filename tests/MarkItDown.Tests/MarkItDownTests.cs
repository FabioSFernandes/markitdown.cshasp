using System.Text;
using MarkItDown.Converters;

namespace MarkItDown.Tests;

public class MarkItDownTests
{
    [Fact]
    public void Constructor_CreatesInstanceWithDefaultConverters()
    {
        // Arrange & Act
        var markItDown = new MarkItDown();

        // Assert
        Assert.NotEmpty(markItDown.Converters);
        Assert.Contains(markItDown.Converters, c => c is PlainTextConverter);
        Assert.Contains(markItDown.Converters, c => c is HtmlConverter);
        Assert.Contains(markItDown.Converters, c => c is CsvConverter);
        Assert.Contains(markItDown.Converters, c => c is JsonConverter);
    }

    [Fact]
    public void RegisterConverter_AddsConverter()
    {
        // Arrange
        var markItDown = new MarkItDown();
        var customConverter = new MockConverter();
        var initialCount = markItDown.Converters.Count;

        // Act
        markItDown.RegisterConverter(customConverter);

        // Assert
        Assert.Equal(initialCount + 1, markItDown.Converters.Count);
        Assert.Contains(customConverter, markItDown.Converters);
    }

    [Fact]
    public void RegisterConverter_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var markItDown = new MarkItDown();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => markItDown.RegisterConverter(null!));
    }

    [Fact]
    public void IsSupported_WithSupportedExtension_ReturnsTrue()
    {
        // Arrange
        var markItDown = new MarkItDown();

        // Act & Assert
        Assert.True(markItDown.IsSupported("test.txt"));
        Assert.True(markItDown.IsSupported("test.html"));
        Assert.True(markItDown.IsSupported("test.csv"));
        Assert.True(markItDown.IsSupported("test.json"));
    }

    [Fact]
    public void IsSupported_WithUnsupportedExtension_ReturnsFalse()
    {
        // Arrange
        var markItDown = new MarkItDown();

        // Act & Assert
        Assert.False(markItDown.IsSupported("test.pdf"));
        Assert.False(markItDown.IsSupported("test.docx"));
    }

    [Fact]
    public void IsMimeTypeSupported_WithSupportedMimeType_ReturnsTrue()
    {
        // Arrange
        var markItDown = new MarkItDown();

        // Act & Assert
        Assert.True(markItDown.IsMimeTypeSupported("text/plain"));
        Assert.True(markItDown.IsMimeTypeSupported("text/html"));
        Assert.True(markItDown.IsMimeTypeSupported("text/csv"));
        Assert.True(markItDown.IsMimeTypeSupported("application/json"));
    }

    [Fact]
    public void IsMimeTypeSupported_WithUnsupportedMimeType_ReturnsFalse()
    {
        // Arrange
        var markItDown = new MarkItDown();

        // Act & Assert
        Assert.False(markItDown.IsMimeTypeSupported("application/pdf"));
        Assert.False(markItDown.IsMimeTypeSupported("application/vnd.openxmlformats-officedocument.wordprocessingml.document"));
    }

    [Fact]
    public void Convert_WithStream_ConvertsContent()
    {
        // Arrange
        var markItDown = new MarkItDown();
        var content = "Hello, World!";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        // Act
        var result = markItDown.Convert(stream, "test.txt");

        // Assert
        Assert.Equal(content, result.TextContent);
    }

    [Fact]
    public void Convert_WithNullStream_ThrowsArgumentNullException()
    {
        // Arrange
        var markItDown = new MarkItDown();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => markItDown.Convert((Stream)null!, "test.txt"));
    }

    [Fact]
    public void Convert_WithUnsupportedFileType_ThrowsNotSupportedException()
    {
        // Arrange
        var markItDown = new MarkItDown();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => markItDown.Convert(stream, "test.unsupported"));
    }

    [Fact]
    public void ConvertByMimeType_WithSupportedMimeType_ConvertsContent()
    {
        // Arrange
        var markItDown = new MarkItDown();
        var content = "Hello, World!";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        // Act
        var result = markItDown.ConvertByMimeType(stream, "text/plain");

        // Assert
        Assert.Equal(content, result.TextContent);
    }

    [Fact]
    public void ConvertByMimeType_WithUnsupportedMimeType_ThrowsNotSupportedException()
    {
        // Arrange
        var markItDown = new MarkItDown();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => markItDown.ConvertByMimeType(stream, "application/pdf"));
    }

    [Fact]
    public void RegisteredConverter_TakesPriority()
    {
        // Arrange
        var markItDown = new MarkItDown();
        var customConverter = new MockConverter();
        markItDown.RegisterConverter(customConverter);

        // Act
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var result = markItDown.Convert(stream, "test.mock");

        // Assert
        Assert.Equal("mock content", result.TextContent);
    }

    private class MockConverter : IDocumentConverter
    {
        public bool CanConvert(string filePath) => filePath.EndsWith(".mock");
        public bool CanConvertMimeType(string mimeType) => mimeType == "application/mock";
        public ConversionResult Convert(string filePath) => new("mock content", "mock", "application/mock");
        public ConversionResult Convert(Stream stream, string? fileName = null) => new("mock content", "mock", "application/mock");
    }
}
