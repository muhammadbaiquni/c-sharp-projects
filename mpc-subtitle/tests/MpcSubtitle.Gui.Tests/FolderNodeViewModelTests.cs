using MpcSubtitle.Core;
using MpcSubtitle.Gui.ViewModels;
using Xunit;

namespace MpcSubtitle.Gui.Tests;

public class FolderNodeViewModelTests
{
    private static string NewTempDir()
        => Path.Combine(Path.GetTempPath(), "mpcnode-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Expand_RepopulatesChildrenWithSubdirectories()
    {
        var root = NewTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "one"));
            Directory.CreateDirectory(Path.Combine(root, "two"));
            Directory.CreateDirectory(Path.Combine(root, "three"));
            File.WriteAllText(Path.Combine(root, "movie.mp4"), "x");
            File.WriteAllText(Path.Combine(root, "movie.srt"), "s");

            var node = FolderNodeViewModel.ForDirectory(root);
            Assert.Single(node.Children); // only the "…" placeholder before expansion
            Assert.Equal(SubtitleCoverage.All, node.Coverage);

            node.IsExpanded = true; // the same trigger the TreeViewItem binding fires

            Assert.Equal(3, node.Children.Count);
            Assert.Equal(new[] { "one", "three", "two" }, node.Children.Select(c => c.Name).ToArray());
            Assert.All(node.Children, child =>
            {
                Assert.NotNull(child.FullPath);
                Assert.Single(child.Children); // each child carries its own placeholder
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Expand_DoesNotDuplicateChildrenAcrossCollapses()
    {
        var root = NewTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "a"));
            Directory.CreateDirectory(Path.Combine(root, "b"));

            var node = FolderNodeViewModel.ForDirectory(root);
            node.IsExpanded = true;
            node.IsExpanded = false;
            node.IsExpanded = true;

            Assert.Equal(2, node.Children.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
