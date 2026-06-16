using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using System.Collections.Generic;

namespace Chronokeep.UI.Participants;

public partial class ParticipantConflicts : Window
{
    private readonly IMainWindow window;

    private ParticipantConflicts(IMainWindow window, List<Participant> participants)
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
        window.WindowFinalize(this);
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}