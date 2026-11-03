namespace MpcSubtitle.Core;

/// <summary>A recognized caption segment ready for timed-text output.</summary>
public sealed record SubtitleSegment(TimeSpan Start, TimeSpan End, string Text);
