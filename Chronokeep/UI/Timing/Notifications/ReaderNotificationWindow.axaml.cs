using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using System.Collections.Generic;
using static Chronokeep.Helpers.Globals;

namespace Chronokeep.UI.Timing.Notifications;

public partial class ReaderNotificationWindow : ChronokeepWindow
{
    private readonly IWindowCallback window;

    private ReaderNotificationWindow(IWindowCallback window)
    {
        InitializeComponent();
        ChronokeepInitialize();
        this.window = window;
        UpdateNotificationsBox();
    }

    public static ReaderNotificationWindow NewWindow(IWindowCallback window)
    {
        return new ReaderNotificationWindow(window);
    }

    private void UpdateNotificationsBox()
    {
        List<ReaderMessage> messages = GetReaderMessages();
        messages.Sort();
        foreach (ReaderMessage msg in messages)
        {
            msg.Notified = true;
        }
        NotificationsList.ItemsSource = messages;
        UpdateReaderMessages(messages);
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        window.WindowFinalize();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.Notifications.ReaderNotificationWindow", "Done button clicked.");
        Close();
    }

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}