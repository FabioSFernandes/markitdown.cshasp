using System.Text;
using MarkItDown.Converters;

namespace MarkItDown.Tests;

public class CsvConverterTests
{
    private readonly CsvConverter _converter = new();

    [Theory]
    [InlineData(".csv", true)]
    [InlineData(".txt", false)]
    [InlineData(".json", false)]
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
    [InlineData("text/csv", true)]
    [InlineData("application/csv", true)]
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
    public void Convert_WithSimpleCsv_ConvertsToMarkdownTable()
    {
        // Arrange
        var csv = "Name,Age,City\nJohn,30,NYC\nJane,25,LA";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = _converter.Convert(stream, "test.csv");

        // Assert
        Assert.Contains("| Name | Age | City |", result.TextContent);
        Assert.Contains("| --- | --- | --- |", result.TextContent);
        Assert.Contains("| John | 30 | NYC |", result.TextContent);
        Assert.Contains("| Jane | 25 | LA |", result.TextContent);
    }

    [Fact]
    public void Convert_WithQuotedValues_HandlesQuotesCorrectly()
    {
        // Arrange
        var csv = "Name,Description\n\"John\",\"A long, detailed description\"\n\"Jane\",\"Simple\"";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = _converter.Convert(stream, "test.csv");

        // Assert
        Assert.Contains("| Name | Description |", result.TextContent);
        Assert.Contains("A long, detailed description", result.TextContent);
    }

    [Fact]
    public void Convert_WithEscapedQuotes_HandlesEscapedQuotesCorrectly()
    {
        // Arrange
        var csv = "Name,Quote\nJohn,\"He said \"\"Hello\"\"\"";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = _converter.Convert(stream, "test.csv");

        // Assert
        Assert.Contains("He said \"Hello\"", result.TextContent);
    }

    [Fact]
    public void Convert_WithPipeCharacter_EscapesPipeCharacter()
    {
        // Arrange
        var csv = "Name,Command\nTest,echo|grep";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = _converter.Convert(stream, "test.csv");

        // Assert
        Assert.Contains("echo\\|grep", result.TextContent);
    }

    [Fact]
    public void Convert_WithEmptyCsv_ReturnsEmptyContent()
    {
        // Arrange
        var csv = "";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = _converter.Convert(stream, "test.csv");

        // Assert
        Assert.Equal(string.Empty, result.TextContent);
    }

    [Fact]
    public void Convert_WithWindowsLineEndings_HandlesCorrectly()
    {
        // Arrange
        var csv = "Name,Age\r\nJohn,30\r\nJane,25";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = _converter.Convert(stream, "test.csv");

        // Assert
        Assert.Contains("| Name | Age |", result.TextContent);
        Assert.Contains("| John | 30 |", result.TextContent);
    }

    [Fact]
    public void Convert_SetsCorrectSourceType()
    {
        // Arrange
        var csv = "Name\nJohn";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = _converter.Convert(stream, "test.csv");

        // Assert
        Assert.Equal("text/csv", result.SourceType);
    }

    [Fact]
    public void Convert_SetsCorrectTitle()
    {
        // Arrange
        var csv = "Name\nJohn";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = _converter.Convert(stream, "employees.csv");

        // Assert
        Assert.Equal("employees", result.Title);
    }
}
