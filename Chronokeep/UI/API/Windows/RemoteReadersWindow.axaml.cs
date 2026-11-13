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
using Avalonia.Interactivity;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Network.API;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronokeepRemote;
using Chronokeep.Timing.Remote;
using Chronokeep.UI.API.Parts;
using Chronokeep.UI.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Chronokeep.UI.API.Windows;

public partial class RemoteReadersWindow : ChronokeepWindow
{
    private static RemoteReadersWindow? theOne;

    private readonly IMainWindow window;
    private readonly IdbInterface database;
    private readonly Event? theEvent;

    private readonly List<ApiObject> remoteApIs = [];

    public static RemoteReadersWindow CreateWindow(IMainWindow window, IdbInterface database)
    {
        theOne ??= new RemoteReadersWindow(window, database);
        return theOne;
    }

    private RemoteReadersWindow(IMainWindow window, IdbInterface database)
    {
        InitializeComponent();
        ChronokeepInitialize();
        this.window = window;
        this.database = database;
        theEvent = database.GetCurrentEvent();
        if (theEvent == null || theEvent.Identifier < 0)
        {
            DialogBox.AsyncShow("Unable to get event information.");
            Close();
            return;
        }
        remoteApIs = database.GetAllApi();
        remoteApIs.RemoveAll(x => x.Type != Constants.ApiConstants.CHRONOKEEP_REMOTE && x.Type != Constants.ApiConstants.CHRONOKEEP_REMOTE_SELF);
        GetReaders();
    }

    private async void GetReaders()
    {
        try
        {
            Dictionary<(int, string), RemoteReader> savedReaders = [];
            foreach (RemoteReader reader in database.GetRemoteReaders(theEvent!.Identifier))
            {
                savedReaders[(reader.ApiiDentifier, reader.Name)] = reader;
            }
            // Fetch the readers from each remote API. One that can't be reached shouldn't hide the others.
            List<string> errors = [];
            foreach (ApiObject api in remoteApIs)
            {
                try
                {
                    List<RemoteReader> readers = await api.GetReaders();
                    ApiListView.Items.Add(new ApiExpanderPart(api, readers, savedReaders, database, window));
                }
                catch (ApiException ex)
                {
                    errors.Add($"{api.Nickname}: {ex.Message}");
                }
            }
            if (errors.Count > 0)
            {
                DialogBox.AsyncShow($"Unable to get readers from:\n\n{string.Join("\n", errors)}");
                if (ApiListView.Items.Count == 0)
                {
                    Close();
                    return;
                }
            }
            LoadingPanel.IsVisible = false;
            ApiListView.IsVisible = true;
        }
        catch (Exception)
        {
            Log.D("UI.API.RemoteReaders", "Error getting readers.");
        }
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        Log.D("UI.API.RemoteReaders", "Window is closed.");
        theOne = null;
        window.WindowFinalize();
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.API.RemoteReaders", "Close button clicked.");
        List<RemoteReader> readersToSave = [];
        List<RemoteReader> otherReaders = [];
        foreach (object? item in ApiListView.Items)
        {
            if (item is not ApiExpanderPart part) continue;
            Dictionary<RemoteReader, bool> downDict = part.GetAutoDownloadDictionary();
            foreach (RemoteReader reader in downDict.Keys)
            {
                if (downDict[reader])
                {
                    readersToSave.Add(reader);
                }
                else
                {
                    otherReaders.Add(reader);
                }
            }
        }
        List<RemoteReader> deleteReaders = [];
        HashSet<(int, string)> readerNames = [];
        foreach (RemoteReader reader in database.GetRemoteReaders(theEvent!.Identifier))
        {
            readerNames.Add((reader.ApiiDentifier, reader.Name));
        }

        deleteReaders.AddRange(otherReaders.Where(reader => readerNames.Contains((reader.ApiiDentifier, reader.Name))));
        database.DeleteRemoteReaders(theEvent.Identifier, deleteReaders);
        database.AddRemoteReaders(theEvent.Identifier, readersToSave);
        // notify main window to update/start remote reader thread
        RemoteReadersNotifier.GetRemoteReadersNotifier().Notify();
        Close();
    }

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}
