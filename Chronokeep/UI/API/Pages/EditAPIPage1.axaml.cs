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
using Chronokeep.Database;
using Chronokeep.Objects;
using Chronokeep.UI.API.Windows;
using Chronokeep.UI.Util;

namespace Chronokeep.UI.API.Pages;

public partial class EditApiPage1 : UserControl
{
    private readonly EditApiWindow window;
    private readonly IdbInterface database;

    public EditApiPage1(EditApiWindow window, IdbInterface database)
    {
        InitializeComponent();
        this.window = window;
        this.database = database;
    }

    private void Unlink_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Event theEvent = database.GetCurrentEvent()!;
        // Check if we've actually got a linked event, then unlink it.
        if (theEvent.ApiId != Constants.ApiConstants.NULL_ID && theEvent.ApiEventId != Constants.ApiConstants.NULL_EVENT_ID)
        {
            theEvent.ApiId = Constants.ApiConstants.NULL_ID;
            theEvent.ApiEventId = Constants.ApiConstants.NULL_EVENT_ID;
            database.UpdateEvent(theEvent);
            window.NetworkUpdateResults();
        }
        else
        {
            DialogBox.AsyncShow("Unable to Link Event");
        }
        window.Close();
    }

    private void Edit_Event_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        window.GotoEditEvent();
    }

    private void Edit_Year_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        window.GotoEditYear();
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        window.Close();
    }
}
