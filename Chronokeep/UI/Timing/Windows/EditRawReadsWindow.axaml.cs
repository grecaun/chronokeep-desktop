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
using System.Text.RegularExpressions;

namespace Chronokeep.UI.Timing.Windows;

public partial class EditRawReadsWindow : Window
{
    private readonly ITimingPage parent;
    private readonly IDBInterface database;
    private readonly Event theEvent;
    private readonly List<ChipRead> chipReads;

    [GeneratedRegex("[^0-9]")]
    private static partial Regex AllowedChars();

    public EditRawReadsWindow(ITimingPage parent, IDBInterface database, List<ChipRead> chipReads)
    {
        InitializeComponent();
        this.parent = parent;
        this.database = database;
        this.chipReads = chipReads;
        this.MinWidth = 280;
        this.Width = 280;
        this.MinHeight = 230;
        this.Height = 230;
        theEvent = database.GetCurrentEvent()!;
        TimeBox.Focus();
    }

    private void Window_Closed(object sender, EventArgs e) { }

    private void DaysBox_PreviewTextInput(object sender, TextInputEventArgs e)
    {
        e.Handled = AllowedChars().IsMatch(e.Text!);
    }

    private void Enter_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Submit_Click(null, null);
        }
    }

    private void Submit_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs? e)
    {
        Log.D("UI.Timing.EditRawReadsWindow", "Submit clicked.");
        // Keep track of any bibs/chips we've changed.
        HashSet<string> bibsChanged = [];
        HashSet<string> chipsChanged = [];
        bool add = AddRadio.IsChecked == true;
        string[] firstparts = TimeBox.Text!.Replace('_', '0').Split(':');
        string[] secondparts = firstparts[2].Split('.');
        int seconds, milliseconds;
        _ = int.TryParse(DaysBox.Text, out int days);
        try
        {
            int hours = Convert.ToInt32(firstparts[0]),
                minutes = Convert.ToInt32(firstparts[1]);
            seconds = Convert.ToInt32(secondparts[0]);
            milliseconds = Convert.ToInt32(secondparts[1]);
            seconds = (hours * 3600) + (minutes * 60) + seconds;
        }
        catch
        {
            Log.D("UI.Timing.EditRawReadsWindow", "Somehow the time value wasn't valid.");
            DialogBox.Show("Something went wrong trying to figure out that time value.");
            return;
        }
        if (!add)
        {
            seconds *= -1;
            milliseconds *= -1;
            days *= -1;
        }
        foreach (ChipRead read in chipReads)
        {
            if (Constants.Timing.CHIPREAD_DUMMYBIB == read.Bib)
            {
                chipsChanged.Add(read.ChipNumber);
            }
            else
            {
                bibsChanged.Add(read.Bib);
            }
            read.TimeSeconds = read.TimeSeconds + (86400 * days) + seconds;
            read.TimeMilliseconds += milliseconds;
            if (read.TimeMilliseconds < 0)
            {
                read.TimeSeconds--;
                read.TimeMilliseconds += 1000;
            }
            else if (read.TimeMilliseconds >= 1000)
            {
                read.TimeSeconds++;
                read.TimeMilliseconds -= 1000;
            }
        }
        database.UpdateChipReads(chipReads);
        database.ResetTimingResultsEvent(theEvent.Identifier);
        parent.UpdateView();
        parent.NotifyTimingWorker();
        Close();
    }

    private void Cancel_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.EditRawReadsWindow", "Cancel clicked.");
        Close();
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