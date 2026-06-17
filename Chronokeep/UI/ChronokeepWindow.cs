using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Chronokeep.UI.Announcer;
using Chronokeep.UI.UhfRfidReader;

namespace Chronokeep.UI;

public class ChronokeepWindow : Window
{
    protected virtual void Maximize() {}
    protected virtual void SetMaximizeIcon() {}
    protected virtual void SetPlatform() {}
    protected virtual Border? TitleBar() { return null; }

    internal void ChronokeepInitialize()
    {
        if (!App.IsWindows)
        {
            Title = "";
            WindowDecorations = WindowDecorations.Full;
            ExtendClientAreaToDecorationsHint = false;
            TitleBar()?.Height = 0;
            TitleBar()?.IsVisible = false;
        }
        else
        {
            WindowDecorations = WindowDecorations.BorderOnly;
            ExtendClientAreaToDecorationsHint = true;
            TitleBar()?.Height = 32;
            TitleBar()?.IsVisible = true;
        }
        if (this is MainWindow or MinWindow or AnnouncerWindow or ChipReaderWindow) return;
        CanResize = false;
        Topmost = true;
        ShowInTaskbar = true;
    }
    
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