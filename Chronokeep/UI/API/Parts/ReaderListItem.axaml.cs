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
using Chronokeep.Network.API;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronokeepRemote;
using Chronokeep.UI.Util;
using System;
using System.Collections.Generic;
using Avalonia.Interactivity;

namespace Chronokeep.UI.API.Parts;

public partial class ReaderListItem : UserControl
{
    private readonly RemoteReader reader;
    private readonly ApiObject api;
    private readonly IdbInterface database;
    private readonly IMainWindow mWindow;

    public ReaderListItem(
        RemoteReader reader,
        ApiObject api,
        Dictionary<(int, string), RemoteReader> savedReaders,
        IdbInterface database,
        IMainWindow mWindow
        )
    {
        InitializeComponent();
        this.database = database;
        this.mWindow = mWindow;
        this.api = api;
        this.reader = reader;
        Event? theEvent = database.GetCurrentEvent();
        if (theEvent == null || theEvent.Identifier < 1)
        {
            return;
        }
        this.reader.EventId = theEvent.Identifier;
        List<TimingLocation> locations = database.GetTimingLocations(theEvent.Identifier);
        locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_ANNOUNCER, theEvent.Identifier, "Announcer", 0, 0));
        if (!theEvent.CommonStartFinish)
        {
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Finish", theEvent.FinishMaxOccurrences, theEvent.FinishIgnoreWithin));
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_START, theEvent.Identifier, "Start", 0, theEvent.StartWindow));
        }
        else
        {
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Start/Finish", theEvent.FinishMaxOccurrences, theEvent.FinishIgnoreWithin));
        }
        AutoFetch.IsChecked = savedReaders.ContainsKey((reader.ApiiDentifier, reader.Name));
        NameBlock.Text = reader.Name;
        int selectedIndex = 0;
        for (int i=0; i<locations.Count; i++)
        {
            if (reader.LocationId == locations[i].Identifier)
            {
                selectedIndex = i;
            }
            LocationBox.Items.Add(new ComboBoxItem()
            {
                Content = locations[i].Name,
                Tag = locations[i].Identifier.ToString()
            });
        }
        LocationBox.SelectedIndex = selectedIndex;
        DateTime date = DateTime.Now;
        StartDatePicker.SelectedDate = date;
        EndDatePicker.SelectedDate = date;
    }

    public RemoteReader GetUpdatedReader()
    {
        RemoteReader output = new()
        {
            Name = reader.Name,
            EventId = reader.EventId,
            ApiiDentifier = api.Identifier
        };
        if (LocationBox.SelectedItem != null && int.TryParse((string)((ComboBoxItem)LocationBox.SelectedItem).Tag!, out int locId))
        {
            output.LocationId = locId;
        }
        else
        {
            output.LocationId = Constants.Timing.LOCATION_FINISH;
        }
        return output;
    }

    public bool AutoDownloadReads()
    {
        return AutoFetch.IsChecked == true;
    }

    private async void Rewind_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.API.RemoteReadersWindow.ReaderListItem", "Rewind clicked.");
            if (!DateTime.TryParse($"{StartDatePicker.SelectedDate?.ToString("yyyy/M/d") ?? DateTime.Now.ToString("yyyy/M/d")} {StartTimeBox.Text!.Replace('_', '0')}", out DateTime startDate))
            {
                startDate = DateTime.Now;
            }
            if (!DateTime.TryParse($"{EndDatePicker.SelectedDate?.ToString("yyyy/M/d") ?? DateTime.Now.ToString("yyyy/M/d")} {EndTimeBox.Text!.Replace('_', '0')}", out DateTime endDate))
            {
                endDate = DateTime.Now;
            }
            try
            {
                Event? theEvent = database.GetCurrentEvent();
                if (theEvent == null || theEvent.Identifier < 1)
                {
                    return;
                }
                reader.EventId = theEvent.Identifier;
                reader.LocationId = LocationBox.SelectedItem == null ? Constants.Timing.LOCATION_FINISH : Convert.ToInt32(((ComboBoxItem)LocationBox.SelectedItem).Tag);
                (List<ChipRead> reads, RemoteNotification _) = await api.GetReads(reader, startDate, endDate);
                database.AddChipReads(reads);
                mWindow.UpdateTimingFromController();
                DialogBox.AsyncShow("Rewind complete.");
            }
            catch (ApiException ex)
            {
                DialogBox.AsyncShow(ex.Message);
            }
        }
        catch (Exception)
        {
            Log.D("UI.API.RemoteReadersWindow.ReaderListItem", "Error rewinding.");
        }
    }

    private void Delete_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.API.RemoteReadersWindow.ReaderListItem", "Delete clicked.");
        DialogBox.AsyncShow(
            "Warning!\n\nThis will delete every read uploaded to the remote api. That data cannot be recoverred once deleted.",
            "Delete",
            "Cancel",
            async void () =>
            {
                try
                {
                    Log.D("UI.API.RemoteReadersWindow.ReaderListItem", "User requests deletion.");
                    if (!DateTime.TryParse($"{StartDatePicker.SelectedDate?.ToString("yyyy/M/d") ?? DateTime.Now.ToString("yyyy/M/d")} {StartTimeBox.Text!.Replace('_', '0')}", out DateTime startDate))
                    {
                        startDate = DateTime.Now;
                    }
                    if (!DateTime.TryParse($"{EndDatePicker.SelectedDate?.ToString("yyyy/M/d") ?? DateTime.Now.ToString("yyyy/M/d")} {EndTimeBox.Text!.Replace('_', '0')}", out DateTime endDate))
                    {
                        endDate = DateTime.Now;
                    }
                    try
                    {
                        long count = await api.DeleteReads(reader, startDate, endDate);
                        mWindow.UpdateTimingFromController();
                        DialogBox.AsyncShow($"Successfully deleted\n\n{count}\n\nreads.");
                    }
                    catch (ApiException ex)
                    {
                        DialogBox.AsyncShow(ex.Message);
                    }
                }
                catch (Exception)
                {
                    Log.D("UI.API.RemoteReadersWindow.ReaderListItem", "Error deleting reads.");
                }
            });
    }
}
