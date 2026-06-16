using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Database;
using Chronokeep.Database.SQLite;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.UI.Parts;
using System.Collections.Generic;
using System.Linq;

namespace Chronokeep.UI.MainPages;

public partial class LocationsPage : UserControl, IMainPage
{
    private readonly IMainWindow mWindow;
    private readonly IDBInterface database;
    private readonly Event? theEvent;
    private int locationCount = 1;
    private bool updateTimingWorker;

    public LocationsPage(IMainWindow mWindow, IDBInterface database)
    {
        InitializeComponent();
        this.mWindow = mWindow;
        this.database = database;
        theEvent = database.GetCurrentEvent();
        UpdateView();
    }

    public void UpdateView()
    {
        if (theEvent == null || theEvent.Identifier < 0)
        {
            return;
        }
        LocationsBox.Items.Clear();
        LocationsBox.Items.Add(new LocationPart(this, new TimingLocation(Constants.Timing.LOCATION_START, theEvent.Identifier, "Start", theEvent.StartMaxOccurrences, theEvent.StartWindow), theEvent));
        LocationsBox.Items.Add(new LocationPart(this, new TimingLocation(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Finish", theEvent.FinishMaxOccurrences, theEvent.FinishIgnoreWithin), theEvent));
        List<TimingLocation> locations = database.GetTimingLocations(theEvent.Identifier);
        locationCount = 1;
        locations.Sort();
        foreach (TimingLocation loc in locations)
        {
            LocationsBox.Items.Add(new LocationPart(this, loc, theEvent));
            locationCount = loc.Identifier > locationCount - 1 ? loc.Identifier + 1 : locationCount;
        }
    }

    internal void RemoveLocation(TimingLocation location)
    {
        Log.D("UI.MainPages.LocationsPage", "Removing a location.");
        if (database.GetAppSetting(Constants.Settings.UPDATE_ON_PAGE_CHANGE)!.Value == Constants.Settings.SETTING_TRUE)
        {
            UpdateDatabase();
        }
        if (location.Identifier == Constants.Timing.LOCATION_FINISH || location.Identifier == Constants.Timing.LOCATION_START)
        {
            Log.E("UI.MainPages.LocationsPage", "Somehow they told us to delete the start/finish location.");
        }
        else
        {
            database.RemoveTimingLocation(location);
        }
        updateTimingWorker = true;
        UpdateView();
    }

    private void UpdateDatabase()
    {
        foreach (LocationPart? locItem in LocationsBox.Items.Cast<LocationPart?>())
        {
            locItem!.UpdateLocation();
            if (locItem.MyLocation.Identifier == Constants.Timing.LOCATION_FINISH)
            {
                if (theEvent!.FinishMaxOccurrences == locItem.MyLocation.MaxOccurrences
                    && theEvent.FinishIgnoreWithin == locItem.MyLocation.IgnoreWithin) continue;
                theEvent.FinishMaxOccurrences = locItem.MyLocation.MaxOccurrences;
                theEvent.FinishIgnoreWithin = locItem.MyLocation.IgnoreWithin;
                database.SetFinishOptions(theEvent);
            }
            else if (locItem.MyLocation.Identifier == Constants.Timing.LOCATION_START)
            {
                if (theEvent!.StartWindow == locItem.MyLocation.IgnoreWithin
                    && theEvent.StartMaxOccurrences == locItem.MyLocation.MaxOccurrences) continue;
                theEvent.StartWindow = locItem.MyLocation.IgnoreWithin;
                theEvent.StartMaxOccurrences = locItem.MyLocation.MaxOccurrences;
                database.SetStartOptions(theEvent);
            }
            else
            {
                if (!locItem.IsUpdated()) continue;
                database.UpdateTimingLocation(locItem.MyLocation);
            }

            updateTimingWorker = true;
        }
        if (database is SQLiteInterface)
        {
            Results.GetStaticVariables(database);
        }
    }

    public void Keyboard_Ctrl_A()
    {
        Add_Click(null, null);
    }

    public void Keyboard_Ctrl_S()
    {
        UpdateDatabase();
        UpdateView();
    }

    public void Keyboard_Ctrl_Z()
    {
        UpdateView();
    }

    public void Closing()
    {
        Log.D("UI.MainPages.LocationsPage", "Location page closing.");
        if (database.GetAppSetting(Constants.Settings.UPDATE_ON_PAGE_CHANGE)!.Value == Constants.Settings.SETTING_TRUE)
        {
            UpdateDatabase();
        }
        if (!updateTimingWorker) return;
        Log.D("UI.MainPages.LocationsPage", "Resetting results.");
        database.ResetTimingResultsEvent(theEvent!.Identifier);
        mWindow.NetworkClearResults();
        mWindow.NotifyTimingWorker();
    }

    private void Add_Click(object? sender, RoutedEventArgs? e)
    {
        Log.D("UI.MainPages.LocationsPage", "Add Location clicked.");
        if (database.GetAppSetting(Constants.Settings.UPDATE_ON_PAGE_CHANGE)!.Value == Constants.Settings.SETTING_TRUE)
        {
            UpdateDatabase();
        }
        database.AddTimingLocation(new TimingLocation(theEvent!.Identifier, "Location " + locationCount));
        updateTimingWorker = true;
        UpdateView();
    }

    private void Update_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.LocationsPage", "Update all clicked.");
        UpdateDatabase();
        UpdateView();
    }

    private void ResetBtn_Click(object? sender, RoutedEventArgs e) { }
}