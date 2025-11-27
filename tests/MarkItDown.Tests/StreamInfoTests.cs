namespace MarkItDown.Tests;

public class StreamInfoTests
{
    [Fact]
    public void CopyAndUpdate_NullOther_ReturnsCopy()
    {
        // Arrange
        var original = new StreamInfo
        {
            MimeType = "text/plain",
            Extension = ".txt",
            Filename = "test.txt"
        };

        // Act
        var copy = original.CopyAndUpdate();

        // Assert
        Assert.Equal(original.MimeType, copy.MimeType);
        Assert.Equal(original.Extension, copy.Extension);
        Assert.Equal(original.Filename, copy.Filename);
    }

    [Fact]
    public void CopyAndUpdate_WithOther_MergesValues()
    {
        // Arrange
        var original = new StreamInfo
        {
            MimeType = "text/plain",
            Extension = ".txt",
            Filename = "test.txt"
        };

        var other = new StreamInfo
        {
            MimeType = "text/html",
            Charset = "utf-8"
        };

        // Act
        var merged = original.CopyAndUpdate(other);

        // Assert
        Assert.Equal("text/html", merged.MimeType); // Updated from other
        Assert.Equal(".txt", merged.Extension); // Kept from original
        Assert.Equal("test.txt", merged.Filename); // Kept from original
        Assert.Equal("utf-8", merged.Charset); // Added from other
    }

    [Fact]
    public void CopyAndUpdate_OtherHasNullValues_KeepsOriginalValues()
    {
        // Arrange
        var original = new StreamInfo
        {
            MimeType = "text/plain",
            Extension = ".txt"
        };

        var other = new StreamInfo
        {
            Charset = "utf-8"
            // MimeType and Extension are null
        };

        // Act
        var merged = original.CopyAndUpdate(other);

        // Assert
        Assert.Equal("text/plain", merged.MimeType);
        Assert.Equal(".txt", merged.Extension);
        Assert.Equal("utf-8", merged.Charset);
    }
}
