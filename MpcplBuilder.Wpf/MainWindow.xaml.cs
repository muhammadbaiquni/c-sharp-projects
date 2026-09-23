using System.Windows;

namespace MpcplBuilder.Wpf
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Use a dedicated ViewModel for MVVM
            DataContext = new MainWindowViewModel();
        }

        // Handlers moved to MainWindowViewModel for MVVM
    }
}
