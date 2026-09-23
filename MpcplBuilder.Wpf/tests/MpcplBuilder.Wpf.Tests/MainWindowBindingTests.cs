using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using FluentAssertions;
using MpcplBuilder.Wpf.Tests.Fakes;

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
                var window = new MainWindow
                {
                    DataContext = new MainWindowViewModel(
                        new FakeInspectFolder(), new FakeGeneratePlaylist(),
                        new FakeFolderPicker(), new FakeUserDialogService())
                };
                window.ApplyTemplate();

                AssertBinding<TextBox>(window, "RootTextBox", TextBox.TextProperty);
                AssertBinding<Button>(window, "BrowseButton", Button.CommandProperty);
                AssertBinding<Button>(window, "GenerateButton", Button.CommandProperty);
                AssertBinding<ListBox>(window, "PreviewListBox", ListBox.ItemsSourceProperty);
                AssertBinding<TextBlock>(window, "StatusTextBlock", TextBlock.TextProperty);

                foreach (var name in new[] { "RootTextBox", "BrowseButton", "GenerateButton", "ClearButton", "CancelButton" })
                {
                    var control = (FrameworkElement)window.FindName(name);
                    AutomationProperties.GetName(control).Should().NotBeNullOrWhiteSpace();
                }

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
