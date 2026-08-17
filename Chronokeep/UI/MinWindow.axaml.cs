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
using Avalonia.Interactivity;
using Avalonia.Styling;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronokeepRemote;
using Chronokeep.Timing;
using Chronokeep.Timing.Remote;
using Chronokeep.UI.EventWindows;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.Util;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chronokeep.Constants;
using static Chronokeep.Helpers.Globals;

namespace Chronokeep.UI;

public partial class MinWindow : ChronokeepWindow, IMainWindow
{
    private readonly MemStore.MemStore? database;
    private readonly MinTimingPage? page;

    // Timing objects.
    private Thread? timingControllerThread;
    private readonly TimingController? timingController;

    private readonly List<Window> openWindows = [];

    // Set up a mutex that will be unique for this program to ensure we only ever have a single instance of it running.
    // Allow for a debug version and non-debug version to run at the same time.
#if DEBUG
    private static readonly Mutex OneWindow = new(true, "{48ED48DE-6E1B-4F3B-8C5C-D0BAB5295366}-chronokeep-debug");
#else
    private static readonly Mutex OneWindow = new(true, "{48ED48DE-6E1B-4F3B-8C5C-D0BAB5295366}-chronokeep");
#endif

    public MinWindow()
    {
        InitializeComponent();
        ChronokeepInitialize();
        // Check that no other instance of this program are running.
        if (!OneWindow.WaitOne(TimeSpan.Zero, true))
        {
            DialogBox.AsyncShow("Chronokeep is already running.");
            Close();
            return;
        }
        OneWindow.ReleaseMutex();

        string dirPath = App.IsWindows ?
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), Settings.PROGRAM_DIR)
            : Path.Combine(Directory.GetCurrentDirectory(), "data");
        string path = Path.Combine(dirPath, MainWindow.DATABASE_FILE_NAME);
        Log.D("UI.MinWindow", "Looking for database file.");
        Directory.CreateDirectory(dirPath);
        if (!File.Exists(path))
        {
            Log.D("UI.MinWindow", "Creating database file.");
            SQLiteConnection.CreateFile(path);
        }
        database = MemStore.MemStore.GetMemStore(new SqLiteInterface(path));
        try
        {
            database.Initialize();
        }
        catch (InvalidDatabaseVersion db)
        {
            DialogBox.AsyncShow(
                $"Database version greater than the max known by this client. Please update the client. Database version {db.FoundVersion}. Max version for this client {db.MaxVersion}");
            Close();
            return;
        }
        Settings.SetupSettings(database);

        timingController = new TimingController(this, database);
        
        Event? theEvent = database.GetCurrentEvent();
        if (theEvent != null)
        {
            EventName.Text = theEvent.Name;
            EventDate.Text = theEvent.Date;
        }
        page = new MinTimingPage(this, database);
        TheFrame.Content = page;
    }
    
    private void NewEvent_Click(object sender, RoutedEventArgs e)
    {
        Log.D("UI.DashboardPage", "New event clicked.");
        if (CancelEventChangeAsync(EventClickType.NewEvent))
        {
            return;
        }
        NewEventWindow newEventWindow = NewEventWindow.NewWindow(this, database!);
        AddWindow(newEventWindow);
        newEventWindow.ShowDialog(this);
    }

    private void ChangeEvent_Click(object sender, RoutedEventArgs e)
    {
        Log.D("UI.DashboardPage", "Change event clicked.");
        if (CancelEventChangeAsync(EventClickType.ChangeEvent))
        {
            return;
        }
        ChangeEventWindow changeEventWindow = ChangeEventWindow.NewWindow(this, database!);
        AddWindow(changeEventWindow);
        changeEventWindow.ShowDialog(this);
    }

    private bool CancelEventChangeAsync(EventClickType clickType)
    {
        Log.D("UI.DashboardPage", "Checking if we need to cancel the change.");
        if (!BackgroundProcessesRunning()) return false;
        DialogBox.AsyncShow(
            "There are processes running in the background. Do you wish to stop these and continue?",
            "Yes",
            "No",
            () =>
            {
                StopBackgroundProcesses();
                switch (clickType)
                {
                    case EventClickType.NewEvent:
                        NewEventWindow newEventWindow = NewEventWindow.NewWindow(this, database!);
                        AddWindow(newEventWindow);
                        newEventWindow.ShowDialog(this);
                        break;
                    case EventClickType.ChangeEvent:
                        ChangeEventWindow changeEventWindow = ChangeEventWindow.NewWindow(this, database!);
                        AddWindow(changeEventWindow);
                        changeEventWindow.ShowDialog(this);
                        break;
                    case EventClickType.ImportEvent:
                    case EventClickType.DeleteEvent:
                    default:
                        break;
                }
            }
        );
        return true;
    }

    private enum EventClickType
    {
        NewEvent,
        ImportEvent,
        ChangeEvent,
        DeleteEvent,
    }

    public void UpdateTheme(string theme)
    {
        Application.Current?.RequestedThemeVariant = theme switch
            {
                Settings.THEME_SYSTEM => Utils.GetSystemTheme() == 1 ? ThemeVariant.Dark : ThemeVariant.Light,
                Settings.THEME_DARK => ThemeVariant.Dark,
                _ => ThemeVariant.Light,
            };
    }

    private void Window_Closing(object sender, WindowClosingEventArgs e)
    {
        if (database == null)
        {
            return;
        }
        if (database.GetAppSetting(Settings.EXIT_NO_PROMPT)!.Value == Settings.SETTING_FALSE &&
            (BackgroundProcessesRunning()))
        {
            bool allowClose = false;
            DialogBox.AsyncShow(
                "Are you sure you wish to exit?",
                "Yes",
                "No",
                () =>
                {
                    allowClose = true;
                }
                );
            if (!allowClose)
            {
                e.Cancel = true;
                return;
            }
        }
        Log.D("UI.MinWindow", "Window is closing!");
        try
        {
            StopTimingController();
        }
        catch
        {
            Log.D("UI.MinWindow", "Something went wrong stopping the timing controller... Oh well!");
        }
        foreach (Window w in openWindows)
        {
            try
            {
                w.Close();
            }
            catch
            {
                Log.D("UI.MinWindow", "Something went wrong closing a window... Oh well!");
            }
        }
        page?.Closing();
    }

    protected override void SetMaximizeIcon()
    {
        MaximizeIcon?.IsVisible = WindowState == WindowState.Normal;
        UnMaximizeIcon?.IsVisible = WindowState == WindowState.Maximized;        
    }

    protected override void Maximize()
    {
        WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
    }

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }

    public bool StopTimingController()
    {
        try
        {
            Log.D("UI.MinWindow", "Stopping Timing Controller.");
            timingController?.Shutdown();
        }
        catch
        {
            return false;
        }
        return true;
    }

    public void WindowFinalize()
    {
        Event? theEvent = database?.GetCurrentEvent();
        if (theEvent != null)
        {
            EventName.Text = theEvent.Name;
            EventDate.Text = theEvent.Date;
        }
        page?.UpdateView();
    }

    public void AddWindow(Window w)
    {
        openWindows.Add(w);
    }

    public async void ConnectTimingSystem(TimingSystem system)
    {
        try
        {
            await Task.Run(() =>
            {
                timingController?.ConnectTimingSystem(system);
            });
            UpdateTiming();
            await Task.Run(() =>
            {
                if (TimingController.IsRunning()) return;
                timingControllerThread = new Thread(timingController!.Run);
                timingControllerThread.Start();
            });
        }
        catch (Exception)
        {
            Log.D("UI.MinWindow", "Error thrown connecting to timing system.");
        }
    }

    public async void DisconnectTimingSystem(TimingSystem system)
    {
        try
        {
            await Task.Run(() =>
            {
                timingController!.DisconnectTimingSystem(system);
            });
            UpdateTiming();
        }
        catch (Exception)
        {
            Log.D("UI.MinWindow", "Error thrown disconnecting from timing system.");
        }
    }

    public void ShutdownTimingController()
    {
        timingController?.Shutdown();
    }

    public List<TimingSystem> GetConnectedSystems()
    {
        List<TimingSystem> connected = timingController!.GetConnectedSystems();
        List<TimingSystem> saved = database!.GetTimingSystems();
        saved.RemoveAll(connected.Contains);
        saved.InsertRange(0, connected);
        return saved;
    }

    public void TimingSystemDisconnected(TimingSystem system)
    {
        Application.Current!.Dispatcher.Invoke(delegate
        {
            if (!system.SystemInterface!.WasShutdown())
            {
                DialogBox.AsyncShow(
                    $"Reader at {system.LocationName} has unexpectedly disconnected. IP Address was {system.IpAddress}.");
            }
            system.Status = SYSTEM_STATUS.DISCONNECTED;
            UpdateTiming();
        });
    }

    public void NotifyTimingWorker() { }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Check for current theme color and apply it.
        AppSetting? themeColor = database!.GetAppSetting(Settings.CURRENT_THEME);
        if (themeColor != null)
        {
            UpdateTheme(themeColor.Value);
        }
    }

    public void Exit()
    {
        Close();
    }

    public bool BackgroundProcessesRunning()
    {
        return TimingController.IsRunning();
    }

    public void StopBackgroundProcesses()
    {
        try
        {
            StopTimingController();
        }
        catch
        {
            Log.D("UI.MinWindow", "Error thrown stopping the timing controller.");
        }
    }

    public void ShowNotificationDialog(string readerName, string address, RemoteNotification notification)
    {
        Log.D("UI.MinWindow", $"Show Notification Dialog called. When '{notification.When}' - Type '{notification.Type}' - ReaderName '{readerName}' - Address '{address}'");
        ReaderMessage msg = new()
        {
            Message = notification,
            SystemName = readerName,
            Address = address,
            // MinWindow can just set it to high always because it lacks an info badge.
            Severity = ReaderMessage.SeverityLevel.High,
        };
        AddReaderMessage(msg);
        UpdateTimingNonBlocking();
    }

    public void UpdateTimingNonBlocking()
    {
        Log.D("UI.MinWindow", "UpdateTimingNonBlocking called.");
        List<ReaderMessage> toShow = [];
        List<ReaderMessage> readerMsgs = GetReaderMessages();
        foreach (ReaderMessage message in readerMsgs.Where(message => message is { Severity: ReaderMessage.SeverityLevel.High, Notified: false }))
        {
            toShow.Add(message);
            message.Notified = true;
            UpdateReaderMessage(message);
        }
        Thread newThread = new(() =>
        {
            // show any dialog boxes that need to be shown due to importance
            foreach (ReaderMessage message in toShow)
            {
                Application.Current!.Dispatcher.Invoke(delegate
                {
                    DialogBox.AsyncShow(message.DialogBoxString);
                });
            }
            // Let the announcer window know that it has new information.
            Application.Current!.Dispatcher.Invoke(delegate
            {
                page?.UpdateView();
            });
        });
        newThread.Start();
    }

    public void SwitchPage(IMainPage iPage) { }

    public void NetworkUpdateResults() { }

    public void NetworkClearResults() { }

    public void StartHttpServer() { }

    public void StopHttpServer() { }

    public bool HttpServerActive() { return false; }

    public void UpdateStatus() { }

    public void UpdateTimingFromController()
    {
        Application.Current!.Dispatcher.Invoke(delegate
        {
            if (page is null) return;
            page.UpdateView();
            page.NewMessage();
        });
    }

    public void UpdateTiming()
    {
        Application.Current!.Dispatcher.Invoke(delegate
        {
            if (page is null) return;
            page.UpdateView();
            page.NewMessage();
        });
    }

    public void UpdateAnnouncerWindow() { }

    public void UpdateRegistrationDistances() { }

    public void UpdateParticipantsFromRegistration() { }

    public bool InDidNotStartMode() { return false; }

    public bool StartDidNotStartMode() { return false; }

    public bool StopDidNotStartMode() { return false; }

    public void NotifyAlarm(string bib, string chip) { }

    public bool AnnouncerConnected() { return false; }

    public void AnnouncerClosing() { }

    public bool AnnouncerOpen() { return false; }

    public void StopAnnouncer() { }

    public void StartApiController() { }

    public bool StopApiController() { return false; }

    public bool IsApiControllerRunning() { return false; }

    public int ApiErrors() { return 0; }

    public void StartRemote() { }

    public void StopRemote() { }

    public RemoteReadsController.RemoteStatus IsRemoteRunning() { return RemoteReadsController.RemoteStatus.UNKNOWN; }

    public int RemoteErrors() { return 0; }

    public bool StartRegistration() { return false; }

    public bool StopRegistration() { return false; }

    public bool IsRegistrationRunning() { return false; }
}
