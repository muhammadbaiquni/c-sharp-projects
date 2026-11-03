using Whisper.net;

namespace MpcSubtitle.Core;

/// <summary>
/// Loads a whisper model once and reuses it to transcribe many audio buffers.
/// Dispose to release native resources.
/// </summary>
public sealed class TranscriptionSession : IDisposable
{
    private readonly WhisperFactory _factory;

    public TranscriptionSession(string modelPath)
    {
        ArgumentNullException.ThrowIfNull(modelPath);
        _factory = WhisperFactory.FromPath(modelPath);
    }

    public async Task<IReadOnlyList<SubtitleSegment>> TranscribeAsync(
        float[] samples,
        string? language,
        Action<SubtitleSegment>? onSegment = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Length == 0)
        {
            return Array.Empty<SubtitleSegment>();
        }

        var builder = _factory.CreateBuilder();
        if (string.IsNullOrWhiteSpace(language) || language.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            builder.WithLanguageDetection();
        }
        else
        {
            builder.WithLanguage(language);
        }

        using var processor = builder.Build();

        var segments = new List<SubtitleSegment>();
        await foreach (var segment in processor.ProcessAsync(samples, cancellationToken))
        {
            var item = new SubtitleSegment(segment.Start, segment.End, segment.Text.Trim());
            segments.Add(item);
            onSegment?.Invoke(item);
        }

        return segments;
    }

    public void Dispose() => _factory.Dispose();
}
