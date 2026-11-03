using System.Text;

namespace MpcSubtitle.Core;

/// <summary>Serializes caption segments to the SRT (SubRip) text format.</summary>
public static class SubRipWriter
{
    public static void Write(string outPath, IReadOnlyList<SubtitleSegment> segments)
    {
        var sb = new StringBuilder();
        var index = 1;

        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment.Text))
            {
                continue; // empty cues are not valid/silent; skip them
            }

            var (start, end) = Normalize(segment.Start, segment.End);
            sb.Append(index++).Append('\n');
            sb.Append(FormatTimestamp(start)).Append(" --> ").Append(FormatTimestamp(end)).Append('\n');
            sb.Append(segment.Text.Replace("\r\n", "\n", StringComparison.Ordinal)
                                  .Replace('\r', '\n')).Append('\n').Append('\n');
        }

        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>Formats a time as SRT timestamp "HH:mm:ss,mmm".</summary>
    public static string FormatTimestamp(TimeSpan t)
    {
        return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00},{t.Milliseconds:000}";
    }

    private static (TimeSpan Start, TimeSpan End) Normalize(TimeSpan start, TimeSpan end)
    {
        if (start > end)
        {
            (start, end) = (end, start); // repair inverted bounds
        }

        if (start < TimeSpan.Zero)
        {
            start = TimeSpan.Zero;
        }

        if (end < TimeSpan.Zero)
        {
            end = TimeSpan.Zero;
        }

        if (end <= start)
        {
            end = start.Add(TimeSpan.FromMilliseconds(1));
        }

        return (start, end);
    }
}
