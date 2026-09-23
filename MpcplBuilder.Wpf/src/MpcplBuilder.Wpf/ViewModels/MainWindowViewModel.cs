using CommunityToolkit.Mvvm.ComponentModel;

namespace MpcplBuilder.Wpf.ViewModels;

public sealed partial class MainWindowViewModel(DefaultViewModel defaultViewModel, ExplorerViewModel explorerViewModel) : ObservableObject
{
    public ExplorerViewModel Explorer { get; } = explorerViewModel;
    public DefaultViewModel Default { get; } = defaultViewModel;

    [ObservableProperty]
    private int selectedTabIndex = 1;
}
