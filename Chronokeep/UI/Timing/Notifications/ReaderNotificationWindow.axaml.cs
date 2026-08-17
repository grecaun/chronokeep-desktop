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
