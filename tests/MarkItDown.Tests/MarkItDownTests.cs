namespace MarkItDown.Tests;

public class MarkItDownTests
{
    [Fact]
    public void Constructor_Default_RegistersBuiltinConverters()
    {
        // Arrange & Act
        var markItDown = new MarkItDown();

        // Assert - should be able to convert plain text without error
        var content = "Test content";
        var stream = CreateStream(content);
        var streamInfo = new StreamInfo { Extension = ".txt", MimeType = "text/plain" };

        var result = markItDown.ConvertStream(stream, streamInfo);
        Assert.Equal(content, result.TextContent);
    }

    [Fact]
    public void Constructor_DisableBuiltins_NoConvertersRegistered()
    {
        // Arrange & Act
        var markItDown = new MarkItDown(enableBuiltins: false);

        // Assert - should throw when trying to convert
        var content = "Test content";
        var stream = CreateStream(content);
        var streamInfo = new StreamInfo { Extension = ".txt", MimeType = "text/plain" };

        Assert.Throws<Exceptions.UnsupportedFormatException>(() => markItDown.ConvertStream(stream, streamInfo));
    }

    [Fact]
    public void RegisterConverter_CustomConverter_IsUsed()
    {
        // Arrange
        var markItDown = new MarkItDown(enableBuiltins: false);
        var customConverter = new CustomTestConverter();
        markItDown.RegisterConverter(customConverter);

        var content = "Test content";
        var stream = CreateStream(content);
        var streamInfo = new StreamInfo { Extension = ".custom", MimeType = "text/custom" };

        // Act
        var result = markItDown.ConvertStream(stream, streamInfo);

        // Assert
        Assert.Equal("Custom: Test content", result.TextContent);
    }

    [Fact]
    public void ConvertStream_NonSeekableStream_HandlesCorrectly()
    {
        // Arrange
        var markItDown = new MarkItDown();
        var content = "Test content";
        var nonSeekableStream = new NonSeekableStream(CreateStream(content));
        var streamInfo = new StreamInfo { Extension = ".txt", MimeType = "text/plain" };

        // Act
        var result = markItDown.ConvertStream(nonSeekableStream, streamInfo);

        // Assert
        Assert.Equal(content, result.TextContent);
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

    private class CustomTestConverter : IDocumentConverter
    {
        public bool Accepts(Stream stream, StreamInfo streamInfo) =>
            streamInfo.Extension == ".custom" || streamInfo.MimeType == "text/custom";

        public DocumentConverterResult Convert(Stream stream, StreamInfo streamInfo)
        {
            using var reader = new StreamReader(stream, leaveOpen: true);
            var content = reader.ReadToEnd();
            return new DocumentConverterResult
            {
                TextContent = $"Custom: {content}"
            };
        }
    }

    private class NonSeekableStream : Stream
    {
        private readonly Stream _inner;

        public NonSeekableStream(Stream inner) => _inner = inner;

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
