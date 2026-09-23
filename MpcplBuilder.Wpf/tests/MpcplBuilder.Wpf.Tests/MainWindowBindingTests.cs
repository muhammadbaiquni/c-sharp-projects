using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using FluentAssertions;
using MpcplBuilder.Wpf.Tests.Fakes;
using MpcplBuilder.Wpf.ViewModels;
using MpcplBuilder.Wpf.Views;

namespace MpcplBuilder.Wpf.Tests;

public sealed class MainWindowBindingTests
{
    [Fact]
    public void MainWindow_PrimaryControlsHaveBindingsAndAccessibleNames()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var view = new DefaultView
                {
                    DataContext = new DefaultViewModel(
                        new FakeInspectFolder(), new FakeGeneratePlaylist(),
                        new FakeFolderPicker(), new FakeUserDialogService())
                };
                view.ApplyTemplate();

                AssertBinding<TextBox>(view, "RootTextBox", TextBox.TextProperty);
                AssertBinding<Button>(view, "BrowseButton", Button.CommandProperty);
                AssertBinding<Button>(view, "GenerateButton", Button.CommandProperty);
                AssertBinding<ListBox>(view, "PreviewListBox", ListBox.ItemsSourceProperty);
                AssertBinding<TextBlock>(view, "StatusTextBlock", TextBlock.TextProperty);

                foreach (var name in new[] { "RootTextBox", "BrowseButton", "GenerateButton", "ClearButton", "CancelButton" })
                {
                    var control = (FrameworkElement)view.FindName(name);
                    AutomationProperties.GetName(control).Should().NotBeNullOrWhiteSpace();
                }
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        failure.Should().BeNull();
    }

    [Fact]
    public void MainWindow_StartsOnDefaultTabAfterExplorer()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var defaultViewModel = new DefaultViewModel(
                    new FakeInspectFolder(), new FakeGeneratePlaylist(),
                    new FakeFolderPicker(), new FakeUserDialogService());
                var explorerViewModel = new ExplorerViewModel();
                var shell = new MainWindowViewModel(defaultViewModel, explorerViewModel);
                var window = new MainWindow { DataContext = shell };
                window.Show();
                var tabs = (TabControl)window.FindName("ShellTabs");

                shell.SelectedTabIndex.Should().Be(1);
                shell.Default.Should().BeSameAs(defaultViewModel);
                shell.Explorer.Should().BeSameAs(explorerViewModel);
                shell.Default.Should().NotBeSameAs((object)shell.Explorer);
                tabs.SelectedIndex.Should().Be(1);
                ((TabItem)tabs.Items[0]).Header.Should().Be("Explorer");
                ((TabItem)tabs.Items[1]).Header.Should().Be("Default");
                ((TabItem)tabs.Items[1]).Content.Should().BeOfType<DefaultView>();
                tabs.SelectedIndex = 0;
                shell.SelectedTabIndex.Should().Be(0);

                window.Close();
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        failure.Should().BeNull();
    }

    private static void AssertBinding<T>(FrameworkElement window, string name, DependencyProperty property)
        where T : FrameworkElement
    {
        var control = (T)window.FindName(name);
        BindingOperations.GetBindingExpression(control, property).Should().NotBeNull();
    }
}
