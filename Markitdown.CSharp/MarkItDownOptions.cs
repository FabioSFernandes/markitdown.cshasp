using MarkItDown.CSharp.Interfaces;

namespace MarkItDown.CSharp;

public sealed class MarkItDownOptions
{
    public HttpClient? HttpClient { get; init; }

    public bool? EnableBuiltins { get; init; }

    public bool? EnablePlugins { get; init; }

    public IEnumerable<IMarkItDownPlugin>? Plugins { get; init; }

    public ILlmClient? LlmClient { get; init; }

    public string? LlmModel { get; init; }

    public string? LlmPrompt { get; init; }

    public string? ExifToolPath { get; init; }

    /// <summary>
    /// Path to the folder that contains the "tessdata" directory (with e.g. eng.traineddata).
    /// Same as Tesseract's TESSDATA_PREFIX. When set, image conversion will run OCR on images.
    /// </summary>
    public string? TesseractTessDataPath { get; init; }

    /// <summary>
    /// Tesseract language code (e.g. "eng", "por"). Default is "eng" when not set.
    /// </summary>
    public string? TesseractLang { get; init; }

    public string? StyleMap { get; init; }

    public IAudioTranscriptionService? AudioTranscriptionService { get; init; }
}

