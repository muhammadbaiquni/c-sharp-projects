namespace MpcSubtitle.Core;

/// <summary>
/// Full per-video pipeline: extract 16 kHz mono audio with ffmpeg, transcribe with a shared
/// session, and write the SRT next to the video. Returns the number of written cues.
/// </summary>
public static class SubtitleGenerator
{
    public static async Task<int> GenerateForFileAsync(
        string videoPath,
        string srtPath,
        string ffmpegPath,
        TranscriptionSession session,
        string? language,
        Action<SubtitleSegment>? onSegment = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(videoPath);
        ArgumentNullException.ThrowIfNull(srtPath);
        ArgumentNullException.ThrowIfNull(ffmpegPath);
        ArgumentNullException.ThrowIfNull(session);

        var tempWav = Path.Combine(Path.GetTempPath(), $"mpc-subtitle-{Guid.NewGuid():N}.wav");
        try
        {
            await FfmpegAudioExtractor.ExtractToWavAsync(ffmpegPath, videoPath, tempWav, cancellationToken).ConfigureAwait(false);
            var samples = WavReader.Read16kPcmMono(tempWav);
            var segments = await session.TranscribeAsync(samples, language, onSegment, cancellationToken).ConfigureAwait(false);
            SubRipWriter.Write(srtPath, segments);
            return segments.Count;
        }
        finally
        {
            try
            {
                if (File.Exists(tempWav))
                {
                    File.Delete(tempWav);
                }
            }
            catch
            {
                // Best-effort cleanup of the temporary audio.
            }
        }
    }
}
