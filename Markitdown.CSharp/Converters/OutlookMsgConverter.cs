using MsgReader.Outlook;
using System.Linq;

namespace MarkItDown.CSharp.Converters;

public sealed class OutlookMsgConverter : DocumentConverter
{
    private static readonly string[] AcceptedMimePrefixes =
    {
        "application/vnd.ms-outlook",
    };

    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".msg",
    };

    private readonly HtmlConverter _htmlConverter = new();

    public override bool Accepts(Stream fileStream, StreamInfo streamInfo, ConversionOptions options)
    {
        var extension = streamInfo.Extension ?? string.Empty;
        if (AcceptedExtensions.Contains(extension))
        {
            return true;
        }

        var mimetype = streamInfo.MimeType ?? string.Empty;
        if (AcceptedMimePrefixes.Any(prefix => mimetype.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    public override Task<DocumentConverterResult> ConvertAsync(
        Stream fileStream,
        StreamInfo streamInfo,
        ConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        fileStream.CopyTo(buffer);
        buffer.Seek(0, SeekOrigin.Begin);

        using var message = new Storage.Message(buffer);
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("# Email Message");
        builder.AppendLine();
        if (!string.IsNullOrWhiteSpace(message.Sender?.DisplayName))
        {
            builder.AppendLine($"**From:** {message.Sender.DisplayName}");
        }
        if (message.Recipients.Any())
        {
            var recipients = string.Join(", ", message.Recipients.Select(r => r.DisplayName));
            builder.AppendLine($"**To:** {recipients}");
        }
        if (!string.IsNullOrWhiteSpace(message.Subject))
        {
            builder.AppendLine($"**Subject:** {message.Subject}");
        }

        builder.AppendLine();
        builder.AppendLine("## Content");
        builder.AppendLine();

        var body = !string.IsNullOrWhiteSpace(message.BodyHtml)
            ? _htmlConverter.ConvertFromString(message.BodyHtml).Markdown
            : message.BodyText ?? string.Empty;

        builder.AppendLine(body.Trim());

        var result = new DocumentConverterResult(builder.ToString().Trim(), message.Subject);
        return Task.FromResult(result);
    }
}

