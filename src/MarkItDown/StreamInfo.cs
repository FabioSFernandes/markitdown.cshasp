namespace MarkItDown;

/// <summary>
/// Contains information about a stream being processed for conversion.
/// </summary>
public sealed class StreamInfo
{
    /// <summary>
    /// Gets or sets the MIME type of the content.
    /// </summary>
    public string? MimeType { get; init; }

    /// <summary>
    /// Gets or sets the character encoding of the content.
    /// </summary>
    public string? Charset { get; init; }

    /// <summary>
    /// Gets or sets the filename of the content.
    /// </summary>
    public string? Filename { get; init; }

    /// <summary>
    /// Gets or sets the file extension (including the leading dot).
    /// </summary>
    public string? Extension { get; init; }

    /// <summary>
    /// Gets or sets the local path of the file.
    /// </summary>
    public string? LocalPath { get; init; }

    /// <summary>
    /// Gets or sets the URL of the content.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Creates a new StreamInfo by copying this instance and updating with values from another StreamInfo.
    /// Non-null values in the other StreamInfo will override values in this instance.
    /// </summary>
    /// <param name="other">The StreamInfo to merge with.</param>
    /// <returns>A new StreamInfo with merged values.</returns>
    public StreamInfo CopyAndUpdate(StreamInfo? other = null)
    {
        if (other is null)
        {
            return new StreamInfo
            {
                MimeType = MimeType,
                Charset = Charset,
                Filename = Filename,
                Extension = Extension,
                LocalPath = LocalPath,
                Url = Url
            };
        }

        return new StreamInfo
        {
            MimeType = other.MimeType ?? MimeType,
            Charset = other.Charset ?? Charset,
            Filename = other.Filename ?? Filename,
            Extension = other.Extension ?? Extension,
            LocalPath = other.LocalPath ?? LocalPath,
            Url = other.Url ?? Url
        };
    }
}
