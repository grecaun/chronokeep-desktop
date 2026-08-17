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

using Avalonia.Controls;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.UI.Timing.Windows;
using Chronokeep.UI.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Chronokeep.UI.MainPages.Timing;

public partial class TimingRawReadsPage : UserControl, ISubPage
{
    private readonly IdbInterface database;
    private readonly ITimingPage parent;
    private readonly Event? theEvent;
    private readonly IMainWindow mWindow;

    private readonly List<ChipRead> chipReads = [];

    public TimingRawReadsPage(ITimingPage parent, IdbInterface database, IMainWindow mWindow)
    {
        InitializeComponent();
        Log.D("UI.Timing.TimingRawReadsPage", "Page initialized.");
        this.parent = parent;
        this.database = database;
        this.mWindow = mWindow;
        theEvent = database.GetCurrentEvent();
        Log.D("UI.Timing.TimingRawReadsPage", "Current event fetched.");
        switch (parent)
        {
            case TimingPage:
                PrivateUpdateView();
                break;
            case MinTimingPage:
                SafeModeUpdateView();
                break;
        }
        Log.D("UI.Timing.TimingRawReadsPage", "View updated.");
        this.mWindow = mWindow;
        if (parent is not MinTimingPage) return;
        DoneButton.IsEnabled = false;
        DoneButton.IsVisible = false;
    }

    public void UpdateView()
    {
        Log.D("UI.Timing.TimingRawReadsPage", "Update View called.");
    }

    public void CancelableUpdateView(CancellationToken token) { }

    public void Search(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        PrivateUpdateView();
    }

    internal void PrivateUpdateView()
    {
        List<ChipRead> reads = [];
        SortType sortType = parent.GetSortType();
        PeopleType peopleType = parent.GetPeopleType();
        string location = parent.GetLocation();
        string readerName = parent.GetReader();
        reads.AddRange(database.GetChipReads(theEvent!.Identifier));
        chipReads.Clear();
        chipReads.AddRange(reads);
        HashSet<string> readerNames = [];
        foreach (ChipRead read in chipReads)
        {
            readerNames.Add(read.Box);
        }
        parent.SetReaders(["All Readers", .. readerNames], true);
        string search = parent.GetSearchValue();
        bool manualOnly = OnlyManualBox.IsChecked == true;
        bool ignoredOnly = OnlyIgnoreBox.IsChecked == true;
        SortWorker(reads, sortType, peopleType, search, manualOnly, location, ignoredOnly, readerName);
        UpdateListView.SelectedItems?.Clear();
        UpdateListView.ItemsSource = reads;
        if (reads.Count > 0)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => { UpdateListView.ScrollIntoView(reads.Count - 1); });
        }
    }

    internal void SafeModeUpdateView()
    {
        if (theEvent == null)
        {
            return;
        }
        List<ChipRead> reads = [];
        SortType sortType = parent.GetSortType();
        PeopleType peopleType = parent.GetPeopleType();
        reads.AddRange(database.GetChipReadsSafemode(theEvent.Identifier));
        chipReads.Clear();
        chipReads.AddRange(reads);
        string search = parent.GetSearchValue();
        string location = parent.GetLocation();
        string readerName = parent.GetReader();
        bool manualOnly = OnlyManualBox.IsChecked == true;
        bool ignoredOnly = OnlyIgnoreBox.IsChecked == true;
        SortWorker(reads, sortType, peopleType, search, manualOnly, location, ignoredOnly, readerName);
        UpdateListView.SelectedItems?.Clear();
        UpdateListView.ItemsSource = reads;
        if (reads.Count > 0)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => { UpdateListView.ScrollIntoView(reads.Count - 1); });
        }
    }

    public void Closing() { }

    public static void UpdateDatabase() { }

    public void KeyboardCtrlA() { }

    public void KeyboardCtrlS() { }

    public void KeyboardCtrlZ() { }

    private static void SortWorker(
        List<ChipRead> reads,
        SortType sortType,
        PeopleType peopleType,
        string search,
        bool manualOnly,
        string location,
        bool ignoredOnly,
        string reader
        )
    {
        if (peopleType == PeopleType.UNKNOWN)
        {
            reads.RemoveAll(read => read.Name.Length > 0);
        }
        reads.RemoveAll(read => read.IsNotMatch(search));
        if (manualOnly)
        {
            reads.RemoveAll(read => read.Type == Constants.Timing.CHIPREAD_TYPE_CHIP);
        }
        if (ignoredOnly)
        {
            reads.RemoveAll(read =>
                read.Status != Constants.Timing.CHIPREAD_STATUS_DNF_IGNORE
                && read.Status != Constants.Timing.CHIPREAD_STATUS_DNS_IGNORE
                && read.Status != Constants.Timing.CHIPREAD_STATUS_IGNORE
                );
        }
        if (!string.IsNullOrEmpty(location) && !location.Equals("All Locations", StringComparison.OrdinalIgnoreCase))
        {
            reads.RemoveAll(read => !read.LocationName.Equals(location, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrEmpty(reader) && !reader.Equals("All Readers", StringComparison.OrdinalIgnoreCase))
        {
            reads.RemoveAll(read => !read.Box.Equals(reader, StringComparison.OrdinalIgnoreCase));
        }
        if (sortType == SortType.BIB)
        {
            reads.Sort(ChipRead.CompareByBib);
        }
        else
        {
            reads.Sort();
        }
    }

    private void OnlyIgnoreBox_Unchecked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.TimingRawReadsPage", "Manual entries only box checked status changed.");
        switch (parent)
        {
            case TimingPage:
                PrivateUpdateView();
                break;
            case MinTimingPage:
                SafeModeUpdateView();
                break;
        }
    }

    private void OnlyManualBox_Unchecked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.TimingRawReadsPage", "Manual entries only box checked status changed.");
        switch (parent)
        {
            case TimingPage:
                PrivateUpdateView();
                break;
            case MinTimingPage:
                SafeModeUpdateView();
                break;
        }
    }

    private void DeleteButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.TimingRawReadsPage", "Delete clicked.");
        DialogBox.AsyncShow(
            "Are you sure you wish to delete these records? They cannot be recovered if you have no other record of them.",
            "Yes",
            "No",
            () =>
            {
                List<ChipRead> readsToDelete = [];
                if (UpdateListView.SelectedItems == null) return;
                readsToDelete.AddRange(UpdateListView.SelectedItems.Cast<ChipRead>());
                database.DeleteChipReads(readsToDelete);
                database.ResetTimingResultsEvent(theEvent!.Identifier);
                switch (parent)
                {
                    case TimingPage:
                        PrivateUpdateView();
                        break;
                    case MinTimingPage:
                        SafeModeUpdateView();
                        break;
                }
                parent.NotifyTimingWorker();
            });
    }

    private void IgnoreButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.TimingRawReadsPage", "Ignore Button clicked.");
        if (UpdateListView.SelectedItems == null) return;
        List<ChipRead> newChipReads = [];
        foreach (ChipRead read in UpdateListView.SelectedItems)
        {
            switch (read.Status)
            {
                // Check what the previous status was. If it was FORCE_IGNORE, then we can set to NONE
                case Constants.Timing.CHIPREAD_STATUS_IGNORE:
                    read.Status = Constants.Timing.CHIPREAD_STATUS_NONE;
                    break;
                // Else if it's DNF, we need to use the special status of DNF ignore
                // so we can restore it to DNF status if we want to un-ignore the read.
                case Constants.Timing.CHIPREAD_STATUS_DNF:
                    read.Status = Constants.Timing.CHIPREAD_STATUS_DNF_IGNORE;
                    break;
                case Constants.Timing.CHIPREAD_STATUS_DNF_IGNORE:
                    read.Status = Constants.Timing.CHIPREAD_STATUS_DNF;
                    break;
                // Treat DNS the same as DNF.
                case Constants.Timing.CHIPREAD_STATUS_DNS:
                    read.Status = Constants.Timing.CHIPREAD_STATUS_DNS_IGNORE;
                    break;
                case Constants.Timing.CHIPREAD_STATUS_DNS_IGNORE:
                    read.Status = Constants.Timing.CHIPREAD_STATUS_DNS;
                    break;
                // These reads are not DNF or DNS. Don't modify announcer reads.
                default:
                {
                    if (read.Status != Constants.Timing.CHIPREAD_STATUS_ANNOUNCER_SEEN &&
                        read.Status != Constants.Timing.CHIPREAD_STATUS_ANNOUNCER_USED)
                    {
                        read.Status = Constants.Timing.CHIPREAD_STATUS_IGNORE;
                    }

                    break;
                }
            }

            newChipReads.Add(read);
        }
        database.SetChipReadStatuses(newChipReads);
        database.ResetTimingResultsEvent(theEvent!.Identifier);
        switch (parent)
        {
            case TimingPage:
                PrivateUpdateView();
                break;
            case MinTimingPage:
                SafeModeUpdateView();
                break;
        }
        parent.NotifyTimingWorker();
    }

    private void ChangeDNS_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.TimingRawReadsPage", "ChangeDNS Button clicked.");
        if (UpdateListView.SelectedItems == null) return;
        List<ChipRead> newChipReads = [];
        foreach (ChipRead read in UpdateListView.SelectedItems)
        {
            // Check what the previous status was. If it was STATUS_DNS we change it to NONE
            read.Status = read.Status == Constants.Timing.CHIPREAD_STATUS_DNS ? Constants.Timing.CHIPREAD_STATUS_NONE :
                // Else set it to DNS
                Constants.Timing.CHIPREAD_STATUS_DNS;
            newChipReads.Add(read);
        }
        database.SetChipReadStatuses(newChipReads);
        database.ResetTimingResultsEvent(theEvent!.Identifier);
        switch (parent)
        {
            case TimingPage:
                PrivateUpdateView();
                break;
            case MinTimingPage:
                SafeModeUpdateView();
                break;
        }
        parent.NotifyTimingWorker();
    }

    private void ChangeDNF_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.TimingRawReadsPage", "ChangeDNF Button clicked.");
        if (UpdateListView.SelectedItems == null) return;
        List<ChipRead> newChipReads = [];
        foreach (ChipRead read in UpdateListView.SelectedItems)
        {
            // Check what the previous status was. If it was STATUS_DNF we change it to NONE
            read.Status = read.Status == Constants.Timing.CHIPREAD_STATUS_DNF ? Constants.Timing.CHIPREAD_STATUS_NONE :
                // Else set it to DNF
                Constants.Timing.CHIPREAD_STATUS_DNF;
            newChipReads.Add(read);
        }
        database.SetChipReadStatuses(newChipReads);
        database.ResetTimingResultsEvent(theEvent!.Identifier);
        switch (parent)
        {
            case TimingPage:
                PrivateUpdateView();
                break;
            case MinTimingPage:
                SafeModeUpdateView();
                break;
        }
        parent.NotifyTimingWorker();
    }

    private void Shift_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.TimingRawReadsPage", "Shift button clicked.");
        if (UpdateListView.SelectedItems == null) return;
        List<ChipRead> localReads = [];
        localReads.AddRange(UpdateListView.SelectedItems.Cast<ChipRead>());
        EditRawReadsWindow editRawReadsWindow = new(parent, database, localReads);
        editRawReadsWindow.ShowDialog((Window)mWindow);
    }

    private void GlobalIgnore_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.TimingRawReadsPage", "Ignore Chip Button clicked.");
        if (UpdateListView.SelectedItems == null) return;
        List<BibChipAssociation> bibChips = [];
        bibChips.AddRange(from ChipRead read in UpdateListView.SelectedItems select new BibChipAssociation() { Bib = read.ChipNumber, Chip = read.ChipNumber, });
        database.AddBibChipAssociation(-1, bibChips);
        Globals.UpdateIgnoredChips(database);
    }

    private void DoneButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.TimingRawReadsPage", "Done Button clicked.");
        parent.SetReaders([], false);
        parent.LoadMainDisplay();
    }
}
