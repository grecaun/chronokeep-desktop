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

public partial class EditRawReadsWindow : ChronokeepWindow
{
    private readonly ITimingPage parent;
    private readonly IdbInterface database;
    private readonly Event theEvent;
    private readonly List<ChipRead> chipReads;

    [GeneratedRegex("[^0-9]")]
    private static partial Regex AllowedChars();

    public EditRawReadsWindow(ITimingPage parent, IdbInterface database, List<ChipRead> chipReads)
    {
        InitializeComponent();
        ChronokeepInitialize();
        this.parent = parent;
        this.database = database;
        this.chipReads = chipReads;
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

    private void Submit_Click(object? sender, RoutedEventArgs? e)
    {
        Log.D("UI.Timing.EditRawReadsWindow", "Submit clicked.");
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
            read.TimeSeconds = read.TimeSeconds + (86400 * days) + seconds;
            read.TimeMilliseconds += milliseconds;
            switch (read.TimeMilliseconds)
            {
                case < 0:
                    read.TimeSeconds--;
                    read.TimeMilliseconds += 1000;
                    break;
                case >= 1000:
                    read.TimeSeconds++;
                    read.TimeMilliseconds -= 1000;
                    break;
            }
        }
        database.UpdateChipReads(chipReads);
        database.ResetTimingResultsEvent(theEvent.Identifier);
        parent.UpdateView();
        parent.NotifyTimingWorker();
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.EditRawReadsWindow", "Cancel clicked.");
        Close();
    }
}