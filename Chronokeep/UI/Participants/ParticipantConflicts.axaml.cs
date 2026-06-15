using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using System.Collections.Generic;

namespace Chronokeep.UI.Participants;

public partial class ParticipantConflicts : Window
{
    private readonly IMainWindow window;

    public ParticipantConflicts(IMainWindow window, List<Participant> participants)
    {
        InitializeComponent();
        this.window = window;

        ParticipantsList.ItemsSource = participants;
    }

    public static ParticipantConflicts NewWindow(IMainWindow window, List<Participant> participants)
    {
        return new ParticipantConflicts(window, participants);
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        window?.WindowFinalize(this);
    }

    private void OnMinimize(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximize(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        MaximizeIcon?.IsVisible = WindowState == WindowState.Normal;
        UnMaximizeIcon?.IsVisible = WindowState == WindowState.Maximized;
    }
}