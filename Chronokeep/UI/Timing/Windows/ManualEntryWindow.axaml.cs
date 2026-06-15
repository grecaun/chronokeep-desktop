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

namespace Chronokeep.UI.Timing.Windows;

public partial class ManualEntryWindow : Window
{
    private readonly IMainWindow window;
    private readonly IDBInterface database;
    private readonly Event? theEvent;

    private readonly HashSet<string> bibsAdded = [];

    private readonly bool dnf = false;

    private ManualEntryWindow(IMainWindow window, IDBInterface database, List<TimingLocation> locations)
    {
        InitializeComponent();
        this.MinHeight = 275;
        this.MinWidth = 300;
        this.Width = 300;
        this.Topmost = true;
        this.window = window;
        this.database = database;
        theEvent = database.GetCurrentEvent();
        if (theEvent == null)
        {
            return;
        }
        DateBox.SelectedDate = DateTime.Parse(theEvent.Date);
        UpdateLocations(locations);
    }

    // For Add DNF Entry use
    private ManualEntryWindow(IMainWindow window, IDBInterface database)
    {
        InitializeComponent();
        this.MinHeight = 275;
        this.MinWidth = 300;
        this.Width = 300;
        this.Topmost = true;
        this.window = window;
        this.database = database;
        theEvent = database.GetCurrentEvent();
        if (theEvent == null)
        {
            return;
        }
        dnf = true;
        List<TimingLocation> locations = database.GetTimingLocations(theEvent.Identifier);
        if (locations != null)
        {
            locations.Insert(0, new(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Finish", theEvent.FinishMaxOccurrences, theEvent.FinishIgnoreWithin));
            UpdateLocations(locations);
        }
    }

    private void ClearBib()
    {
        BibBox.Clear();
        BibBox.Focus();
    }

    public void UpdateLocations(List<TimingLocation> locations)
    {
        int selectedLoc;
        try
        {
            if (LocationBox.SelectedIndex >= 0)
            {
                selectedLoc = Convert.ToInt32(((ComboBoxItem)LocationBox.SelectedItem!).Tag);
            }
            else
            {
                selectedLoc = Constants.Timing.LOCATION_FINISH;
            }
        }
        catch
        {
            selectedLoc = Constants.Timing.LOCATION_FINISH;
        }
        ComboBoxItem? current, selected = null;
        LocationBox.Items.Clear();
        foreach (TimingLocation loc in locations)
        {
            current = new ComboBoxItem()
            {
                Content = loc.Name,
                Tag = loc.Identifier.ToString()
            };
            LocationBox.Items.Add(current);
            if (loc.Identifier == selectedLoc)
            {
                selected = current;
            }
        }
        if (selected != null)
        {
            LocationBox.SelectedItem = selected;
        }
        else
        {
            LocationBox.SelectedIndex = 0;
        }
    }

    public static ManualEntryWindow NewWindow(IMainWindow window, IDBInterface database, List<TimingLocation>? locations = null)
    {
        if (locations == null)
        {
            return new(window, database);
        }
        return new(window, database, locations);
    }

    private void AddDNF()
    {
        Log.D("UI.Timing.ManualEntryWindow", "DNF entry detected.");
        string bib = BibBox.Text!.Trim();
        if (string.IsNullOrEmpty(bib))
        {
            DialogBox.Show("Invalid bib value given.");
            return;
        }
        string timeVal = TimeBox.Text!.Replace('_', '0');
        int locationId = Convert.ToInt32(((ComboBoxItem)LocationBox.SelectedItem!).Tag);
        DateTime time;
        long hours, minutes, seconds, milliseconds;
        hours = Convert.ToInt32(timeVal[..2]);
        minutes = Convert.ToInt32(timeVal.Substring(3, 2));
        seconds = Convert.ToInt32(timeVal.Substring(6, 2));
        milliseconds = Convert.ToInt32(timeVal.Substring(9, 3));
        if (hours == minutes && minutes == seconds && seconds == milliseconds && milliseconds == 0)
        {
            time = DateTime.Now;
        }
        else
        {
            if (NetTimeButton.IsChecked == true)
            {
                List<Participant> participants = database.GetParticipants(theEvent!.Identifier);
                List<Distance> distances = database.GetDistances(theEvent.Identifier);
                // Store the offset start values for each distance by distance ID
                Dictionary<int, (int seconds, int milliseconds)> distanceStartOffsetDictionary = [];
                // Store participants by their bib number
                Dictionary<string, Participant> participantsDictionary = [];
                foreach (Distance div in distances)
                {
                    distanceStartOffsetDictionary[div.Identifier] = (div.StartOffsetSeconds, div.StartOffsetMilliseconds);
                }
                foreach (Participant part in participants)
                {
                    participantsDictionary[part.EventSpecific.Bib] = part;
                }
                (int seconds, int milliseconds) startOffset = (0, 0);
                // Check if the bib corresponds to a person, then if that person has a valid distance ID
                if (participantsDictionary.TryGetValue(bib, out Participant? oPart) && distanceStartOffsetDictionary.TryGetValue(oPart.EventSpecific.DistanceIdentifier, out (int seconds, int milliseconds) oStart))
                {
                    startOffset = oStart;
                }
                time = DateTime.Parse(theEvent.Date + " 00:00:00.000");
                milliseconds += theEvent.StartMilliseconds + startOffset.milliseconds;
                seconds += (minutes * 60) + (hours * 3600) + theEvent.StartSeconds + startOffset.seconds;
            }
            else if (ClockTimeButton.IsChecked == true)
            {
                time = DateTime.Parse(theEvent!.Date + " 00:00:00.000");
                milliseconds += theEvent.StartMilliseconds;
                seconds += (minutes * 60) + (hours * 3600) + theEvent.StartSeconds;
            }
            else
            {
                time = DateTime.Parse(DateBox.SelectedDate + " 00:00:00.000");
                if (hours > 23)
                {
                    hours = 23;
                }
                seconds += (minutes * 60) + (hours * 3600);
            }
            time = time.AddSeconds(seconds);
            time = time.AddMilliseconds(milliseconds);
        }
        ChipRead newEntry = new(theEvent!.Identifier, locationId, bib, time, Constants.Timing.CHIPREAD_STATUS_DNF);
        Log.D("UI.Timing.ManualEntryWindow", "Bib " + BibBox + " LocationId " + locationId + " Time " + newEntry.TimeString);
        database.AddChipRead(newEntry);
        bibsAdded.Add(bib);
        ClearBib();
    }

    private void AddEntry()
    {
        Log.D("UI.Timing.ManualEntryWindow", "Manual entry detected.");
        string bib;
        try
        {
            bib = BibBox.Text!;
        }
        catch
        {
            DialogBox.Show("Invalid bib value given.");
            return;
        }
        string timeVal = TimeBox.Text!.Replace('_', '0');
        int locationId = Convert.ToInt32(((ComboBoxItem)LocationBox.SelectedItem!).Tag);
        DateTime time;
        long hours, minutes, seconds, milliseconds;
        hours = Convert.ToInt32(timeVal[..2]);
        minutes = Convert.ToInt32(timeVal.Substring(3, 2));
        seconds = Convert.ToInt32(timeVal.Substring(6, 2));
        milliseconds = Convert.ToInt32(timeVal.Substring(9, 3));
        if (hours == minutes && minutes == seconds && seconds == milliseconds && milliseconds == 0)
        {
            DialogBox.Show("No time value specified.");
            return;
        }
        if (NetTimeButton.IsChecked == true)
        {
            List<Participant> participants = database.GetParticipants(theEvent!.Identifier);
            List<Distance> distances = database.GetDistances(theEvent.Identifier);
            // Store the offset start values for each distance by distance ID
            Dictionary<int, (int seconds, int milliseconds)> distanceStartOffsetDictionary = [];
            // Store participants by their bib number
            Dictionary<string, Participant> participantsDictionary = [];
            foreach (Distance div in distances)
            {
                distanceStartOffsetDictionary[div.Identifier] = (div.StartOffsetSeconds, div.StartOffsetMilliseconds);
            }
            foreach (Participant part in participants)
            {
                participantsDictionary[part.EventSpecific.Bib] = part;
            }
            (int seconds, int milliseconds) startOffset = (0, 0);
            // Check if the bib corresponds to a person, then if that person has a valid distance ID
            if (participantsDictionary.TryGetValue(bib, out Participant? oPart) && distanceStartOffsetDictionary.TryGetValue(oPart.EventSpecific.DistanceIdentifier, out (int seconds, int milliseconds) oStart))
            {
                startOffset = oStart;
            }
            time = DateTime.Parse(theEvent.Date + " 00:00:00.000");
            milliseconds += theEvent.StartMilliseconds + startOffset.milliseconds;
            seconds += (minutes * 60) + (hours * 3600) + theEvent.StartSeconds + startOffset.seconds;
        }
        else if (ClockTimeButton.IsChecked == true)
        {
            time = DateTime.Parse(theEvent!.Date + " 00:00:00.000");
            milliseconds += theEvent.StartMilliseconds;
            seconds += (minutes * 60) + (hours * 3600) + theEvent.StartSeconds;
        }
        else
        {
            time = DateTime.Parse(DateBox.SelectedDate + " 00:00:00.000");
            if (hours > 23)
            {
                hours = 23;
            }
            seconds += (minutes * 60) + (hours * 3600);
        }
        time = time.AddSeconds(seconds);
        time = time.AddMilliseconds(milliseconds);
        ChipRead newEntry = new(theEvent!.Identifier, locationId, bib, time, Constants.Timing.CHIPREAD_STATUS_NONE);
        Log.D("UI.Timing.ManualEntryWindow", "Bib " + BibBox + " LocationId " + locationId + " Time " + newEntry.TimeString);
        database.AddChipRead(newEntry);
        bibsAdded.Add(bib);
        ClearBib();
    }

    private void Window_Closing(object sender, WindowClosingEventArgs e)
    {
        window?.WindowFinalize(this);
        if (bibsAdded.Count > 0)
        {
            database.ResetTimingResultsEvent(theEvent!.Identifier);
            window?.NotifyTimingWorker();
        }
    }

    private void Enter_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (dnf)
            {
                AddDNF();
            }
            else
            {
                AddEntry();
            }
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (dnf)
        {
            AddDNF();
        }
        else
        {
            AddEntry();
        }
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}