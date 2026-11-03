using System.Collections.ObjectModel;
using FluentAssertions;
using MpcplBuilder.Application.Explorer;
using MpcplBuilder.Wpf.Behaviors;
using MpcplBuilder.Wpf.Tests.Fakes;
using MpcplBuilder.Wpf.ViewModels;

namespace MpcplBuilder.Wpf.Tests;

public sealed class TreeViewSelectionTests
{
    [Fact]
    public void ApplySelection_ControlClickAddsAndRemovesFolder()
    {
        var first = Folder(@"D:\First");
        var second = Folder(@"D:\Second");
        var selected = new ObservableCollection<FolderNodeViewModel> { first };
        var anchor = TreeViewSelection.ApplySelection(selected, [first, second], second, first,
            control: true, shift: false);

        selected.Should().Equal(first, second);
        anchor.Should().BeSameAs(second);

        anchor = TreeViewSelection.ApplySelection(selected, [first, second], second, anchor,
            control: true, shift: false);

        selected.Should().Equal(first);
        anchor.Should().BeSameAs(second);
    }

    [Fact]
    public void ApplySelection_ShiftClickSelectsVisibleRangeFromAnchor()
    {
        var first = Folder(@"D:\First");
        var second = Folder(@"D:\Second");
        var third = Folder(@"D:\Third");
        var fourth = Folder(@"D:\Fourth");
        var selected = new ObservableCollection<FolderNodeViewModel> { second };

        var anchor = TreeViewSelection.ApplySelection(selected, [first, second, third, fourth], fourth, second,
            control: false, shift: true);

        selected.Should().Equal(second, third, fourth);
        anchor.Should().BeSameAs(second);
    }

    private static FolderNodeViewModel Folder(string path) =>
        new(new ExplorerFolder(path, Path.GetFileName(path), false, true), new FakeLoadExplorerChildren());
}
