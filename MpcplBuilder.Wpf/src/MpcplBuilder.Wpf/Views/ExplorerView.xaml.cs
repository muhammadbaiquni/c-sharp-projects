using System.Windows;
using UserControl = System.Windows.Controls.UserControl;
using MpcplBuilder.Wpf.ViewModels;

namespace MpcplBuilder.Wpf.Views;

public partial class ExplorerView : UserControl
{
    public ExplorerView() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ExplorerViewModel viewModel)
            viewModel.InitializeCommand.Execute(null);
    }
}
