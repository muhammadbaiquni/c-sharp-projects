using MpcSubtitle.Core;
using Xunit;

namespace MpcSubtitle.Core.Tests;

public class WavReaderTests
{
    private static byte[] BuildWav(IReadOnlyList<short[]> channels, int sampleRate = 16000)
    {
        var channelCount = channels.Count;
        var frameCount = channels[0].Length;
        var dataLength = frameCount * channelCount * 2;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII);

        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataLength);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16); // fmt chunk size
        writer.Write((short)1); // PCM
        writer.Write((short)channelCount);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channelCount * 2); // byte rate
        writer.Write((short)(channelCount * 2)); // block align
        writer.Write((short)16); // bits per sample
        writer.Write("data"u8.ToArray());
        writer.Write(dataLength);

        for (var f = 0; f < frameCount; f++)
        {
            for (var c = 0; c < channelCount; c++)
            {
                writer.Write(channels[c][f]);
            }
        }

        writer.Flush();
        return stream.ToArray();
    }

    [Fact]
    public void Parse_DecodesMonoPcmToNormalizedFloat()
    {
        var wav = BuildWav(new[] { new short[] { 0, 32767, -32768, 16384 } });
        var samples = WavReader.Parse16kPcmMono(wav);

        Assert.Equal(4, samples.Length);
        Assert.Equal(0f, samples[0], 4);
        Assert.Equal(32767f / 32768f, samples[1], 5);
        Assert.Equal(-1f, samples[2], 4);
        Assert.Equal(0.5f, samples[3], 4);
    }

    [Fact]
    public void Parse_AveragesStereoChannels()
    {
        var wav = BuildWav(new[] { new short[] { 1000, 1000 }, new short[] { 2000, 2000 } });
        var samples = WavReader.Parse16kPcmMono(wav);

        Assert.Equal(2, samples.Length);
        Assert.Equal(1500f / 32768f, samples[0], 5);
        Assert.Equal(1500f / 32768f, samples[1], 5);
    }

    [Fact]
    public void Parse_RejectsWrongSampleRate()
    {
        var wav = BuildWav(new[] { new short[] { 0, 0 } }, sampleRate: 44100);
        Assert.Throws<InvalidDataException>(() => WavReader.Parse16kPcmMono(wav));
    }

    [Fact]
    public void Parse_RejectsNonPcmFormatTag()
    {
        var wav = BuildWav(new[] { new short[] { 0, 0 } });
        // Corrupt the format tag (offset 20) to a non-PCM value.
        wav[20] = 3;
        Assert.Throws<InvalidDataException>(() => WavReader.Parse16kPcmMono(wav));
    }
}
