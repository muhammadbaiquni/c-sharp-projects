using System.Diagnostics;

namespace MpcSubtitle.Core;

/// <summary>Locates and launches MPC-HC with an external subtitle file.</summary>
public static class MpcLauncher
{
    /// <summary>Locates the MPC-HC executable or null when not found.</summary>
    public static string? LocateMpcHc(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            var full = Path.GetFullPath(explicitPath);
            return File.Exists(full) ? full : null;
        }

        return CandidatePaths().FirstOrDefault(File.Exists);
    }

    public static IEnumerable<string> CandidatePaths()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        };

        foreach (var root in roots.Where(r => !string.IsNullOrEmpty(r)))
        {
            yield return Path.Combine(root, "MPC-HC", "mpc-hc64.exe");
            yield return Path.Combine(root, "MPC-HC", "mpc-hc.exe");
        }
    }

    /// <summary>
    /// Launches MPC-HC playing <paramref name="video"/> with <paramref name="srtPath"/> loaded.
    /// Returns false when the process could not be started.
    /// </summary>
    public static bool Launch(string mpcPath, string video, string srtPath)
    {
        var videoFull = Path.GetFullPath(video);
        var psi = new ProcessStartInfo
        {
            FileName = mpcPath,
            UseShellExecute = false,
            CreateNoWindow = false,
            WorkingDirectory = Path.GetDirectoryName(videoFull) ?? "",
        };

        psi.ArgumentList.Add(videoFull);
        psi.ArgumentList.Add("/sub");
        psi.ArgumentList.Add(srtPath);

        return Process.Start(psi) is not null;
    }
}
