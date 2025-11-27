using System.Text;
using MarkItDown.Converters;

namespace MarkItDown.Tests;

public class JsonConverterTests
{
    private readonly JsonConverter _converter = new();

    [Theory]
    [InlineData(".json", true)]
    [InlineData(".txt", false)]
    [InlineData(".csv", false)]
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
    [InlineData("application/json", true)]
    [InlineData("text/json", true)]
    [InlineData("text/plain", false)]
    [InlineData("text/csv", false)]
    public void CanConvertMimeType_WithVariousTypes_ReturnsExpectedResult(string mimeType, bool expected)
    {
        // Arrange & Act
        var result = _converter.CanConvertMimeType(mimeType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_WithSimpleObject_ConvertsToMarkdown()
    {
        // Arrange
        var json = "{\"name\":\"John\",\"age\":30}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        var result = _converter.Convert(stream, "test.json");

        // Assert
        Assert.Contains("**name:**", result.TextContent);
        Assert.Contains("John", result.TextContent);
        Assert.Contains("**age:**", result.TextContent);
        Assert.Contains("30", result.TextContent);
    }

    [Fact]
    public void Convert_WithArray_ConvertsToMarkdownList()
    {
        // Arrange
        var json = "[\"apple\",\"banana\",\"cherry\"]";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        var result = _converter.Convert(stream, "test.json");

        // Assert
        Assert.Contains("apple", result.TextContent);
        Assert.Contains("banana", result.TextContent);
        Assert.Contains("cherry", result.TextContent);
    }

    [Fact]
    public void Convert_WithNestedObject_ConvertsToNestedMarkdown()
    {
        // Arrange
        var json = "{\"person\":{\"name\":\"John\",\"address\":{\"city\":\"NYC\"}}}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        var result = _converter.Convert(stream, "test.json");

        // Assert
        Assert.Contains("**person:**", result.TextContent);
        Assert.Contains("**name:**", result.TextContent);
        Assert.Contains("John", result.TextContent);
        Assert.Contains("**city:**", result.TextContent);
        Assert.Contains("NYC", result.TextContent);
    }

    [Fact]
    public void Convert_WithBooleanValues_ConvertsCorrectly()
    {
        // Arrange
        var json = "{\"active\":true,\"deleted\":false}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        var result = _converter.Convert(stream, "test.json");

        // Assert
        Assert.Contains("true", result.TextContent);
        Assert.Contains("false", result.TextContent);
    }

    [Fact]
    public void Convert_WithNullValue_ConvertsCorrectly()
    {
        // Arrange
        var json = "{\"value\":null}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        var result = _converter.Convert(stream, "test.json");

        // Assert
        Assert.Contains("null", result.TextContent);
    }

    [Fact]
    public void Convert_WithInvalidJson_ReturnsRawContentAsCodeBlock()
    {
        // Arrange
        var json = "{ invalid json }";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        var result = _converter.Convert(stream, "test.json");

        // Assert
        Assert.Contains("```json", result.TextContent);
        Assert.Contains("{ invalid json }", result.TextContent);
    }

    [Fact]
    public void Convert_SetsCorrectSourceType()
    {
        // Arrange
        var json = "{}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        var result = _converter.Convert(stream, "test.json");

        // Assert
        Assert.Equal("application/json", result.SourceType);
    }

    [Fact]
    public void Convert_SetsCorrectTitle()
    {
        // Arrange
        var json = "{}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        var result = _converter.Convert(stream, "config.json");

        // Assert
        Assert.Equal("config", result.Title);
    }
}
