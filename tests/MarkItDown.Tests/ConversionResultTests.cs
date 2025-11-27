using MarkItDown;

namespace MarkItDown.Tests;

public class ConversionResultTests
{
    [Fact]
    public void Constructor_WithValidContent_CreatesInstance()
    {
        // Arrange & Act
        var result = new ConversionResult("# Test", "Title", "text/plain");

        // Assert
        Assert.Equal("# Test", result.TextContent);
        Assert.Equal("Title", result.Title);
        Assert.Equal("text/plain", result.SourceType);
    }

    [Fact]
    public void Constructor_WithNullContent_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ConversionResult(null!));
    }

    [Fact]
    public void Constructor_WithOptionalParameters_AllowsNullValues()
    {
        // Arrange & Act
        var result = new ConversionResult("content");

        // Assert
        Assert.Equal("content", result.TextContent);
        Assert.Null(result.Title);
        Assert.Null(result.SourceType);
    }
}
