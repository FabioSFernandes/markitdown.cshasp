using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.AspNetCore.StaticFiles;
using MarkItDown.CSharp.Converters;
using MarkItDown.CSharp.Exceptions;
using MarkItDown.CSharp.Interfaces;
using UriUtilities = MarkItDown.CSharp.Utilities.UriUtils;
using Ude;

namespace MarkItDown.CSharp;

public sealed class MarkItDown : IDisposable, IConverterRegistry, IConversionEngine
{
    private const double PrioritySpecific = 0.0;
    private const double PriorityGeneric = 10.0;

    private readonly HttpClient _httpClient;
    private readonly List<ConverterRegistration> _converters = new();
    private readonly Dictionary<string, List<ConverterRegistration>> _convertersByExtension = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILlmClient? _llmClient;
    private readonly string? _llmModel;
    private readonly string? _llmPrompt;
    private readonly string? _styleMap;
    private readonly string? _exifToolPath;
    private readonly string? _tesseractTessDataPath;
    private readonly string? _tesseractLang;
    private readonly IAudioTranscriptionService? _audioTranscriptionService;
    private readonly IEnumerable<IMarkItDownPlugin>? _plugins;
    private readonly bool _ownsHttpClient;

    private bool _builtinsEnabled;
    private bool _pluginsEnabled;

    public MarkItDown(MarkItDownOptions? options = null)
    {
        options ??= new MarkItDownOptions();

        if (options.HttpClient is null)
        {
            _httpClient = new HttpClient();
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = options.HttpClient;
            _ownsHttpClient = false;
        }

        _plugins = options.Plugins;
        _llmClient = options.LlmClient;
        _llmModel = options.LlmModel;
        _llmPrompt = options.LlmPrompt;
        _styleMap = options.StyleMap;
        _audioTranscriptionService = options.AudioTranscriptionService;
        _exifToolPath = ResolveExifToolPath(options.ExifToolPath);
        _tesseractTessDataPath = options.TesseractTessDataPath;
        _tesseractLang = options.TesseractLang;

        if (options.EnableBuiltins is null or true)
        {
            EnableBuiltins();
        }

        if (options.EnablePlugins == true)
        {
            EnablePlugins();
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    public void EnableBuiltins()
    {
        if (_builtinsEnabled)
        {
            throw new InvalidOperationException("Built-in converters already enabled.");
        }

        RegisterConverter(new PlainTextConverter(), PriorityGeneric);
        RegisterConverter(new ZipConverter(this), PriorityGeneric);
        RegisterConverter(new HtmlConverter(), PriorityGeneric);
        RegisterConverter(new RssConverter());
        RegisterConverter(new WikipediaConverter());
        RegisterConverter(new YouTubeConverter());
        RegisterConverter(new BingSerpConverter());
        RegisterConverter(new DocxConverter());
        RegisterConverter(new XlsxConverter());
        RegisterConverter(new XlsConverter());
        RegisterConverter(new PptxConverter());
        RegisterConverter(new PdfConverter());
        RegisterConverter(new RtfConverter());
        RegisterConverter(new AudioConverter());
        RegisterConverter(new ImageConverter());
        RegisterConverter(new IpynbConverter());
        RegisterConverter(new OutlookMsgConverter());
        RegisterConverter(new EpubConverter());
        RegisterConverter(new CsvConverter());

        _builtinsEnabled = true;
    }

    public void EnablePlugins()
    {
        if (_pluginsEnabled || _plugins is null)
        {
            return;
        }

        foreach (var plugin in _plugins)
        {
            plugin.RegisterConverters(this, ConversionOptions.Empty);
        }

        _pluginsEnabled = true;
    }

    public void RegisterConverter(IDocumentConverter converter, double priority = PrioritySpecific)
    {
        var registration = new ConverterRegistration(converter, priority);
        _converters.Insert(0, registration);

        var extensions = converter.SupportedExtensions;
        if (extensions is not null && extensions.Count > 0)
        {
            foreach (var ext in extensions)
            {
                var normalized = NormalizeExtension(ext);
                if (string.IsNullOrEmpty(normalized))
                    continue;
                if (!_convertersByExtension.TryGetValue(normalized, out var list))
                {
                    list = new List<ConverterRegistration>();
                    _convertersByExtension[normalized] = list;
                }
                list.Add(registration);
            }
        }
    }

    private static string? NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return null;
        var s = extension.Trim();
        if (s.Length > 0 && s[0] != '.')
            s = "." + s;
        return s.ToLowerInvariant();
    }

    public Task<DocumentConverterResult> ConvertAsync(
        object source,
        StreamInfo? streamInfo = null,
        ConversionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return source switch
        {
            string path when path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                            path.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
                            path.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                => ConvertUriAsync(path, streamInfo, options, cancellationToken),
            string path => ConvertLocalAsync(path, streamInfo, options, cancellationToken),
            Stream stream => ConvertStreamAsync(stream, streamInfo, options, cancellationToken),
            HttpResponseMessage response => ConvertResponseAsync(response, streamInfo, options, cancellationToken),
            _ => throw new ArgumentException("Unsupported source type.", nameof(source)),
        };
    }

    public DocumentConverterResult Convert(
        object source,
        StreamInfo? streamInfo = null,
        ConversionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return ConvertAsync(source, streamInfo, options, cancellationToken).GetAwaiter().GetResult();
    }

    public async Task<DocumentConverterResult> ConvertLocalAsync(
        string path,
        StreamInfo? streamInfo = null,
        ConversionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        await using var fileStream = File.OpenRead(fullPath);
        var baseInfo = new StreamInfo
        {
            LocalPath = fullPath,
            FileName = Path.GetFileName(fullPath),
            Extension = Path.GetExtension(fullPath),
        };

        var mergedInfo = streamInfo is null ? baseInfo : baseInfo.CopyAndUpdate(streamInfo);
        return await ConvertStreamAsync(fileStream, mergedInfo, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DocumentConverterResult> ConvertUriAsync(
        string uri,
        StreamInfo? streamInfo = null,
        ConversionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        uri = uri.Trim();
        if (uri.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            var (netloc, path) = UriUtilities.FileUriToPath(uri);
            if (!string.IsNullOrEmpty(netloc) && !netloc.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("File URI must point to localhost.");
            }

            return await ConvertLocalAsync(path, streamInfo, options, cancellationToken).ConfigureAwait(false);
        }

        if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var (mime, attributes, data) = UriUtilities.ParseDataUri(uri);
            var info = new StreamInfo
            {
                MimeType = mime,
                Charset = attributes.TryGetValue("charset", out var charset) ? charset : null,
            };

            if (streamInfo is not null)
            {
                info = info.CopyAndUpdate(streamInfo);
            }

            await using var dataStream = new MemoryStream(data);
            return await ConvertStreamAsync(dataStream, info, options, cancellationToken).ConfigureAwait(false);
        }

        if (uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await ConvertResponseAsync(response, streamInfo, options, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"Unsupported URI scheme for '{uri}'.");
    }

    public async Task<DocumentConverterResult> ConvertResponseAsync(
        HttpResponseMessage response,
        StreamInfo? streamInfo = null,
        ConversionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var info = new StreamInfo
        {
            MimeType = response.Content.Headers.ContentType?.MediaType,
            Charset = response.Content.Headers.ContentType?.CharSet,
            FileName = GetFileNameFromHeaders(response.Content.Headers) ?? GetFileNameFromUrl(response.RequestMessage?.RequestUri),
            Extension = GetExtensionFromHeaders(response.Content.Headers) ?? GetExtensionFromUrl(response.RequestMessage?.RequestUri),
            Url = response.RequestMessage?.RequestUri?.ToString(),
        };

        if (streamInfo is not null)
        {
            info = info.CopyAndUpdate(streamInfo);
        }

        await using var buffer = new MemoryStream();
        await response.Content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        buffer.Seek(0, SeekOrigin.Begin);

        return await ConvertStreamAsync(buffer, info, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DocumentConverterResult> ConvertStreamAsync(
        Stream stream,
        StreamInfo? streamInfo = null,
        ConversionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var seekableStream = await EnsureSeekableStreamAsync(stream, cancellationToken).ConfigureAwait(false);
        var info = streamInfo ?? new StreamInfo();
        var guesses = GetStreamInfoGuesses(seekableStream, info);
        var opt = options ?? ConversionOptions.Empty;
        return await ConvertInternalAsync(seekableStream, guesses, opt, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DocumentConverterResult> ConvertInternalAsync(
        Stream stream,
        IReadOnlyList<StreamInfo> guesses,
        ConversionOptions options,
        CancellationToken cancellationToken)
    {
        var attempts = new List<FailedConversionAttempt>();
        var sortedAll = _converters.OrderBy(c => c.Priority).ToList();
        var initialPosition = stream.CanSeek ? stream.Position : 0;

        foreach (var guess in guesses.Concat(new[] { new StreamInfo() }))
        {
            var convertersToTry = GetConvertersForGuess(guess, sortedAll);
            foreach (var registration in convertersToTry)
            {
                var converter = registration.Converter;
                if (!stream.CanSeek)
                {
                    throw new InvalidOperationException("Stream must be seekable.");
                }

                stream.Seek(initialPosition, SeekOrigin.Begin);
                if (!SafeAccepts(converter, stream, guess, options))
                {
                    continue;
                }

                try
                {
                    stream.Seek(initialPosition, SeekOrigin.Begin);
                    var preparedOptions = PrepareOptions(options, guess);
                    var result = await converter.ConvertAsync(stream, guess, preparedOptions, cancellationToken)
                        .ConfigureAwait(false);
                    if (result is not null)
                    {
                        NormalizeMarkdown(result);
                        return result;
                    }
                }
                catch (MissingDependencyException ex)
                {
                    attempts.Add(new FailedConversionAttempt(converter, ex));
                }
                catch (Exception ex)
                {
                    attempts.Add(new FailedConversionAttempt(converter, ex));
                }
            }
        }

        if (attempts.Count > 0)
        {
            throw new FileConversionException(attempts: attempts);
        }

        throw new UnsupportedFormatException("No converters could handle the provided stream.");
    }

    private IReadOnlyList<ConverterRegistration> GetConvertersForGuess(StreamInfo guess, List<ConverterRegistration> sortedAll)
    {
        var normalizedExt = NormalizeExtension(guess.Extension);
        if (string.IsNullOrEmpty(normalizedExt) || !_convertersByExtension.TryGetValue(normalizedExt, out var byExt))
            return sortedAll;

        var byExtOrdered = byExt.OrderBy(c => c.Priority).ToList();
        var set = new HashSet<IDocumentConverter>(byExtOrdered.Select(c => c.Converter));
        var rest = sortedAll.Where(c => !set.Contains(c.Converter)).ToList();
        return byExtOrdered.Concat(rest).ToList();
    }

    private bool SafeAccepts(IDocumentConverter converter, Stream stream, StreamInfo info, ConversionOptions options)
    {
        var position = stream.CanSeek ? stream.Position : 0;
        try
        {
            return converter.Accepts(stream, info, options);
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Seek(position, SeekOrigin.Begin);
            }
        }
    }

    private ConversionOptions PrepareOptions(ConversionOptions options, StreamInfo streamInfo)
    {
        var merged = new ConversionOptions(options.Entries);

        if (_llmClient is not null && !merged.Contains("llm_client"))
        {
            merged.Set("llm_client", _llmClient);
        }
        if (!string.IsNullOrWhiteSpace(_llmModel) && !merged.Contains("llm_model"))
        {
            merged.Set("llm_model", _llmModel);
        }
        if (!string.IsNullOrWhiteSpace(_llmPrompt) && !merged.Contains("llm_prompt"))
        {
            merged.Set("llm_prompt", _llmPrompt);
        }
        if (!string.IsNullOrWhiteSpace(_styleMap) && !merged.Contains("style_map"))
        {
            merged.Set("style_map", _styleMap);
        }
        if (!string.IsNullOrWhiteSpace(_exifToolPath) && !merged.Contains("exiftool_path"))
        {
            merged.Set("exiftool_path", _exifToolPath);
        }
        if (!string.IsNullOrWhiteSpace(_tesseractTessDataPath) && !merged.Contains("tesseract_tessdata_path"))
        {
            merged.Set("tesseract_tessdata_path", _tesseractTessDataPath);
        }
        if (!string.IsNullOrWhiteSpace(_tesseractLang) && !merged.Contains("tesseract_lang"))
        {
            merged.Set("tesseract_lang", _tesseractLang);
        }
        if (_audioTranscriptionService is not null && !merged.Contains("audio_transcription_service"))
        {
            merged.Set("audio_transcription_service", _audioTranscriptionService);
        }

        merged.Set("http_client", _httpClient);
        merged.Set("_parent_converters", _converters.Select(c => c.Converter).ToList());

        if (!merged.Contains("file_extension") && !string.IsNullOrWhiteSpace(streamInfo.Extension))
        {
            merged.Set("file_extension", streamInfo.Extension);
        }

        if (!merged.Contains("url") && !string.IsNullOrWhiteSpace(streamInfo.Url))
        {
            merged.Set("url", streamInfo.Url);
        }

        return merged;
    }

    private List<StreamInfo> GetStreamInfoGuesses(Stream stream, StreamInfo baseInfo)
    {
        var guesses = new List<StreamInfo>();
        var enhanced = EnhanceGuess(baseInfo);

        var detection = DetectMime(stream);
        string? charset = baseInfo.Charset;
        if (detection?.IsText == true && string.IsNullOrWhiteSpace(baseInfo.Charset))
        {
            charset = DetectCharset(stream);
        }

        if (detection is not null)
        {
            // If we have a known extension, prefer it over detected MIME type
            // This is important for formats like DOCX/XLSX/PPTX which are ZIP-based
            var finalExtension = enhanced.Extension ?? detection.Extension;
            var finalMimeType = enhanced.MimeType;
            
            // Only use detected MIME type if we don't have a better one from extension
            if (finalMimeType is null)
            {
                finalMimeType = detection.MimeType;
            }
            // If detected as ZIP but we have a known Office extension, use the proper MIME type
            else if (detection.MimeType == "application/zip" && !string.IsNullOrWhiteSpace(finalExtension))
            {
                var provider = new FileExtensionContentTypeProvider();
                if (provider.TryGetContentType("file" + finalExtension, out var properMime))
                {
                    finalMimeType = properMime;
                }
            }
            
            guesses.Add(
                enhanced.CopyAndUpdate(
                    mimeType: finalMimeType,
                    extension: finalExtension,
                    charset: charset));
        }
        else
        {
            guesses.Add(enhanced);
        }

        return guesses;
    }

    private static StreamInfo EnhanceGuess(StreamInfo info)
    {
        var provider = new FileExtensionContentTypeProvider();
        var mime = info.MimeType;
        if (mime is null && !string.IsNullOrWhiteSpace(info.Extension) &&
            provider.TryGetContentType("file" + info.Extension, out var guessedMime))
        {
            mime = guessedMime;
        }

        if (info.Extension is null && !string.IsNullOrWhiteSpace(info.MimeType))
        {
            info = info.CopyAndUpdate(extension: GuessExtensionFromMime(info.MimeType));
        }

        return info.CopyAndUpdate(mimeType: mime);
    }

    private static string? GuessExtensionFromMime(string mimeType) =>
        mimeType switch
        {
            "application/pdf" => ".pdf",
            "text/html" => ".html",
            "application/zip" => ".zip",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ".pptx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            "text/csv" => ".csv",
            _ => null,
        };

    private static string? DetectCharset(Stream stream)
    {
        var position = stream.Position;
        var buffer = new byte[Math.Min(4096, (int)(stream.Length - position))];
        stream.Read(buffer, 0, buffer.Length);
        stream.Seek(position, SeekOrigin.Begin);

        var detector = new CharsetDetector();
        detector.Feed(buffer, 0, buffer.Length);
        detector.DataEnd();
        return detector.Charset;
    }

    private MimeDetectionResult? DetectMime(Stream stream)
    {
        var position = stream.Position;
        var buffer = new byte[Math.Min(512, (int)(stream.Length - position))];
        stream.Read(buffer, 0, buffer.Length);
        stream.Seek(position, SeekOrigin.Begin);

        if (buffer.Length >= 4 && buffer[0] == 0x25 && buffer[1] == 0x50 && buffer[2] == 0x44 && buffer[3] == 0x46)
        {
            return new MimeDetectionResult("application/pdf", ".pdf", false);
        }

        if (buffer.Length >= 4 && buffer[0] == 0x50 && buffer[1] == 0x4B)
        {
            return new MimeDetectionResult("application/zip", ".zip", false);
        }

        var sample = Encoding.UTF8.GetString(buffer);
        if (Regex.IsMatch(sample, @"<html", RegexOptions.IgnoreCase))
        {
            return new MimeDetectionResult("text/html", ".html", true);
        }

        if (sample.TrimStart().StartsWith("{") || sample.TrimStart().StartsWith("["))
        {
            return new MimeDetectionResult("application/json", ".json", true);
        }

        if (IsProbablyText(buffer))
        {
            return new MimeDetectionResult("text/plain", ".txt", true);
        }

        return null;
    }

    private static bool IsProbablyText(byte[] buffer)
    {
        var printable = buffer.Count(b => b is >= 32 and <= 126 or (>= 9 and <= 13));
        return printable / (double)buffer.Length > 0.8;
    }

    private static async Task<Stream> EnsureSeekableStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
        memory.Seek(0, SeekOrigin.Begin);
        return memory;
    }

    private static void NormalizeMarkdown(DocumentConverterResult result)
    {
        var lines = result.Markdown.Split('\n');
        lines = lines.Select(line => line.TrimEnd()).ToArray();
        var text = string.Join("\n", lines);
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        result.Markdown = text.Trim();
    }

    private static string? GetFileNameFromHeaders(HttpContentHeaders headers)
    {
        var disposition = headers.ContentDisposition;
        if (disposition is null || string.IsNullOrWhiteSpace(disposition.FileNameStar))
        {
            return disposition?.FileName?.Trim('"');
        }

        return disposition.FileNameStar;
    }

    private static string? GetExtensionFromHeaders(HttpContentHeaders headers)
    {
        var fileName = GetFileNameFromHeaders(headers);
        return fileName is null ? null : Path.GetExtension(fileName);
    }

    private static string? GetFileNameFromUrl(System.Uri? uri)
    {
        if (uri is null)
        {
            return null;
        }

        return Path.GetFileName(uri.AbsolutePath);
    }

    private static string? GetExtensionFromUrl(System.Uri? uri)
    {
        var name = GetFileNameFromUrl(uri);
        return name is null ? null : Path.GetExtension(name);
    }

    private string? ResolveExifToolPath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath;
        }

        var envPath = Environment.GetEnvironmentVariable("EXIFTOOL_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            return envPath;
        }

        var candidate = SearchPath("exiftool");
        return candidate;
    }

    private static string? SearchPath(string executable)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
        {
            return null;
        }

        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var potential = Path.Combine(dir, executable);
            if (File.Exists(potential))
            {
                return potential;
            }
            if (OperatingSystem.IsWindows())
            {
                var exe = potential + ".exe";
                if (File.Exists(exe))
                {
                    return exe;
                }
            }
        }

        return null;
    }

    private sealed record ConverterRegistration(IDocumentConverter Converter, double Priority);

    private sealed record MimeDetectionResult(string MimeType, string? Extension, bool IsText);
}

