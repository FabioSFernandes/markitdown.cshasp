using System.Diagnostics.CodeAnalysis;

namespace MarkItDown.CSharp;

/// <summary>
///     Metadata describing the origin of a stream being converted.
/// </summary>
public sealed record StreamInfo
{
    public string? MimeType { get; init; }
    public string? Extension { get; init; }
    public string? Charset { get; init; }
    public string? FileName { get; init; }
    public string? LocalPath { get; init; }
    public string? Url { get; init; }

    public StreamInfo CopyAndUpdate(StreamInfo? other = null, Action<StreamInfoBuilder>? configure = null)
    {
        var builder = new StreamInfoBuilder(this);
        if (other is not null)
        {
            builder.Merge(other);
        }

        configure?.Invoke(builder);
        return builder.Build();
    }

    public StreamInfo CopyAndUpdate(
        string? mimeType = null,
        string? extension = null,
        string? charset = null,
        string? fileName = null,
        string? localPath = null,
        string? url = null)
    {
        return this with
        {
            MimeType = mimeType ?? MimeType,
            Extension = extension ?? Extension,
            Charset = charset ?? Charset,
            FileName = fileName ?? FileName,
            LocalPath = localPath ?? LocalPath,
            Url = url ?? Url,
        };
    }

    public sealed class StreamInfoBuilder
    {
        private string? _charset;
        private string? _extension;
        private string? _fileName;
        private string? _localPath;
        private string? _mimeType;
        private string? _url;

        public StreamInfoBuilder(StreamInfo source)
        {
            _mimeType = source.MimeType;
            _extension = source.Extension;
            _charset = source.Charset;
            _fileName = source.FileName;
            _localPath = source.LocalPath;
            _url = source.Url;
        }

        public StreamInfoBuilder Merge(StreamInfo streamInfo)
        {
            _mimeType ??= streamInfo.MimeType;
            _extension ??= streamInfo.Extension;
            _charset ??= streamInfo.Charset;
            _fileName ??= streamInfo.FileName;
            _localPath ??= streamInfo.LocalPath;
            _url ??= streamInfo.Url;
            return this;
        }

        public StreamInfoBuilder SetMimeType(string? mimeType)
        {
            _mimeType = mimeType ?? _mimeType;
            return this;
        }

        public StreamInfoBuilder SetExtension(string? extension)
        {
            _extension = extension ?? _extension;
            return this;
        }

        public StreamInfoBuilder SetCharset(string? charset)
        {
            _charset = charset ?? _charset;
            return this;
        }

        public StreamInfoBuilder SetFileName(string? fileName)
        {
            _fileName = fileName ?? _fileName;
            return this;
        }

        public StreamInfoBuilder SetLocalPath(string? path)
        {
            _localPath = path ?? _localPath;
            return this;
        }

        public StreamInfoBuilder SetUrl(string? url)
        {
            _url = url ?? _url;
            return this;
        }

        [SuppressMessage("Design", "CA1024")]
        public StreamInfo Build()
        {
            return new StreamInfo
            {
                MimeType = _mimeType,
                Extension = _extension,
                Charset = _charset,
                FileName = _fileName,
                LocalPath = _localPath,
                Url = _url,
            };
        }
    }
}

