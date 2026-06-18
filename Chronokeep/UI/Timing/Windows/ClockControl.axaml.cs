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

public partial class ClockControl : ChronokeepWindow
{
    private static ClockControl? theOne;

    private readonly IMainWindow window;
    private readonly IdbInterface database;

    private readonly Dictionary<int, Chronoclock> clockDict = [];

    private ClockControl(IMainWindow window, IdbInterface database)
    {
        InitializeComponent();
        ChronokeepInitialize();
        this.window = window;
        this.database = database;
        List<Chronoclock> clocks = database.GetClocks();
        foreach (Chronoclock clock in clocks)
        {
            clockDict[clock.Identifier] = clock;
        }
        UpdateView();
    }
    public static ClockControl CreateWindow(IMainWindow window, IdbInterface database)
    {
        theOne ??= new ClockControl(window, database);
        return theOne;
    }

    internal void RemoveClock(Chronoclock clock)
    {
        database.RemoveClocks([clock]);
        clockDict.Remove(clock.Identifier);
        UpdateView();
    }

    private void UpdateView()
    {
        Log.D("UI.Timing.ClockControl", "UpdateView");
        foreach (object? clItem in ClockListView.Items)
        {
            if (clItem is not ClockPart clPart) continue;
            Chronoclock clock = clPart.GetUpdatedClock();
            if (clockDict.ContainsKey(clock.Identifier))
            {
                clockDict[clock.Identifier] = clock;
            }
        }
        ClockListView.Items.Clear();
        Event? theEvent = database.GetCurrentEvent();
        if (theEvent == null) { return; }
        foreach (Chronoclock clock in clockDict.Values)
        {
            ClockListView.Items.Add(new ClockPart(clock, this, theEvent));
        }
    }

    internal void UpdateTime(string time)
    {
        TimeLabel.Text = $"Clock time is {time}";
        TimeLabel.IsVisible = true;
        CurrentTimeLabel.Text = $"System time is {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        CurrentTimeLabel.IsVisible = true;
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        Log.D("UI.Timing.ClockControl", "Window is closed.");
        theOne = null;
        foreach (ClockPart? clItem in ClockListView.Items.Cast<ClockPart?>())
        {
            Chronoclock clock = clItem!.GetUpdatedClock();
            database.UpdateClock(clock);
        }
        window.WindowFinalize();
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
            Url = "chronoclock.local",
            Enabled = false,
        };
        newClock.Identifier = database.AddClock(newClock);
        if (newClock.Identifier >= 0)
        {
            clockDict[newClock.Identifier] = newClock;
        }
        UpdateView();
    }

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}