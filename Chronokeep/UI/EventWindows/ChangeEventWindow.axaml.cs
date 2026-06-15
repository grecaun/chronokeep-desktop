using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.UI.Util;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chronokeep.UI.EventWindows;

public partial class ChangeEventWindow : Window
{
    private readonly IWindowCallback window;
    private readonly IDBInterface database;

    private ChangeEventWindow(IWindowCallback window, IDBInterface database)
    {
        InitializeComponent();
        this.window = window;
        this.database = database;
        UpdateEventBox();
    }

    public static ChangeEventWindow NewWindow(IWindowCallback window, IDBInterface database)
    {
        return new ChangeEventWindow(window, database);
    }

    internal async void UpdateEventBox()
    {
        List<Event> events = [];
        await Task.Run(() =>
        {
            events = database.GetEvents();
        });
        events.Sort();
        if (searchBox.Text != null && searchBox.Text.Length > 0)
        {
            Log.D("UI.ChangeEventWindow", "searchBox.Text " + searchBox.Text);
            events.RemoveAll(x => !x.Name.Contains(searchBox.Text, StringComparison.OrdinalIgnoreCase));
        }
        eventList.ItemsSource = events;
        if (events.Count < 1)
        {
            ChangeButton.IsEnabled = false;
        }
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateEventBox();
    }

    private void ChangeButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.ChangeEventWindow", "Change Button Clicked.");
        Event one = (Event)eventList.SelectedItem!;
        if (one != null)
        {
            Log.D("UI.ChangeEventWindow", "Selected event has ID of " + one.Identifier);
            database.SetCurrentEvent(one.Identifier);
            window.WindowFinalize(this);
        }
        else
        {
            Log.D("UI.ChangeEventWindow", "No event selected.");
            DialogBox.Show("No event selected.");
            return;
        }
        Close();
    }

    private void DeleteButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.ChangeEventWindow", "Delete button clicked.");
        Event one = (Event)eventList.SelectedItem!;
        if (one != null)
        {
            Log.D("UI.ChangeEventWindow", "Selected event has ID of " + one.Identifier);
            database.RemoveEvent(one.Identifier);
            UpdateEventBox();
        }
        else
        {
            Log.D("UI.ChangeEventWindow", "No event selected.");
            DialogBox.Show("No event selected.");
        }
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.ChangeEventWindow", "Cancel button clicked.");
        Close();
    }

    private void EventList_MouseDoubleClick(object? sender, TappedEventArgs e)
    {
        Log.D("UI.ChangeEventWindow", "Double Click detected.");
        Event one = (Event)eventList.SelectedItem!;
        if (one != null)
        {
            Log.D("UI.ChangeEventWindow", "Selected event has ID of " + one.Identifier);
            database.SetCurrentEvent(one.Identifier);
            window.WindowFinalize(this);
        }
        else
        {
            Log.D("UI.ChangeEventWindow", "No event selected.");
            DialogBox.Show("No event selected.");
            return;
        }
        Close();
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        window?.WindowFinalize(this);
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}