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

namespace Chronokeep.UI.EventWindows;

public partial class NewEventWindow : Window
{
    private readonly IDBInterface database;
    private readonly IWindowCallback window;

    private readonly Dictionary<string, Event> eventDict = [];

    public NewEventWindow(IWindowCallback window, IDBInterface database)
    {
        InitializeComponent();
        this.window = window;
        this.database = database;
        this.MinWidth = 350;
        this.MinHeight = 200;
        this.Width = 350;
        this.Height = 310;
        List<Event> events = database.GetEvents();
        events.Sort();
        List<string> eventNames = [];
        foreach (Event e in events)
        {
            string name = string.Format("{0} {1}", e.YearCode, e.Name);
            eventDict.Add(name, e);
            eventNames.Add(name);
        }
        oldEvent.ItemsSource = eventNames;
    }

    public static NewEventWindow NewWindow(IWindowCallback window, IDBInterface database)
    {
        return new NewEventWindow(window, database);
    }

    private void Submit()
    {
        string nameString = nameBox.Text!.Trim();
        string yearString = yearCodeBox.Text!.Trim();
        long dateVal = DateTime.Now.Date.Ticks;
        if (datePicker.Text != null)
        {
            dateVal = DateTime.Parse(datePicker.Text.Replace('_', '0')).Ticks;
        }
        Log.D("NewEventWindow", "Name given for event: '" + nameString + "' Date Given: " + dateVal + " Date Value: " + dateVal);
        if (nameString == "")
        {
            DialogBox.Show("Please input a value in the name box.");
            return;
        }
        else
        {
            int oldEventId = -1;
            if (oldEvent.Text!.Length > 0 && eventDict.TryGetValue(oldEvent.Text, out Event? oEvent))
            {
                oldEventId = oEvent.Identifier;
            }
            Event newEvent = new(nameString, dateVal, yearString);
            database.AddEvent(newEvent);
            newEvent.Identifier = database.GetEventID(newEvent);
            // Copy all values from old event.
            if (oldEventId > 0)
            {
                // Copy old event values.
                Event oldEvent = database.GetEvent(oldEventId)!;
                newEvent.CopyFrom(oldEvent);
                // Update database with current values.
                database.UpdateEvent(newEvent);
                // Get distances from old event
                List<Distance> distances = database.GetDistances(oldEventId);
                List<Distance> newDistances = [];
                // DistanceDict translates a distance name into the old distance identifier.
                Dictionary<string, int> DistanceDict = [];
                // DistanceTranslationDict holds a new distance id and translates it from the old distance with the same name.
                Dictionary<int, int> DistanceTranslationDict = [];
                foreach (Distance d in distances)
                {
                    DistanceDict[d.Name] = d.Identifier;
                    d.Identifier = Constants.Timing.DISTANCE_DUMMYIDENTIFIER;
                    d.EventIdentifier = newEvent.Identifier;
                    newDistances.Add(d);
                }
                // Update database with new distances.
                database.AddDistances(newDistances);
                // Retrieve newly added distances.
                newDistances = database.GetDistances(newEvent.Identifier);
                foreach (Distance newD in newDistances)
                {
                    // Set up a translation dictionary.
                    DistanceTranslationDict[DistanceDict[newD.Name]] = newD.Identifier;
                }
                // Translate linked distance id's.
                // this is a separate process due to potential issues with ordering
                foreach (Distance newD in newDistances)
                {
                    if (Constants.Timing.DISTANCE_NO_LINKED_ID != newD.LinkedDistance)
                    {
                        if (DistanceTranslationDict.TryGetValue(newD.LinkedDistance, out int oDistId))
                        {
                            newD.LinkedDistance = oDistId;
                        }
                        else
                        {
                            newD.LinkedDistance = Constants.Timing.DISTANCE_NO_LINKED_ID;
                        }
                        database.UpdateDistance(newD);
                    }
                }
                // Get locations from old event.
                List<TimingLocation> locations = database.GetTimingLocations(oldEventId);
                List<TimingLocation> newLocations = [];
                // translates a location name into the old distance identifier
                Dictionary<string, int> LocationDict = [];
                // translates the old location id to the new location id
                Dictionary<int, int> LocationTranslationDict = [];
                foreach (TimingLocation loc in locations)
                {
                    loc.EventIdentifier = newEvent.Identifier;
                    newLocations.Add(loc);
                    LocationDict[loc.Name] = loc.Identifier;
                }
                // Update database with new locations
                database.AddTimingLocations(newLocations);
                // retrieve newly added locations
                newLocations = database.GetTimingLocations(newEvent.Identifier);
                foreach (TimingLocation newLoc in newLocations)
                {
                    LocationTranslationDict[LocationDict[newLoc.Name]] = newLoc.Identifier;
                }
                // Get old segments from the database.
                List<Segment> segments = database.GetSegments(oldEventId);
                List<Segment> newSegments = [];
                foreach (Segment s in segments)
                {
                    s.EventId = newEvent.Identifier;
                    if (newEvent.DistanceSpecificSegments && DistanceTranslationDict.TryGetValue(s.DistanceId, out int tDistId))
                    {
                        s.DistanceId = tDistId;
                    }
                    if (Constants.Timing.LOCATION_FINISH != s.LocationId && Constants.Timing.LOCATION_START != s.LocationId && LocationTranslationDict.TryGetValue(s.LocationId, out int tLocId))
                    {
                        s.LocationId = tLocId;
                    }
                    newSegments.Add(s);
                }
                // Update database with new segments.
                database.AddSegments(newSegments);
                // Get age groups from database.
                List<AgeGroup> ageGroups = database.GetAgeGroups(oldEventId);
                List<AgeGroup> newAgeGroups = [];
                foreach (AgeGroup ag in ageGroups)
                {
                    ag.EventId = newEvent.Identifier;
                    if (!newEvent.CommonAgeGroups && DistanceTranslationDict.TryGetValue(ag.DistanceId, out int tDistId))
                    {
                        ag.DistanceId = tDistId;
                    }
                    newAgeGroups.Add(ag);
                }
                // Update database with new age groups.
                database.AddAgeGroups(newAgeGroups);
            }
            else
            {
                database.AddDistance(new("Default Distance", newEvent.Identifier));
            }
            database.SetCurrentEvent(newEvent.Identifier);
            window.WindowFinalize(this);
        }
        this.Close();
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        window?.WindowFinalize(this);
    }

    private void Keyboard_Up(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Submit();
        }
    }

    private void Submit_Click(object? sender, RoutedEventArgs e)
    {
        Submit();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        this.Close();
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