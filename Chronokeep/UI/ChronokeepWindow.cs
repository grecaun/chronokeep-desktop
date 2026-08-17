/*
Chronokeep Desktop - Race Scoring Software
Copyright (C) 2026 James Sentinella

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
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
        if (this.TryFindResource("PrimaryBackground", this.ActualThemeVariant, out var bgBrush))
        {
            Background = (IBrush?)bgBrush;
        }
        CanResize = false;
        Topmost = true;
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
