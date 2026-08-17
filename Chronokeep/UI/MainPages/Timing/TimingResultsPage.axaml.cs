/*
Chronokeep Desktop - Race Scoring Software
Copyright (C) 2026 James Sentinella

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using Avalonia;
using Avalonia.Controls;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.UI.Participants;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Chronokeep.UI.MainPages.Timing;

public partial class TimingResultsPage : UserControl, ISubPage
{
    private readonly TimingPage parent;
    private readonly IdbInterface database;
    private readonly Event? theEvent;

    private readonly ObservableCollection<TimeResult> results = [];

    public TimingResultsPage(TimingPage parent, IdbInterface database)
    {
        InitializeComponent();
        this.parent = parent;
        this.database = database;
        theEvent = database.GetCurrentEvent();
        if (database is SqLiteInterface)
        {
            Database.SQLite.Results.GetStaticVariables(database);
        }
        if (theEvent is { EventType: Constants.Timing.EVENT_TYPE_TIME or Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA })
        {
            ChipTimeHeader.Text = "Lap Time";
        }
        if (!theEvent!.DisplayPlacements)
        {
            HidePlacements();
        }
        UpdateListView.ItemsSource = results;
        UpdateView();
    }

    public void Closing() { }

    public void KeyboardCtrlA() { }

    public void KeyboardCtrlS() { }

    public void KeyboardCtrlZ() { }

    private void Customize(
        SortType sortType,
        PeopleType peopleType,
        List<TimeResult> newResults,
        string search,
        string location)
    {
        switch (peopleType)
        {
            case PeopleType.DEFAULT:
                newResults.RemoveAll(TimeResult.StartTimes);
                break;
            case PeopleType.KNOWN:
                newResults.RemoveAll(TimeResult.IsNotKnown);
                break;
            case PeopleType.UNKNOWN:
                newResults.RemoveAll(TimeResult.IsKnown);
                break;
            case PeopleType.UNKNOWN_FINISHES when Constants.Timing.EVENT_TYPE_TIME == theEvent!.EventType:
                {
                    Log.D("UI.Timing.TimingResultsPage", "Time based event.");
                    Dictionary<int, TimeResult> validResults = [];
                    foreach (TimeResult result in newResults.Where(result => Constants.Timing.TIMERESULT_DUMMYPERSON != result.EventSpecificId))
                    {
                        validResults[result.EventSpecificId] = result;
                    }
                    newResults.RemoveAll(x => !validResults.ContainsValue(x) && TimeResult.IsKnown(x));
                    break;
                }
            case PeopleType.UNKNOWN_FINISHES:
                newResults.RemoveAll(TimeResult.IsNotFinishOrKnown);
                break;
            case PeopleType.UNKNOWN_STARTS:
                newResults.RemoveAll(TimeResult.IsNotStartOrKnown);
                break;
            case PeopleType.FINISHES when Constants.Timing.EVENT_TYPE_TIME == theEvent!.EventType:
                {
                    Log.D("UI.Timing.TimingResultsPage", "Time based event.");
                    Dictionary<int, TimeResult> validResults = [];
                    foreach (TimeResult result in newResults.Where(result => Constants.Timing.TIMERESULT_DUMMYPERSON != result.EventSpecificId))
                    {
                        validResults[result.EventSpecificId] = result;
                    }
                    newResults.RemoveAll(x => !validResults.ContainsValue(x));
                    break;
                }
            case PeopleType.FINISHES:
                newResults.RemoveAll(TimeResult.IsNotFinish);
                break;
            case PeopleType.STARTS:
                newResults.RemoveAll(TimeResult.IsNotStart);
                break;
            case PeopleType.ALL:
            default:
                break;
        }
        newResults.RemoveAll(result => result.IsNotMatch(search));
        Log.D("UI.Timing.TimingResultsPage", $"Removing all location based items. {location}");
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
            await Task.Run(() =>
            {
                Customize(sortType, peopleType, newResults, search, location);
            });
            int oldCount = results.Count;
            results.Clear();
            foreach (TimeResult timeResult in newResults)
            {
                results.Add(timeResult);
            }
            if (newResults.Count > 0 && newResults.Count > oldCount)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    UpdateListView.ScrollIntoView(results.Count - 1);
                });
            }
        }
        catch (Exception)
        {
            Log.D("UI.Timing.TimingResultsPage", "Error updating view.");
        }
    }

    private void HidePlacements()
    {
        PlaceHeader.IsVisible = false;
        GenderPlaceHeader.IsVisible = false;
        AgePlaceHeader.IsVisible = false;
        DivisionPlaceHeader.IsVisible = false;
    }

    public void CancelableUpdateView(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        UpdateView();
    }

    public void Search(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        UpdateView();
    }

    private void UpdateListView_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (UpdateListView.SelectedItem == null) return;
        TimeResult selected = (TimeResult)UpdateListView.SelectedItem;
        ModifyParticipantWindow modifyParticipant = new(parent, database, selected.EventSpecificId, selected.Bib);
        modifyParticipant.Show();
    }
}
