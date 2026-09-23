using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using FluentAssertions;
using MpcplBuilder.Application.Explorer;
using MpcplBuilder.Wpf.Tests.Fakes;
using MpcplBuilder.Wpf.ViewModels;
using MpcplBuilder.Wpf.Views;

namespace MpcplBuilder.Wpf.Tests;

public sealed class MainWindowBindingTests
{
    [Fact]
    public void MainWindow_PrimaryControlsHaveBindingsAndAccessibleNames() => RunOnSta(() =>
    {
        var view = new DefaultView { DataContext = CreateDefault() };
        var window = new Window { Content = view };
        try
        {
            window.Show();
            ProcessDispatcher();
            AssertBinding<TextBox>(view, "RootTextBox", TextBox.TextProperty, "RootPath");
            AssertBinding<Button>(view, "BrowseButton", Button.CommandProperty, "BrowseCommand");
            AssertBinding<Button>(view, "GenerateButton", Button.CommandProperty, "GenerateCommand");
            AssertBinding<ListBox>(view, "PreviewListBox", ItemsControl.ItemsSourceProperty, "PreviewItems");
            AssertBinding<TextBlock>(view, "StatusTextBlock", TextBlock.TextProperty, "Status");
            AssertAccessible(view, "RootTextBox", "BrowseButton", "GenerateButton", "ClearButton", "CancelButton");
        }
        finally { window.Close(); }
    });

    [Fact]
    public void MainWindow_ExplorerBindsToolbarTreeSelectionAndLazyExpansionWithoutBindingErrors() => RunOnSta(() =>
    {
        var roots = new FakeLoadExplorerRoots
        {
            Handler = _ => Task.FromResult(new ExplorerLoadResult(ExplorerLoadStatus.Success,
                [new ExplorerFolder(@"C:\", "Local Disk (C:)", false, true)], null))
        };
        var children = new FakeLoadExplorerChildren
        {
            Handler = (_, _) => Task.FromResult(new ExplorerLoadResult(ExplorerLoadStatus.Success,
                [new ExplorerFolder(@"C:\Music", "Music", true, true),
                 new ExplorerFolder(@"C:\Unavailable", "Unavailable", false, false)], null))
        };
        var explorer = new ExplorerViewModel(roots, children, new FakeInspectPlaylistPresence(),
            new FakeGeneratePlaylist(), new FakeUserDialogService());
        var shell = new MainWindowViewModel(CreateDefault(), explorer);
        var window = new MainWindow { DataContext = shell };
        window.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/MpcplBuilder.Wpf;component/Styles/Controls.xaml", UriKind.Relative)
        });
        try
        {
            window.Show();
            ProcessDispatcher();
            var tabs = (TabControl)window.FindName("ShellTabs");
            shell.SelectedTabIndex.Should().Be(1);
            tabs.SelectedIndex.Should().Be(1);
            ((TabItem)tabs.Items[0]).Header.Should().Be("Explorer");
            ((TabItem)tabs.Items[1]).Header.Should().Be("Default");
            ((TabItem)tabs.Items[1]).Content.Should().BeOfType<DefaultView>();
            tabs.SelectedIndex = 0;
            ProcessDispatcher();
            shell.SelectedTabIndex.Should().Be(0);
            var view = (FrameworkElement)((TabItem)tabs.Items[0]).Content;
            view.GetType().Name.Should().Be("ExplorerView");
            view.DataContext.Should().BeSameAs(explorer);
            var tree = AssertBinding<TreeView>(view, "FoldersTreeView", ItemsControl.ItemsSourceProperty, "Roots");
            var relative = AssertBinding<RadioButton>(view, "RelativePathRadioButton", ToggleButton.IsCheckedProperty, "IsRelativePath");
            var full = AssertBinding<RadioButton>(view, "FullPathRadioButton", ToggleButton.IsCheckedProperty, "IsFullPath");
            var longPath = AssertBinding<RadioButton>(view, "LongPathRadioButton", ToggleButton.IsCheckedProperty, "IsLongPath");
            var generate = AssertBinding<Button>(view, "GenerateButton", Button.CommandProperty, "GenerateCommand");
            var refresh = AssertBinding<Button>(view, "RefreshButton", Button.CommandProperty, "RefreshCommand");
            var status = AssertBinding<TextBlock>(view, "StatusTextBlock", TextBlock.TextProperty, "Status");
            AssertAccessible(view, "FoldersTreeView", "RelativePathRadioButton", "FullPathRadioButton", "LongPathRadioButton", "GenerateButton", "RefreshButton");
            relative.IsChecked.Should().BeTrue();
            full.IsChecked = true;
            explorer.IsFullPath.Should().BeTrue();
            explorer.IsRelativePath.Should().BeFalse();
            longPath.IsChecked = true;
            explorer.IsLongPath.Should().BeTrue();
            explorer.IsFullPath.Should().BeFalse();
            relative.IsChecked = true;
            generate.Command.Should().BeSameAs(explorer.GenerateCommand);
            refresh.Command.Should().BeSameAs(explorer.RefreshCommand);
            generate.IsEnabled.Should().BeFalse();
            explorer.Status = "Test status";
            ProcessDispatcher();
            status.Text.Should().Be("Test status");

            var root = explorer.Roots.Single();
            var drive = root.Children.Single();
            tree.ItemsSource.Should().BeSameAs(explorer.Roots);
            var rootItem = Container(tree, root);
            rootItem.IsExpanded.Should().BeTrue();
            var driveItem = Container(rootItem, drive);
            driveItem.IsExpanded = true;
            ProcessDispatcher();
            drive.IsExpanded.Should().BeTrue();
            drive.Children.Select(n => n.DisplayName).Should().Equal("Music", "Unavailable");
            var music = drive.Children[0];
            var musicItem = Container(driveItem, music);
            musicItem.IsSelected = true;
            ProcessDispatcher();
            explorer.SelectedFolder.Should().BeSameAs(music);
            generate.IsEnabled.Should().BeTrue();
            AssertFolderVisual(musicItem, "Music", "Playlist present", Colors.ForestGreen);
            AssertFolderVisual(driveItem, "Local Disk (C:)", "No playlist", Colors.Goldenrod);
            drive.HasPlaylist = true;
            ProcessDispatcher();
            AssertFolderVisual(driveItem, "Local Disk (C:)", "Playlist present", Colors.ForestGreen);
            AutomationProperties.GetHelpText(driveItem).Should().Be("Playlist present");
            driveItem.ToolTip.ToString().Should().NotContain("No playlist");
            var unavailableItem = Container(driveItem, drive.Children[1]);
            AutomationProperties.GetHelpText(unavailableItem).Should().Contain("Folder unavailable");
            explorer.SelectedFolder = drive;
            ProcessDispatcher();
            tree.SelectedItem.Should().BeSameAs(drive);
            musicItem.IsSelected.Should().BeFalse();
            driveItem.IsSelected.Should().BeTrue();

            music.HasPlaylist = false;
            ProcessDispatcher();
            AssertFolderVisual(musicItem, "Music", "No playlist", Colors.Goldenrod);
            unavailableItem.IsSelected = true;
            ProcessDispatcher();
            generate.IsEnabled.Should().BeFalse();
            rootItem.IsSelected = true;
            ProcessDispatcher();
            generate.IsEnabled.Should().BeFalse();
            musicItem.IsSelected = true;
            ProcessDispatcher();

            // Refresh replaces node instances; the restored VM selection must be visibly selected too.
            refresh.Command!.Execute(null);
            ProcessDispatcher();
            tree.SelectedItem.Should().BeSameAs(explorer.SelectedFolder);
            explorer.SelectedFolder!.FullPath.Should().Be(@"C:\Music");
            rootItem = Container(tree, explorer.Roots.Single());
            driveItem = Container(rootItem, explorer.Roots.Single().Children.Single());
            driveItem.IsExpanded.Should().BeTrue();
            Container(driveItem, explorer.SelectedFolder).IsSelected.Should().BeTrue();
            driveItem.IsExpanded = false;
            ProcessDispatcher();
            explorer.Roots.Single().Children.Single().IsExpanded.Should().BeFalse();
            explorer.SelectedFolder = null;
            ProcessDispatcher();
            tree.SelectedItem.Should().BeNull();
        }
        finally { window.Close(); }
    });

    private static DefaultViewModel CreateDefault() => new(new FakeInspectFolder(), new FakeGeneratePlaylist(),
        new FakeFolderPicker(), new FakeUserDialogService());

    private static TreeViewItem Container(ItemsControl parent, object item) =>
        (TreeViewItem)parent.ItemContainerGenerator.ContainerFromItem(item);

    private static void AssertFolderVisual(TreeViewItem item, string name, string status, Color color)
    {
        AutomationProperties.GetName(item).Should().Contain(name).And.Contain(status);
        // The header has both a folder silhouette and text, so playlist status does not rely on color.
        var header = Descendants(item).OfType<ContentPresenter>().First(p => p.Content == item.Header);
        Descendants(header).OfType<TextBlock>().Should().Contain(t => t.Text == status);
        var icon = Descendants(header).OfType<System.Windows.Shapes.Path>().Single();
        icon.Data.Should().NotBeNull();
        ((SolidColorBrush)icon.Fill).Color.Should().Be(color);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void AssertAccessible(FrameworkElement view, params string[] names)
    {
        foreach (var name in names)
            AutomationProperties.GetName((DependencyObject)view.FindName(name)).Should().NotBeNullOrWhiteSpace();
    }

    private static T AssertBinding<T>(FrameworkElement view, string name, DependencyProperty property, string path)
        where T : FrameworkElement
    {
        var control = (T)view.FindName(name);
        control.Should().NotBeNull($"{name} must be present");
        var binding = BindingOperations.GetBindingExpression(control, property);
        binding.Should().NotBeNull();
        binding!.ParentBinding.Path.Path.Should().Be(path);
        return control;
    }

    private static void ProcessDispatcher() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static void RunOnSta(Action test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var source = PresentationTraceSources.DataBindingSource;
            var oldLevel = source.Switch.Level;
            using var listener = new BindingErrorListener();
            source.Listeners.Add(listener);
            source.Switch.Level = SourceLevels.Error;
            try
            {
                test();
                ProcessDispatcher();
                listener.Errors.Should().BeEmpty("all rendered bindings must resolve after dispatcher processing");
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                source.Listeners.Remove(listener);
                source.Switch.Level = oldLevel;
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure.Should().BeNull();
    }

    private sealed class BindingErrorListener : TraceListener
    {
        public List<string> Errors { get; } = [];
        public override void Write(string? message) { if (!string.IsNullOrWhiteSpace(message)) Errors.Add(message); }
        public override void WriteLine(string? message) => Write(message);
    }
}
