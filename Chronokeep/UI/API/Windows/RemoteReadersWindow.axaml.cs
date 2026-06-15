using Avalonia;
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

namespace Chronokeep.UI.API.Windows;

public partial class RemoteReadersWindow : Window
{
    private static RemoteReadersWindow? theOne = null;

    private readonly IMainWindow window;
    private readonly IDBInterface database;
    private readonly Event? theEvent;

    private readonly List<APIObject> remoteAPIs = [];

    public static RemoteReadersWindow CreateWindow(IMainWindow window, IDBInterface database)
    {
        theOne ??= new(window, database);
        return theOne;
    }

    private RemoteReadersWindow(IMainWindow window, IDBInterface database)
    {
        InitializeComponent();
        this.window = window;
        this.database = database;
        this.MinWidth = 10;
        this.MinHeight = 10;
        theEvent = database.GetCurrentEvent();
        if (theEvent == null || theEvent.Identifier < 0)
        {
            DialogBox.Show("Unable to get event information.");
            this.Close();
            return;
        }
        remoteAPIs = database.GetAllAPI();
        remoteAPIs.RemoveAll(x => x.Type != Constants.APIConstants.CHRONOKEEP_REMOTE && x.Type != Constants.APIConstants.CHRONOKEEP_REMOTE_SELF);
        GetReaders();
    }

    private async void GetReaders()
    {
        try
        {
            Dictionary<(int, string), RemoteReader> savedReaders = [];
            foreach (RemoteReader reader in database.GetRemoteReaders(theEvent!.Identifier))
            {
                savedReaders[(reader.APIIDentifier, reader.Name)] = reader;
            }
            // fetch all readers from the remote apis
            foreach (APIObject api in remoteAPIs)
            {
                var readers = await api.GetReaders();
                apiListView.Items.Add(new APIExpanderPart(api, readers, savedReaders, database, window));
            }
        }
        catch (APIException ex)
        {
            DialogBox.Show(ex.Message);
            Close();
            return;
        }
        loadingPanel.IsVisible = false;
        apiListView.IsVisible = true;
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        Log.D("UI.API.RemoteReaders", "Window is closed.");
        theOne = null;
        window.WindowFinalize(this);
    }

    private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.API.RemoteReaders", "Close button clicked.");
        List<RemoteReader> readersToSave = [];
        List<RemoteReader> otherReaders = [];
        foreach (object? item in apiListView.Items)
        {
            if (item is APIExpanderPart part)
            {
                var downDict = part.GetAutoDownloadDictionary();
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
        }
        List<RemoteReader> deleteReaders = [];
        HashSet<(int, string)> readerNames = [];
        foreach (RemoteReader reader in database.GetRemoteReaders(theEvent!.Identifier))
        {
            readerNames.Add((reader.APIIDentifier, reader.Name));
        }
        foreach (RemoteReader reader in otherReaders)
        {
            if (readerNames.Contains((reader.APIIDentifier, reader.Name)))
            {
                deleteReaders.Add(reader);
            }
        }
        database.DeleteRemoteReaders(theEvent.Identifier, deleteReaders);
        database.AddRemoteReaders(theEvent.Identifier, readersToSave);
        // notify mainwindow to update/start remote reader thread
        RemoteReadersNotifier.GetRemoteReadersNotifier().Notify();
        Close();
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