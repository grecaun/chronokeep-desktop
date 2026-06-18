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
using System.Linq;

namespace Chronokeep.UI.EventWindows;

public partial class NewEventWindow : ChronokeepWindow
{
    private readonly IdbInterface database;
    private readonly IWindowCallback window;

    private readonly Dictionary<string, Event> eventDict = [];

    private NewEventWindow(IWindowCallback window, IdbInterface database)
    {
        InitializeComponent();
        ChronokeepInitialize();
        this.window = window;
        this.database = database;
        List<Event> events = database.GetEvents();
        events.Sort();
        List<string> eventNames = [];
        foreach (Event e in events)
        {
            string name = $"{e.YearCode} {e.Name}";
            eventDict.Add(name, e);
            eventNames.Add(name);
        }
        OldEvent.ItemsSource = eventNames;
    }

    public static NewEventWindow NewWindow(IWindowCallback window, IdbInterface database)
    {
        return new NewEventWindow(window, database);
    }

    private void Submit()
    {
        string nameString = NameBox.Text!.Trim();
        string yearString = YearCodeBox.Text!.Trim();
        long dateVal = DateTime.Now.Date.Ticks;
        if (DatePicker.Text != null)
        {
            dateVal = DateTime.Parse(DatePicker.Text.Replace('_', '0')).Ticks;
        }
        Log.D("NewEventWindow", $"Name given for event: '{nameString}' Date Given: {dateVal} Date Value: {dateVal}");
        if (nameString == "")
        {
            DialogBox.Show("Please input a value in the name box.");
            return;
        }
        int oldEventId = -1;
        if (OldEvent.Text!.Length > 0 && eventDict.TryGetValue(OldEvent.Text, out Event? oEvent))
        {
            oldEventId = oEvent.Identifier;
        }
        Event newEvent = new(nameString, dateVal, yearString);
        database.AddEvent(newEvent);
        newEvent.Identifier = database.GetEventId(newEvent);
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
            Dictionary<string, int> distanceDict = [];
            // DistanceTranslationDict holds a new distance id and translates it from the old distance with the same name.
            Dictionary<int, int> distanceTranslationDict = [];
            foreach (Distance d in distances)
            {
                distanceDict[d.Name] = d.Identifier;
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
                distanceTranslationDict[distanceDict[newD.Name]] = newD.Identifier;
            }
            // Translate linked distance id's.
            // this is a separate process due to potential issues with ordering
            foreach (Distance newD in newDistances.Where(newD => Constants.Timing.DISTANCE_NO_LINKED_ID != newD.LinkedDistance))
            {
                newD.LinkedDistance = distanceTranslationDict.GetValueOrDefault(newD.LinkedDistance, Constants.Timing.DISTANCE_NO_LINKED_ID);
                database.UpdateDistance(newD);
            }
            // Get locations from old event.
            List<TimingLocation> locations = database.GetTimingLocations(oldEventId);
            List<TimingLocation> newLocations = [];
            // translates a location name into the old distance identifier
            Dictionary<string, int> locationDict = [];
            // translates the old location id to the new location id
            Dictionary<int, int> locationTranslationDict = [];
            foreach (TimingLocation loc in locations)
            {
                loc.EventIdentifier = newEvent.Identifier;
                newLocations.Add(loc);
                locationDict[loc.Name] = loc.Identifier;
            }
            // Update database with new locations
            database.AddTimingLocations(newLocations);
            // retrieve newly added locations
            newLocations = database.GetTimingLocations(newEvent.Identifier);
            foreach (TimingLocation newLoc in newLocations)
            {
                locationTranslationDict[locationDict[newLoc.Name]] = newLoc.Identifier;
            }
            // Get old segments from the database.
            List<Segment> segments = database.GetSegments(oldEventId);
            List<Segment> newSegments = [];
            foreach (Segment s in segments)
            {
                s.EventId = newEvent.Identifier;
                if (newEvent.DistanceSpecificSegments && distanceTranslationDict.TryGetValue(s.DistanceId, out int tDistId))
                {
                    s.DistanceId = tDistId;
                }
                if (Constants.Timing.LOCATION_FINISH != s.LocationId && Constants.Timing.LOCATION_START != s.LocationId && locationTranslationDict.TryGetValue(s.LocationId, out int tLocId))
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
                if (!newEvent.CommonAgeGroups && distanceTranslationDict.TryGetValue(ag.DistanceId, out int tDistId))
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
            database.AddDistance(new Distance("Default Distance", newEvent.Identifier));
        }
        database.SetCurrentEvent(newEvent.Identifier);
        window.WindowFinalize();
        Close();
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        window.WindowFinalize();
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
        Close();
    }

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}