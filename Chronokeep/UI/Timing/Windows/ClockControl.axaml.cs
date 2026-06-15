using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.UI.Parts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Chronokeep.UI.Timing.Windows;

public partial class ClockControl : Window
{
    private static ClockControl? theOne = null;

    private readonly IMainWindow window;
    private readonly IDBInterface database;

    private readonly Dictionary<int, Chronoclock> ClockDict = [];

    private ClockControl(IMainWindow window, IDBInterface database)
    {
        InitializeComponent();
        this.window = window;
        this.database = database;
        this.MinWidth = 10;
        this.MinHeight = 10;
        List<Chronoclock> clocks = database.GetClocks();
        foreach (Chronoclock clock in clocks)
        {
            ClockDict[clock.Identifier] = clock;
        }
        UpdateView();
    }
    public static ClockControl CreateWindow(IMainWindow window, IDBInterface database)
    {
        theOne ??= new(window, database);
        return theOne;
    }

    internal void RemoveClock(Chronoclock clock)
    {
        database.RemoveClocks([clock]);
        ClockDict.Remove(clock.Identifier);
        UpdateView();
    }

    private void UpdateView()
    {
        Log.D("UI.Timing.ClockControl", "UpdateView");
        foreach (object? clItem in clockListView.Items.Cast<ClockPart?>())
        {
            if (clItem is ClockPart clPart)
            {
                Chronoclock clock = clPart.GetUpdatedClock();
                ClockDict[clock.Identifier] = clock;
            }
        }
        clockListView.Items.Clear();
        Event? theEvent = database.GetCurrentEvent();
        if (theEvent == null) { return; }
        foreach (Chronoclock clock in ClockDict.Values)
        {
            clockListView.Items.Add(new ClockPart(clock, this, theEvent));
        }
    }

    internal void UpdateTime(string time)
    {
        TimeLabel.Text = string.Format("Clock time is {0}", time);
        TimeLabel.IsVisible = true;
        CurrentTimeLabel.Text = string.Format("System time is {0}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        CurrentTimeLabel.IsVisible = true;
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        Log.D("UI.Timing.ClockControl", "Window is closed.");
        theOne = null;
        foreach (ClockPart? clItem in clockListView.Items.Cast<ClockPart?>())
        {
            Chronoclock clock = clItem!.GetUpdatedClock();
            database.UpdateClock(clock);
        }
        window.WindowFinalize(this);
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ClockControl", "Close button clicked.");
        Close();
    }

    private void AddButton_Click(object? sender, RoutedEventArgs e)
    {
        Chronoclock newClock = new()
        {
            Name = "New Clock",
            URL = "chronoclock.local",
            Enabled = false,
        };
        newClock.Identifier = database.AddClock(newClock);
        if (newClock.Identifier >= 0)
        {
            ClockDict[newClock.Identifier] = newClock;
        }
        UpdateView();
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