using System.Globalization;
using System.Windows.Data;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace MpcplBuilder.Wpf.Converters;

public sealed class FolderStatusToBrushConverter : IValueConverter
{
    public Brush PlaylistBrush { get; set; } = Brushes.ForestGreen;
    public Brush EmptyBrush { get; set; } = Brushes.Goldenrod;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? PlaylistBrush : EmptyBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;
}
