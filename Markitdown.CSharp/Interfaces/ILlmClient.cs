namespace MarkItDown.CSharp.Interfaces;

public interface ILlmClient
{
    Task<string?> CreateCaptionAsync(LlmCaptionRequest request, CancellationToken cancellationToken = default);
}

public sealed record LlmCaptionRequest(
    Stream Content,
    StreamInfo StreamInfo,
    string Model,
    string Prompt);

