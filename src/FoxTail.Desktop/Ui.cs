using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FoxTail.Desktop;

internal static class Ui
{
    public static SolidColorBrush Brush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    public static Grid Grid(string rows = "*", string columns = "*")
    {
        var grid = new Grid();
        var converter = new GridLengthConverter();
        foreach (var row in rows.Split(',')) grid.RowDefinitions.Add(new RowDefinition { Height = (GridLength)converter.ConvertFromInvariantString(row)! });
        foreach (var column in columns.Split(',')) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = (GridLength)converter.ConvertFromInvariantString(column)! });
        return grid;
    }
}
