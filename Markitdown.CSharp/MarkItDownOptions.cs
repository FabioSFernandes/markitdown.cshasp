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

    public string? StyleMap { get; init; }

    public IAudioTranscriptionService? AudioTranscriptionService { get; init; }
}

