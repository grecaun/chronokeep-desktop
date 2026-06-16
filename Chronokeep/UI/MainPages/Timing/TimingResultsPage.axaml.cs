using Avalonia.Controls;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Chronokeep.UI.MainPages.Timing;

public partial class TimingResultsPage : UserControl, ISubPage
{
    private readonly TimingPage parent;
    private readonly IDBInterface database;
    private readonly Event? theEvent;

    private readonly List<TimeResult> results = [];

    public TimingResultsPage(TimingPage parent, IDBInterface database)
    {
        InitializeComponent();
        this.parent = parent;
        this.database = database;
        theEvent = database.GetCurrentEvent();
        UpdateListView.ItemsSource = results;
        if (Constants.Timing.EVENT_TYPE_TIME == theEvent!.EventType)
        {
            UpdateListView.Columns[4].Header = "Lap Time";
        }
        if (database is SQLiteInterface)
        {
            Database.SQLite.Results.GetStaticVariables(database);
        }
        parent.SetReaders([], false);
        UpdateView();
    }

    public void Closing() { }

    public void EditSelected() { }

    public void Keyboard_Ctrl_A() { }

    public void Keyboard_Ctrl_S() { }

    public void Keyboard_Ctrl_Z() { }

    private void Customize(
        SortType sortType,
        PeopleType peopleType,
        List<TimeResult> newResults,
        string search,
        string location)
    {
        if (peopleType == PeopleType.DEFAULT)
        {
            newResults.RemoveAll(TimeResult.StartTimes);
        }
        else if (peopleType == PeopleType.KNOWN)
        {
            newResults.RemoveAll(TimeResult.IsNotKnown);
        }
        else if (peopleType == PeopleType.UNKNOWN)

        {

            newResults.RemoveAll(TimeResult.IsKnown);

        }
        else if (peopleType == PeopleType.UNKNOWN_FINISHES)

        {
            if (Constants.Timing.EVENT_TYPE_TIME == theEvent!.EventType)
            {
                Log.D("UI.Timing.TimingResultsPage", "Time based event.");
                Dictionary<int, TimeResult> validResults = [];
                foreach (TimeResult result in newResults.Where(result => Constants.Timing.TIMERESULT_DUMMYPERSON != result.EventSpecificId))
                {
                    validResults[result.EventSpecificId] = result;
                }
                newResults.RemoveAll(x => !validResults.ContainsValue(x) && TimeResult.IsKnown(x));
            }
            else
            {
                newResults.RemoveAll(TimeResult.IsNotFinishOrKnown);
            }

        }
        else if (peopleType == PeopleType.UNKNOWN_STARTS)

        {
            newResults.RemoveAll(TimeResult.IsNotStartOrKnown);

        }
        else if (peopleType == PeopleType.FINISHES)
        {
            if (Constants.Timing.EVENT_TYPE_TIME == theEvent!.EventType)
            {
                Log.D("UI.Timing.TimingResultsPage", "Time based event.");
                Dictionary<int, TimeResult> validResults = [];
                foreach (TimeResult result in newResults.Where(result => Constants.Timing.TIMERESULT_DUMMYPERSON != result.EventSpecificId))
                {
                    validResults[result.EventSpecificId] = result;
                }
                newResults.RemoveAll(x => !validResults.ContainsValue(x));
            }
            else
            {
                newResults.RemoveAll(TimeResult.IsNotFinish);
            }
        }
        else if (peopleType == PeopleType.STARTS)
        {
            newResults.RemoveAll(TimeResult.IsNotStart);
        }
        newResults.RemoveAll(result => result.IsNotMatch(search));
        Log.D("UI.Timing.TimingResultsPage", "Removing all location based items. " + location);
        if (location.Length > 0 && !location.Equals("All Locations", StringComparison.OrdinalIgnoreCase))
        {
            newResults.RemoveAll(read => !read.LocationName.Equals(location, StringComparison.OrdinalIgnoreCase));
        }
        switch (sortType)
        {
            case SortType.BIB:
                newResults.Sort(TimeResult.CompareByBib);
                break;
            case SortType.GUNTIME:
                newResults.Sort(TimeResult.CompareByGunTime);
                break;
            case SortType.DISTANCE:
                newResults.Sort(TimeResult.CompareByDistance);
                break;
            case SortType.AGEGROUP:
                newResults.Sort(TimeResult.CompareByAgeGroup);
                break;
            case SortType.GENDER:
                newResults.Sort(TimeResult.CompareByGender);
                break;
            case SortType.PLACE:
                newResults.Sort(TimeResult.CompareByDistancePlace);
                break;
            case SortType.SYSTIME:
            default:
                newResults.Sort(TimeResult.CompareBySystemTime);
                break;
        }
    }

    public async void SortBy(SortType sortType)
    {
        try
        {
            List<TimeResult> newResults = [.. results];
            PeopleType peopleType = parent.GetPeopleType();
            string search = parent.GetSearchValue();
            string location = parent.GetLocation();
            await Task.Run(() =>
            {
                Customize(sortType, peopleType, newResults, search, location);
            });
            UpdateListView.ItemsSource = newResults;
            if (newResults.Count > 0)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    UpdateListView.ScrollIntoView(newResults[^1], null);
                });
            }
        }
        catch (Exception)
        {
            Log.D("UI.Timing.TimingResultsPage", "Error sorting.");
        }
    }

    public async void Location(string location)
    {
        try
        {
            List<TimeResult> newResults = [.. results];
            PeopleType peopleType = parent.GetPeopleType();
            SortType sortType = parent.GetSortType();
            string search = parent.GetSearchValue();
            await Task.Run(() =>
            {
                Customize(sortType, peopleType, newResults, search, location);
            });
            UpdateListView.ItemsSource = newResults;
            if (newResults.Count > 0)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    UpdateListView.ScrollIntoView(newResults[^1], null);
                });
            }
        }
        catch (Exception)
        {
            Log.D("UI.Timing.TimingResultsPage", "Error customizing based on location.");
        }
    }

    public async void UpdateView()
    {
        try
        {
            List<TimeResult> newResults = [];
            SortType sortType = parent.GetSortType();
            PeopleType peopleType = parent.GetPeopleType();
            string search = parent.GetSearchValue();
            string location = parent.GetLocation();
            await Task.Run(() =>
            {
                newResults = database.GetTimingResults(theEvent!.Identifier);
            });
            results.Clear();
            results.AddRange(newResults);
            await Task.Run(() =>
            {
                Customize(sortType, peopleType, newResults, search, location);
            });
            UpdateListView.ItemsSource = newResults;
            if (newResults.Count > 0)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    UpdateListView.ScrollIntoView(newResults[^1], null);
                });
            }
            if (theEvent!.DisplayPlacements)
            {
                DisplayPlacements();
            }
            else
            {
                HidePlacements();
            }
        }
        catch (Exception)
        {
            Log.D("UI.Timing.TimingResultsPage", "Error updating view.");
        }
    }

    private void DisplayPlacements()
    {
        UpdateListView.Columns[7].IsVisible = true;
        UpdateListView.Columns[9].IsVisible = true;
        UpdateListView.Columns[11].IsVisible = true;
        UpdateListView.Columns[13].IsVisible = true;
    }

    private void HidePlacements()
    {
        UpdateListView.Columns[7].IsVisible = false;
        UpdateListView.Columns[9].IsVisible = false;
        UpdateListView.Columns[11].IsVisible = false;
        UpdateListView.Columns[13].IsVisible = false;
    }

    public void CancelableUpdateView(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        UpdateView();
    }

    public void Search(CancellationToken token, string searchText)
    {
        token.ThrowIfCancellationRequested();
        UpdateView();
    }

    public async void Show(PeopleType peopleType)
    {
        try
        {
            List<TimeResult> newResults = [.. results];
            SortType sortType = parent.GetSortType();
            string search = parent.GetSearchValue();
            string location = parent.GetLocation();
            await Task.Run(() =>
            {
                Customize(sortType, peopleType, newResults, search, location);
            });
            UpdateListView.ItemsSource = newResults;
            if (newResults.Count > 0)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    UpdateListView.ScrollIntoView(newResults[^1], null);
                });
            }
        }
        catch (Exception)
        {
            Log.D("UI.Timing.TimingResultsPage", "Error limiting view.");
        }
    }

    public void Reader(string reader) { }
}