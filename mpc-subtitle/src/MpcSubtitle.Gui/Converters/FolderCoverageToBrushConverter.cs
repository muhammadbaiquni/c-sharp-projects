using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MpcSubtitle.Core;

namespace MpcSubtitle.Gui.Converters;

/// <summary>Maps a folder's subtitle coverage to an icon fill color (yellow/orange/green).</summary>
public sealed class FolderCoverageToBrushConverter : IValueConverter
{
    private static readonly Brush NoneBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF2, 0xC1, 0x4A)));
    private static readonly Brush PartialBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0x8C, 0x00)));
    private static readonly Brush AllBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x5A, 0xB8, 0x3B)));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            SubtitleCoverage.Partial => PartialBrush,
            SubtitleCoverage.All => AllBrush,
            _ => NoneBrush,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
