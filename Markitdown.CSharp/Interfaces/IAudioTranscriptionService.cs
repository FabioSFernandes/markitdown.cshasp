namespace MarkItDown.CSharp.Interfaces;

public interface IAudioTranscriptionService
{
    Task<string?> TranscribeAsync(Stream audioStream, string audioFormat, CancellationToken cancellationToken = default);
}

