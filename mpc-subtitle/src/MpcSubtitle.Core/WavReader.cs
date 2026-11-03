using System.Buffers.Binary;

namespace MpcSubtitle.Core;

/// <summary>
/// Reads a 16-bit PCM WAV and returns normalized mono samples at the whisper-compatible
/// 16 kHz sample rate. Channel count other than stereo/mono and unsupported formats are rejected.
/// </summary>
public static class WavReader
{
    public const int WhisperSampleRate = 16000;

    public static float[] Read16kPcmMono(string path)
    {
        return Parse16kPcmMono(File.ReadAllBytes(path));
    }

    public static float[] Parse16kPcmMono(ReadOnlySpan<byte> wav)
    {
        if (wav.Length < 44)
        {
            throw new InvalidDataException("File too small to be a RIFF/WAVE file.");
        }

        if (!wav[0..4].SequenceEqual("RIFF"u8) || !wav[8..12].SequenceEqual("WAVE"u8))
        {
            throw new InvalidDataException("Not a RIFF/WAVE file.");
        }

        ushort format = 0;
        ushort channels = 0;
        ushort bits = 0;
        uint sampleRate = 0;
        ReadOnlySpan<byte> data = default;
        var offset = 12;

        while (offset + 8 <= wav.Length)
        {
            var id = wav.Slice(offset, 4);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(wav.Slice(offset + 4, 4));
            if (size > int.MaxValue)
            {
                throw new InvalidDataException("WAV chunk too large.");
            }

            var body = offset + 8;
            if ((long)body + size > wav.Length)
            {
                break; // truncated chunk; stop scanning cleanly
            }

            if (id.SequenceEqual("fmt "u8) && size >= 16)
            {
                format = BinaryPrimitives.ReadUInt16LittleEndian(wav.Slice(body, 2));
                channels = BinaryPrimitives.ReadUInt16LittleEndian(wav.Slice(body + 2, 2));
                sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(wav.Slice(body + 4, 4));
                bits = BinaryPrimitives.ReadUInt16LittleEndian(wav.Slice(body + 14, 2));
            }
            else if (id.SequenceEqual("data"u8))
            {
                data = wav.Slice(body, (int)size);
            }

            offset = body + (int)size + ((int)size % 2); // chunks are word-aligned
        }

        if (format != 1)
        {
            throw new InvalidDataException($"Unsupported WAV format tag {format}; expected PCM (1).");
        }

        if (bits != 16)
        {
            throw new InvalidDataException($"Unsupported bit depth {bits}; expected 16-bit PCM.");
        }

        if (sampleRate != WhisperSampleRate)
        {
            throw new InvalidDataException(
                $"Expected {WhisperSampleRate} Hz mono, got {sampleRate} Hz. Resample first (ffmpeg -ar {WhisperSampleRate}).");
        }

        if (channels == 0 || data.IsEmpty)
        {
            throw new InvalidDataException("WAV has no audio samples.");
        }

        var frameBytes = channels * 2;
        var frameCount = data.Length / frameBytes;
        var samples = new float[frameCount];

        for (var i = 0; i < frameCount; i++)
        {
            var sum = 0;
            for (var c = 0; c < channels; c++)
            {
                sum += BinaryPrimitives.ReadInt16LittleEndian(data.Slice(i * frameBytes + c * 2, 2));
            }

            samples[i] = sum / (float)channels / 32768f; // average channels, normalize s16 -> [-1, 1]
        }

        return samples;
    }
}
