using MpcSubtitle.Core;
using Xunit;

namespace MpcSubtitle.Core.Tests;

public class FolderExplorerTests
{
    private static string NewTempDir()
        => Path.Combine(Path.GetTempPath(), "mpcexplorr-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ListSubdirectories_ReturnsSortedDirectoriesOnly()
    {
        var root = NewTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "b"));
            Directory.CreateDirectory(Path.Combine(root, "a"));
            Directory.CreateDirectory(Path.Combine(root, "c"));
            File.WriteAllText(Path.Combine(root, "note.txt"), "x");

            var dirs = FolderExplorer.ListSubdirectories(root);

            Assert.Equal(3, dirs.Count);
            Assert.Equal(new[] { "a", "b", "c" }, dirs.Select(Path.GetFileName).ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ListSubdirectories_ReturnsEmpty_ForMissingDirectory()
    {
        var missing = Path.Combine(Path.GetTempPath(), "mpcexplorr-" + Guid.NewGuid().ToString("N"));
        Assert.Empty(FolderExplorer.ListSubdirectories(missing));
    }

    [Fact]
    public void ListSubdirectories_RespectsMaxEntries()
    {
        var root = NewTempDir();
        try
        {
            for (var i = 0; i < 5; i++)
            {
                Directory.CreateDirectory(Path.Combine(root, "d" + i));
            }

            Assert.Equal(3, FolderExplorer.ListSubdirectories(root, maxEntries: 3).Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
