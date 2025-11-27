namespace MarkItDown.Tests;

public class PlainTextConverterTests
{
    [Fact]
    public void Convert_PlainTextFile_ReturnsCorrectContent()
    {
        // Arrange
        var markItDown = new MarkItDown();
        var content = "Hello, World!\nThis is a test.";
        var stream = CreateStream(content);
        var streamInfo = new StreamInfo { Extension = ".txt", MimeType = "text/plain" };

        // Act
        var result = markItDown.ConvertStream(stream, streamInfo);

        // Assert
        Assert.Equal(content, result.TextContent);
    }

    [Fact]
    public void Convert_JsonFile_ReturnsCorrectContent()
    {
        // Arrange
        var markItDown = new MarkItDown();
        var content = """{"key": "value"}""";
        var stream = CreateStream(content);
        var streamInfo = new StreamInfo { Extension = ".json", MimeType = "application/json" };

        // Act
        var result = markItDown.ConvertStream(stream, streamInfo);

        // Assert
        Assert.Equal(content, result.TextContent);
    }

    [Fact]
    public void Convert_PythonCodeFile_ReturnsCodeBlock()
    {
        // Arrange
        var markItDown = new MarkItDown();
        var content = "print('Hello, World!')";
        var stream = CreateStream(content);
        var streamInfo = new StreamInfo { Extension = ".py", MimeType = "text/x-python" };

        // Act
        var result = markItDown.ConvertStream(stream, streamInfo);

        // Assert
        Assert.Contains("```python", result.TextContent);
        Assert.Contains(content, result.TextContent);
        Assert.Contains("```", result.TextContent);
    }

    [Fact]
    public void Convert_CSharpCodeFile_ReturnsCodeBlock()
    {
        // Arrange
        var markItDown = new MarkItDown();
        var content = "Console.WriteLine(\"Hello, World!\");";
        var stream = CreateStream(content);
        var streamInfo = new StreamInfo { Extension = ".cs", MimeType = "text/x-csharp" };

        // Act
        var result = markItDown.ConvertStream(stream, streamInfo);

        // Assert
        Assert.Contains("```csharp", result.TextContent);
        Assert.Contains(content, result.TextContent);
    }

    private static MemoryStream CreateStream(string content)
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(content);
        writer.Flush();
        stream.Position = 0;
        return stream;
    }
}
