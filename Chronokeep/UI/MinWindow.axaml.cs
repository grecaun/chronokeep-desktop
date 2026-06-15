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
using System.Threading;
using System.Threading.Tasks;
using static Chronokeep.Helpers.Globals;

namespace Chronokeep.UI;

public partial class MinWindow : Window, IMainWindow
{
    private readonly MemStore.MemStore? database;
    private readonly MinTimingPage? page;

    // Timing objects.
    private Thread? TimingControllerThread;
    private readonly TimingController? TimingController;

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
        // Check that no other instance of this program are running.
        if (!OneWindow.WaitOne(TimeSpan.Zero, true))
        {
            DialogBox.Show("Chronokeep is already running.");
            this.Close();
            return;
        }
        OneWindow.ReleaseMutex();

        string dirPath = App.IsWindows ?
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), Constants.Settings.PROGRAM_DIR)
            : Path.Combine(Directory.GetCurrentDirectory(), "data");
        string path = Path.Combine(dirPath, MainWindow.DatabaseFileName);
        Log.D("UI.MainWindow", "Looking for database file.");
        if (!Directory.Exists(dirPath))
        {
            Log.D("UI.MainWindow", "Creating directory.");
            Directory.CreateDirectory(dirPath);
        }
        if (!File.Exists(path))
        {
            Log.D("UI.MainWindow", "Creating database file.");
            SQLiteConnection.CreateFile(path);
        }
        database = MemStore.MemStore.GetMemStore(new SQLiteInterface(path));
        try
        {
            database.Initialize();
        }
        catch (InvalidDatabaseVersion db)
        {
            DialogBox.Show(string.Format("Database version greater than the max known by this client. Please update the client. Database version {0}. Max version for this client {1}", db.FoundVersion, db.MaxVersion));
            this.Close();
            return;
        }
        Constants.Settings.SetupSettings(database);

        TimingController = new TimingController(this, database);

        page = new MinTimingPage(this, database);
        TheFrame.Content = page;
    }


    private void NewEvent_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.DashboardPage", "New event clicked.");
        if (CancelEventChangeAsync(EventClickType.NewEvent))
        {
            return;
        }
        NewEventWindow newEventWindow = NewEventWindow.NewWindow(this, database!);
        if (newEventWindow != null)
        {
            this.AddWindow(newEventWindow);
            newEventWindow.ShowDialog(this);
        }
    }

    private void ChangeEvent_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.DashboardPage", "Change event clicked.");
        if (CancelEventChangeAsync(EventClickType.ChangeEvent))
        {
            return;
        }
        ChangeEventWindow changeEventWindow = ChangeEventWindow.NewWindow(this, database!);
        if (changeEventWindow != null)
        {
            this.AddWindow(changeEventWindow);
            changeEventWindow.ShowDialog(this);
        }
    }

    private bool CancelEventChangeAsync(EventClickType clickType)
    {
        Log.D("UI.DashboardPage", "Checking if we need to cancel the change.");
        if (BackgroundProcessesRunning())
        {
            DialogBox.Show(
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
                            if (newEventWindow != null)
                            {
                                this.AddWindow(newEventWindow);
                                newEventWindow.ShowDialog(this);
                            }
                            break;
                        case EventClickType.ChangeEvent:
                            ChangeEventWindow changeEventWindow = ChangeEventWindow.NewWindow(this, database!);
                            if (changeEventWindow != null)
                            {
                                this.AddWindow(changeEventWindow);
                                changeEventWindow.ShowDialog(this);
                            }
                            break;
                    }
                }
                );
            return true;
        }
        return false;
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
                Constants.Settings.THEME_SYSTEM => Utils.GetSystemTheme() == 1 ? ThemeVariant.Dark : ThemeVariant.Light,
                Constants.Settings.THEME_DARK => ThemeVariant.Dark,
                _ => ThemeVariant.Light,
            };
    }

    private void Window_Closing(object sender, WindowClosingEventArgs e)
    {
        if (database == null)
        {
            return;
        }
        if (database.GetAppSetting(Constants.Settings.EXIT_NO_PROMPT)!.Value == Constants.Settings.SETTING_FALSE &&
            (BackgroundProcessesRunning()))
        {
            bool AllowClose = false;
            DialogBox.Show(
                "Are you sure you wish to exit?",
                "Yes",
                "No",
                () =>
                {
                    AllowClose = true;
                }
                );
            if (!AllowClose)
            {
                e.Cancel = true;
                return;
            }
        }
        Log.D("UI.MainWindow", "Window is closing!");
        try
        {
            StopTimingController();
        }
        catch { }
        foreach (Window w in openWindows)
        {
            try
            {
                w.Close();
            }
            catch
            {
                Log.D("UI.MainWindow", "Oh well!");
            }
        }
        page?.Closing();
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

    public bool StopTimingController()
    {
        try
        {
            Log.D("UI.MainWindow", "Stopping Timing Controller.");
            TimingController?.Shutdown();
        }
        catch
        {
            return false;
        }
        return true;
    }

    public void WindowFinalize(Window? w)
    {
        page?.UpdateView();
    }

    public void AddWindow(Window w)
    {
        openWindows.Add(w);
    }

    public async void ConnectTimingSystem(TimingSystem system)
    {
        await Task.Run(() =>
        {
            TimingController?.ConnectTimingSystem(system);
        });
        UpdateTiming();
        await Task.Run(() =>
        {
            if (!TimingController.IsRunning())
            {
                TimingControllerThread = new Thread(new ThreadStart(TimingController!.Run));
                TimingControllerThread.Start();
            }
        });
    }

    public async void DisconnectTimingSystem(TimingSystem system)
    {
        await Task.Run(() =>
        {
            TimingController!.DisconnectTimingSystem(system);
        });
        UpdateTiming();
    }

    public void ShutdownTimingController()
    {
        TimingController?.Shutdown();
    }

    public List<TimingSystem> GetConnectedSystems()
    {
        List<TimingSystem> connected = TimingController!.GetConnectedSystems();
        List<TimingSystem> saved = database!.GetTimingSystems();
        saved.RemoveAll(x => connected.Contains(x));
        saved.InsertRange(0, connected);
        return saved;
    }

    public void TimingSystemDisconnected(TimingSystem system)
    {
        Application.Current!.Dispatcher.Invoke(new Action(delegate ()
        {
            if (!system.SystemInterface!.WasShutdown())
            {
                DialogBox.Show(string.Format("Reader at {0} has unexpectedly disconnected. IP Address was {1}.", system.LocationName, system.IPAddress));
            }
            system.Status = SYSTEM_STATUS.DISCONNECTED;
            UpdateTiming();
        }));
    }

    public void NotifyTimingWorker() { }

    private void Window_Loaded(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Check for current theme color and apply it.
        AppSetting? themeColor = database!.GetAppSetting(Constants.Settings.CURRENT_THEME);
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
        catch { }
    }

    public void ShowNotificationDialog(string ReaderName, string Address, RemoteNotification notification)
    {
        Log.D("UI.MainWindow", $"Show Notification Dialog called. When '{notification.When}' - Type '{notification.Type}' - ReaderName '{ReaderName}' - Address '{Address}'");
        ReaderMessage msg = new()
        {
            Message = notification,
            SystemName = ReaderName,
            Address = Address,
            // MinWindow can just set it to high always because it lacks an info badge.
            Severity = ReaderMessage.SeverityLevel.High,
        };
        AddReaderMessage(msg);
        UpdateTimingNonBlocking();
    }

    public void UpdateTimingNonBlocking()
    {
        Log.D("UI.MainWindow", "UpdateTimingNonBlocking called.");
        List<ReaderMessage> toShow = [];
        List<ReaderMessage> readerMsgs = GetReaderMessages();
        foreach (ReaderMessage message in readerMsgs)
        {
            if (message.Severity == ReaderMessage.SeverityLevel.High && !message.Notified)
            {
                toShow.Add(message);
                message.Notified = true;
                UpdateReaderMessage(message);
            }
        }
        Thread newThread = new(new ThreadStart(() =>
        {
            // show any dialogboxes that need to be shown due to importance
            foreach (ReaderMessage message in toShow)
            {
                Application.Current!.Dispatcher.Invoke(new Action(delegate ()
                {
                    DialogBox.Show(message.DialogBoxString);
                }));
            }
            // Let the announcer window know that it has new information.
            Application.Current!.Dispatcher.Invoke(new Action(delegate ()
            {
                page?.UpdateView();
            }));
        }));
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
        Application.Current!.Dispatcher.Invoke(new Action(delegate ()
        {
            if (page is MinTimingPage tPage)
            {
                tPage.UpdateView();
                tPage.NewMessage();
            }
        }));
    }

    public void UpdateTiming()
    {
        Application.Current!.Dispatcher.Invoke(new Action(delegate ()
        {
            if (page is MinTimingPage tPage)
            {
                tPage.UpdateView();
                tPage.NewMessage();
            }
        }));
    }

    public void UpdateAnnouncerWindow() { }

    public void UpdateRegistrationDistances() { }

    public void UpdateParticipantsFromRegistration() { }

    public bool InDidNotStartMode() { return false; }

    public bool StartDidNotStartMode() { return false; }

    public bool StopDidNotStartMode() { return false; }

    public void NotifyAlarm(string Bib, string Chip) { }

    public bool AnnouncerConnected() { return false; }

    public void AnnouncerClosing() { }

    public bool AnnouncerOpen() { return false; }

    public void StopAnnouncer() { }

    public void StartAPIController() { }

    public bool StopAPIController() { return false; }

    public bool IsAPIControllerRunning() { return false; }

    public int APIErrors() { return 0; }

    public void StartRemote() { }

    public void StopRemote() { }

    public RemoteReadsController.RemoteStatus IsRemoteRunning() { return RemoteReadsController.RemoteStatus.UNKNOWN; }

    public int RemoteErrors() { return 0; }

    public bool StartRegistration() { return false; }

    public bool StopRegistration() { return false; }

    public bool IsRegistrationRunning() { return false; }
}