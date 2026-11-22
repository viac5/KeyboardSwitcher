using Avalonia.Controls;

namespace KeyboardSwitcher.Presentation;

internal static class ControlExtensions
{
    public static T SetGrid<T>( this T control, int column, int row ) where T : Control
    {
        Grid.SetColumn( control, column );
        Grid.SetRow( control, row );
        return control;
    }
}
