namespace MarkItDown.Tests;

public class HtmlConverterTests
{
    [Fact]
    public void Convert_SimpleHtml_ExtractsTitle()
    {
        // Arrange
        var markItDown = new MarkItDown();
        var html = """
            <html>
            <head><title>Test Page</title></head>
            <body><p>Content</p></body>
            </html>
            """;
        var stream = CreateStream(html);
        var streamInfo = new StreamInfo { Extension = ".html", MimeType = "text/html" };

        // Act
        var result = markItDown.ConvertStream(stream, streamInfo);

        // Assert
        Assert.Equal("Test Page", result.Title);
    }

    [Fact]
    public void Convert_HeadingsHtml_ConvertedToMarkdownHeadings()
    {
        // Arrange
        var markItDown = new MarkItDown();
        var html = """
            <html>
            <body>
            <h1>Heading 1</h1>
            <h2>Heading 2</h2>
            <h3>Heading 3</h3>
            </body>
            </html>
            """;
        var stream = CreateStream(html);
        var streamInfo = new StreamInfo { Extension = ".html", MimeType = "text/html" };

        // Act
        var result = markItDown.ConvertStream(stream, streamInfo);

        // Assert
        Assert.Contains("# Heading 1", result.TextContent);
        Assert.Contains("## Heading 2", result.TextContent);
        Assert.Contains("### Heading 3", result.TextContent);
    }

    [Fact]
    public void Convert_BoldAndItalicHtml_ConvertedToMarkdown()
    {
        // Arrange
        var markItDown = new MarkItDown();
        var html = """
            <html>
            <body>
            <p><strong>bold</strong> and <em>italic</em></p>
            </body>
            </html>
            """;
        var stream = CreateStream(html);
        var streamInfo = new StreamInfo { Extension = ".html", MimeType = "text/html" };

        // Act
        var result = markItDown.ConvertStream(stream, streamInfo);

        // Assert
        Assert.Contains("**bold**", result.TextContent);
        Assert.Contains("*italic*", result.TextContent);
    }

    [Fact]
    public void Convert_LinksHtml_ConvertedToMarkdownLinks()
    {
        // Arrange
        var markItDown = new MarkItDown();
        var html = """
            <html>
            <body>
            <a href="https://example.com">Example</a>
            </body>
            </html>
            """;
        var stream = CreateStream(html);
        var streamInfo = new StreamInfo { Extension = ".html", MimeType = "text/html" };

        // Act
        var result = markItDown.ConvertStream(stream, streamInfo);

        // Assert
        Assert.Contains("[Example](https://example.com)", result.TextContent);
    }

    [Fact]
    public void Convert_UnorderedListHtml_ConvertedToMarkdownList()
    {
        // Arrange
        var markItDown = new MarkItDown();
        var html = """
            <html>
            <body>
            <ul>
            <li>Item 1</li>
            <li>Item 2</li>
            <li>Item 3</li>
            </ul>
            </body>
            </html>
            """;
        var stream = CreateStream(html);
        var streamInfo = new StreamInfo { Extension = ".html", MimeType = "text/html" };

        // Act
        var result = markItDown.ConvertStream(stream, streamInfo);

        // Assert
        Assert.Contains("- Item 1", result.TextContent);
        Assert.Contains("- Item 2", result.TextContent);
        Assert.Contains("- Item 3", result.TextContent);
    }

    [Fact]
    public void Convert_OrderedListHtml_ConvertedToMarkdownList()
    {
        // Arrange
        var markItDown = new MarkItDown();
        var html = """
            <html>
            <body>
            <ol>
            <li>First</li>
            <li>Second</li>
            <li>Third</li>
            </ol>
            </body>
            </html>
            """;
        var stream = CreateStream(html);
        var streamInfo = new StreamInfo { Extension = ".html", MimeType = "text/html" };

        // Act
        var result = markItDown.ConvertStream(stream, streamInfo);

        // Assert
        Assert.Contains("1. First", result.TextContent);
        Assert.Contains("2. Second", result.TextContent);
        Assert.Contains("3. Third", result.TextContent);
    }

    [Fact]
    public void Convert_CodeBlockHtml_ConvertedToMarkdownCodeBlock()
    {
        // Arrange
        var markItDown = new MarkItDown();
        var html = """
            <html>
            <body>
            <pre><code>function test() { return 1; }</code></pre>
            </body>
            </html>
            """;
        var stream = CreateStream(html);
        var streamInfo = new StreamInfo { Extension = ".html", MimeType = "text/html" };

        // Act
        var result = markItDown.ConvertStream(stream, streamInfo);

        // Assert
        Assert.Contains("```", result.TextContent);
        Assert.Contains("function test()", result.TextContent);
    }

    [Fact]
    public void Convert_InlineCodeHtml_ConvertedToMarkdownCode()
    {
        // Arrange
        var markItDown = new MarkItDown();
        var html = """
            <html>
            <body>
            <p>Use the <code>print()</code> function.</p>
            </body>
            </html>
            """;
        var stream = CreateStream(html);
        var streamInfo = new StreamInfo { Extension = ".html", MimeType = "text/html" };

        // Act
        var result = markItDown.ConvertStream(stream, streamInfo);

        // Assert
        Assert.Contains("`print()`", result.TextContent);
    }

    [Fact]
    public void Convert_TableHtml_ConvertedToMarkdownTable()
    {
        // Arrange
        var markItDown = new MarkItDown();
        var html = """
            <html>
            <body>
            <table>
            <tr><th>Name</th><th>Age</th></tr>
            <tr><td>Alice</td><td>30</td></tr>
            <tr><td>Bob</td><td>25</td></tr>
            </table>
            </body>
            </html>
            """;
        var stream = CreateStream(html);
        var streamInfo = new StreamInfo { Extension = ".html", MimeType = "text/html" };

        // Act
        var result = markItDown.ConvertStream(stream, streamInfo);

        // Assert
        Assert.Contains("| Name | Age |", result.TextContent);
        Assert.Contains("| --- | --- |", result.TextContent);
        Assert.Contains("| Alice | 30 |", result.TextContent);
        Assert.Contains("| Bob | 25 |", result.TextContent);
    }

    [Fact]
    public void Convert_ImageHtml_ConvertedToMarkdownImage()
    {
        // Arrange
        var markItDown = new MarkItDown();
        var html = """
            <html>
            <body>
            <img src="image.png" alt="Test Image">
            </body>
            </html>
            """;
        var stream = CreateStream(html);
        var streamInfo = new StreamInfo { Extension = ".html", MimeType = "text/html" };

        // Act
        var result = markItDown.ConvertStream(stream, streamInfo);

        // Assert
        Assert.Contains("![Test Image](image.png)", result.TextContent);
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
