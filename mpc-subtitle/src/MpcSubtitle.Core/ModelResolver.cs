using Whisper.net.Ggml;

namespace MpcSubtitle.Core;

/// <summary>
/// Resolves a model name or file path into a local GGML model file,
/// downloading (and caching) models from the whisper.net Hugging Face repo on first use.
/// </summary>
public static class ModelResolver
{
    private static readonly Dictionary<string, (GgmlType Type, string FileName)> KnownModels =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["tiny"] = (GgmlType.Tiny, "ggml-tiny.bin"),
            ["tiny.en"] = (GgmlType.TinyEn, "ggml-tiny.en.bin"),
            ["base"] = (GgmlType.Base, "ggml-base.bin"),
            ["base.en"] = (GgmlType.BaseEn, "ggml-base.en.bin"),
            ["small"] = (GgmlType.Small, "ggml-small.bin"),
            ["small.en"] = (GgmlType.SmallEn, "ggml-small.en.bin"),
            ["medium"] = (GgmlType.Medium, "ggml-medium.bin"),
            ["medium.en"] = (GgmlType.MediumEn, "ggml-medium.en.bin"),
            ["large"] = (GgmlType.LargeV3, "ggml-large-v3.bin"),
            ["large-v1"] = (GgmlType.LargeV1, "ggml-large-v1.bin"),
            ["large-v2"] = (GgmlType.LargeV2, "ggml-large-v2.bin"),
            ["large-v3"] = (GgmlType.LargeV3, "ggml-large-v3.bin"),
            ["large-v3-turbo"] = (GgmlType.LargeV3Turbo, "ggml-large-v3-turbo.bin"),
        };

    public static IEnumerable<string> KnownNames => KnownModels.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase);

    public static string DefaultCacheDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MpcSubtitle", "models");

    public static async Task<string> ResolveAsync(
        string modelArg,
        string? cacheDir = null,
        Action<string>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelArg))
        {
            throw new ArgumentException("Model must be a known name (tiny/base/small/medium/large) or a path to a .bin file.");
        }

        // A path that already exists wins over names.
        if (File.Exists(modelArg))
        {
            return Path.GetFullPath(modelArg);
        }

        if (!KnownModels.TryGetValue(modelArg.Trim(), out var known))
        {
            throw new ArgumentException(
                $"Unknown model '{modelArg}' and no such file. Known names: {string.Join(", ", KnownNames)}");
        }

        var dir = cacheDir ?? DefaultCacheDir;
        Directory.CreateDirectory(dir);
        var target = Path.Combine(dir, known.FileName);

        if (File.Exists(target) && new FileInfo(target).Length > 0)
        {
            onProgress?.Invoke($"Using cached model {known.FileName}");
            return target;
        }

        onProgress?.Invoke($"Downloading {known.FileName} (one-time, ~50–3000 MB depending on size)...");
        var tmp = target + ".download";
        long total = 0;

        try
        {
            {
                await using var source = await WhisperGgmlDownloader.Default
                    .GetGgmlModelAsync(known.Type, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                await using var destination = new FileStream(
                    tmp, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1 << 16, useAsync: true);

                var buffer = new byte[1 << 16];
                var sinceReport = 0L;

                while (true)
                {
                    var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    total += read;
                    sinceReport += read;

                    if (sinceReport >= 8 * 1024 * 1024)
                    {
                        onProgress?.Invoke($"  ... {total / (1024 * 1024)} MiB downloaded");
                        sinceReport = 0;
                    }
                }

                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            } // both streams disposed before the move

            File.Move(tmp, target, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(tmp);
            }
            catch
            {
                // Best-effort cleanup of a partial download.
            }

            throw;
        }

        onProgress?.Invoke($"Model saved to {target} ({total / (1024 * 1024)} MiB)");
        return target;
    }
}
