namespace MpcplBuilder.Application.Abstractions;

public sealed class PlaylistOverwriteRequiredException(string outputPath, Exception innerException)
    : IOException($"Playlist already exists: {outputPath}", innerException);
