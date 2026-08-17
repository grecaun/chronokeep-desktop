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
using Avalonia.Input;
using Avalonia.Interactivity;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chronokeep.UI.EventWindows;

public partial class ChangeEventWindow : ChronokeepWindow
{
    private readonly IWindowCallback window;
    private readonly IdbInterface database;

    private ChangeEventWindow(IWindowCallback window, IdbInterface database)
    {
        InitializeComponent();
        ChronokeepInitialize();
        this.window = window;
        this.database = database;
        UpdateEventBox();
    }

    public static ChangeEventWindow NewWindow(IWindowCallback window, IdbInterface database)
    {
        return new ChangeEventWindow(window, database);
    }

    private async void UpdateEventBox()
    {
        try
        {
            List<Event> events = [];
            await Task.Run(() =>
            {
                events = database.GetEvents();
            });
            events.Sort();
            if (SearchBox.Text is { Length: > 0 })
            {
                Log.D("UI.ChangeEventWindow", $"searchBox.Text {SearchBox.Text}");
                events.RemoveAll(x => !x.Name.Contains(SearchBox.Text, StringComparison.OrdinalIgnoreCase));
            }
            EventList.ItemsSource = events;
            if (events.Count < 1)
            {
                ChangeButton.IsEnabled = false;
            }
        }
        catch (Exception)
        {
            Log.D("UI.ChangeEventWindow", "Error updating event box.");
        }
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateEventBox();
    }

    private void ChangeButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.ChangeEventWindow", "Change Button Clicked.");
        Event one = (Event)EventList.SelectedItem!;
        Log.D("UI.ChangeEventWindow", $"Selected event has ID of {one.Identifier}");
        database.SetCurrentEvent(one.Identifier);
        window.WindowFinalize();
        Close();
    }

    private void DeleteButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.ChangeEventWindow", "Delete button clicked.");
        Event one = (Event)EventList.SelectedItem!;
        Log.D("UI.ChangeEventWindow", $"Selected event has ID of {one.Identifier}");
        database.RemoveEvent(one.Identifier);
        UpdateEventBox();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.ChangeEventWindow", "Cancel button clicked.");
        Close();
    }

    private void EventList_MouseDoubleClick(object? sender, TappedEventArgs e)
    {
        Log.D("UI.ChangeEventWindow", "Double Click detected.");
        Event one = (Event)EventList.SelectedItem!;
        Log.D("UI.ChangeEventWindow", $"Selected event has ID of {one.Identifier}");
        database.SetCurrentEvent(one.Identifier);
        window.WindowFinalize();
        Close();
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        window.WindowFinalize();
    }

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}
