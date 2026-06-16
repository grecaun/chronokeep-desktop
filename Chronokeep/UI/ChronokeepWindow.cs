using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Chronokeep.UI;

public class ChronokeepWindow : Window
{
    protected virtual void Maximize() {}
    protected virtual void SetMaximizeIcon() {}

    internal void OnMinimize(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    internal void OnMaximize(object? sender, RoutedEventArgs e)
    {
        Maximize();
    }

    internal void OnClose(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    internal void Window_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        SetMaximizeIcon();
    }
    
    internal void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    internal void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        Maximize();
    }
}