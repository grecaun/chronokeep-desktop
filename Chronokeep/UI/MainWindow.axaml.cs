using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Network;
using Chronokeep.Network.Registration;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronokeepPortal;
using Chronokeep.Objects.ChronokeepRemote;
using Chronokeep.Timing;
using Chronokeep.Timing.Announcer;
using Chronokeep.Timing.API;
using Chronokeep.Timing.Remote;
using Chronokeep.UI.Announcer;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.Util;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static Chronokeep.Helpers.Globals;

namespace Chronokeep.UI
{
    public partial class MainWindow : Window, IMainWindow
    {
        internal static Window? mWindow;
        internal IMainPage? CurrentPage;
        internal static bool ForceClose = false;

        private readonly MemStore.MemStore? database;
        internal static string DatabaseFileName = "Chronokeep.sqlite";

        // Network objects
        private HttpServer? httpServer = null;
        private readonly int httpServerPort = 6933;

        // Zero Conf/Registration objects.
        private Thread? ZConfThread = null;
        private ZeroConf? ZConfServer = null;
        private Thread? RegistrationThread = null;
        private RegistrationWorker? RegistrationWorker = null;

        // Timing objects.
        private Thread? TimingControllerThread = null;
        private TimingController? TimingController = null;
        private Thread? TimingWorkerThread = null;
        private TimingWorker? TimingWorker = null;

        // API objects.
        private Thread? APIControllerThread = null;
        private APIController? APIController = null;

        // Remote Reads objects
        private Thread? RemoteThread = null;
        private RemoteReadsController? RemoteController = null;

        // Announcer objects
        private AnnouncerWindow? announcerWindow = null;

        private readonly List<Window> openWindows = [];

        // Setting to allow the user to enter a mode where we can record DNS chips.
        private bool didNotStartMode = false;
        private readonly Lock dnsLock = new();

        // Setup a timer for updating the view
        private readonly DispatcherTimer TimingUpdater = new();

        // Set up a mutex that will be unique for this program to ensure we only ever have a single instance of it running.
        // Allow for a debug version and non-debug version to run at the same time.
#if DEBUG
        private static readonly Mutex OneWindow = new(true, "{48ED48DE-6E1B-4F3B-8C5C-D0BAB5295366}-chronokeep-debug");
#else
        private static readonly Mutex OneWindow = new(true, "{48ED48DE-6E1B-4F3B-8C5C-D0BAB5295366}-chronokeep");
#endif

        public MainWindow()
        {
            InitializeComponent();
            mWindow = this;

            // Check that no other instance of this program are running.
            if (!OneWindow.WaitOne(TimeSpan.Zero, true))
            {
                DialogBox.Show("Chronokeep is already running.");
                Close();
                return;
            }
            OneWindow.ReleaseMutex();

            string dirPath = App.IsWindows ?
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), Constants.Settings.PROGRAM_DIR)
                : Path.Combine(Directory.GetCurrentDirectory(), "data");
#if DEBUG
            DatabaseFileName = "Chronokeep_test.sqlite";
#endif
            string path = Path.Combine(dirPath, DatabaseFileName);
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
                Close();
                return;
            }
            Constants.Settings.SetupSettings(database);

            // Ensure Global values are set up.
            SetupValues(database);

            // Setup AgeGroup static variables
            Event? theEvent = database.GetCurrentEvent();

            CurrentPage = new DashboardPage(this, database);
            CurrentContent.Content = CurrentPage;

            UpdateStatus();

            // Check for updates.
            if (database.GetAppSetting(Constants.Settings.CHECK_UPDATES)!.Value == Constants.Settings.SETTING_TRUE)
            {
                Updates.Check.Do(this);
            }

            DataContext = this;

            // Set timing update to every two tenths of a second.
            TimingUpdater.Tick += UpdateTimingTick;
            TimingUpdater.Interval = new TimeSpan(0, 0, 0, 0, 200);
            TimingUpdater.Start();

            // Set the global upload interval.
            if (!int.TryParse(database.GetAppSetting(Constants.Settings.UPLOAD_INTERVAL)!.Value, out Globals.UploadInterval))
            {
                DialogBox.Show("Something went wrong trying to update the upload interval.");
            }

            // Set the global download interval.
            if (!int.TryParse(database.GetAppSetting(Constants.Settings.DOWNLOAD_INTERVAL)!.Value, out Globals.DownloadInterval))
            {
                DialogBox.Show("Something went wrong trying to update the download interval.");
            }

            // Pull alarms from the database.
            if (theEvent != null && theEvent.Identifier != -1)
            {
                Alarm.AddAlarms(database.GetAlarms(theEvent.Identifier));
            }

            // Setup global twilio account credentials.
            Constants.GlobalVars.SetTwilioCredentials(database);
        }

        public void UpdateTheme(string theme)
        {
            Application.Current?.RequestedThemeVariant = theme switch
            {
                Constants.Settings.THEME_SYSTEM => Utils.GetSystemTheme() == 0 ? ThemeVariant.Dark : ThemeVariant.Light,
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
            if (!ForceClose && database.GetAppSetting(Constants.Settings.EXIT_NO_PROMPT)!.Value == Constants.Settings.SETTING_FALSE &&
                (BackgroundProcessesRunning()))
            {
                DialogBox.Show(
                    "Are you sure you wish to exit?",
                    "Yes",
                    "No",
                    () =>
                        {
                            ForceClose = true;
                            mWindow?.Close();
                        }
                    );
                e.Cancel = true;
                return;
            }
            Log.D("UI.MainWindow", "Window is closing!");
            try
            {
                StopTimingController();
            }
            catch { }
            try
            {
                StopTimingWorker();
            }
            catch { }
            try
            {
                StopAPIController();
            }
            catch { }
            try
            {
                StopAnnouncer();
            }
            catch { }
            try
            {
                StopRegistration();
            }
            catch { }
            try
            {
                httpServer?.Stop();
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
            try
            {
                CurrentPage?.Closing();
            }
            catch { }
            try
            {
                TimingUpdater.Stop();
            }
            catch { }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TimingController = new TimingController(this, database!);
            TimingWorker = TimingWorker.NewWorker(this, database!);
            TimingWorkerThread = new Thread(new ThreadStart(TimingWorker.Run));
            TimingWorkerThread.Start();
            TimingWorker.Notify();
            // Check for current theme color and apply it.
            AppSetting? themeColor = database!.GetAppSetting(Constants.Settings.CURRENT_THEME);
            if (themeColor != null)
            {
                UpdateTheme(themeColor.Value);
            }
            // Check for hardware changes.
            Log.D("UI.MainWindow", "Starting hardware checker.");
            HardwareChecker hwCheck = new(database);
            Thread hardwareThread = new(new ThreadStart(hwCheck.Run));
            hardwareThread.Start();
            UpdateTimingBadge();
            // Check last known program version
            Log.D("UI.MainWindow", "Starting changelog version checker.");
            string gitVersion = "";
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Chronokeep." + "version.txt")!)
            {
                using StreamReader reader = new(stream);
                gitVersion = reader.ReadToEnd();
            }
            if (gitVersion.Contains('-'))
            {
                gitVersion = gitVersion.Split('-')[0];
            }
            Log.D("UI.MainWindow", "Version.txt read.");
            AppSetting programVers = database.GetAppSetting(Constants.Settings.PROGRAM_VERSION)!;
            AppSetting showChangelog = database.GetAppSetting(Constants.Settings.AUTO_SHOW_CHANGELOG)!;
            if (programVers == null && showChangelog != null && showChangelog.Value == Constants.Settings.SETTING_TRUE)
            {
                Log.D("UI.MainWindow", "AppSetting not set.");
                // Program version was not set, thus this is an upgraded program.
                ChangeLogWindow clw = ChangeLogWindow.NewWindow(this, database);
                clw.Show();
            }
            else
            {
                Log.D("UI.MainWindow", "Splitting defined values, parsing them, then checking if newer version.");
                string[] gitSplit = gitVersion.Replace("v", "").Split('.');
                string[] dbSplit = programVers!.Value.Replace("v", "").Split('.');
                if (dbSplit.Length != 3 || gitSplit.Length != 3)
                {
                    DialogBox.Show($"Expected 3 values when checking the program version. DB ${programVers.Value} - P ${gitVersion}");
                }
                else if (int.TryParse(gitSplit[0], out int newMajor) &&
                        int.TryParse(gitSplit[1], out int newMinor) &&
                        int.TryParse(gitSplit[2], out int newPatch) &&
                        int.TryParse(dbSplit[0], out int oldMajor) &&
                        int.TryParse(dbSplit[1], out int oldMinor) &&
                        int.TryParse(dbSplit[2], out int oldPatch))
                {
                    if (newMajor > oldMajor ||                              // The new Major version is greater than the old Major version (1.9.0 -> 2.0.0)
                        (newMajor == oldMajor && (newMinor > oldMinor       // The Major versions match but the new Minor version is greater than the old Minor version (1.9.0 -> 1.10.0)
                        || (newMinor == oldMinor && newPatch > oldPatch)))) // The Major and Minor versions match but the new Patch version is greater than the old Patch version (1.10.0 -> 1.10.1)
                    {
                        if (showChangelog != null && showChangelog.Value == Constants.Settings.SETTING_TRUE)
                        {
                            ChangeLogWindow clw = ChangeLogWindow.NewWindow(this, database);
                            clw.Show();
                        }
                    }
                }
                else
                {
                    DialogBox.Show($"Invalid version values found. DB${dbSplit.Length} - P${gitSplit.Length}");
                }
            }
            database.SetAppSetting(Constants.Settings.PROGRAM_VERSION, gitVersion);
        }

        public void SwitchPage(IMainPage iPage)
        {
            CurrentPage?.Closing();
            CurrentPage = iPage;
            CurrentContent.Content = CurrentPage;
        }

        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            Log.D("UI.MainWindow", "Dashboard button clicked.");
            if (CurrentPage is DashboardPage)
            {
                Log.D("UI.MainWindow", "Dashboard page already displayed.");
                return;
            }
            UncheckAll();
            DashboardButton.IsChecked = true;
            SwitchPage(new DashboardPage(this, database!));
        }

        private void TimingButton_Click(object sender, RoutedEventArgs e)
        {
            Log.D("UI.MainWindow", "Timing button clicked.");
            if (CurrentPage is TimingPage page)
            {
                Log.D("UI.MainWindow", "Timing page already displayed.");
                page.LoadMainDisplay();
                return;
            }
            UncheckAll();
            TimingButton.IsChecked = true;
            SwitchPage(new TimingPage(this, database!));
        }

        private void ParticipantsButton_Click(object sender, RoutedEventArgs e)
        {
            Log.D("UI.MainWindow", "Participants button clicked.");
            if (CurrentPage is ParticipantsPage)
            {
                Log.D("UI.MainWindow", "Participants page already displayed.");
                return;
            }
            UncheckAll();
            ParticipantsButton.IsChecked = true;
            SwitchPage(new ParticipantsPage(this, database!));
        }

        private void ChipsButton_Click(object sender, RoutedEventArgs e)
        {
            Log.D("UI.MainWindow", "Chips button clicked.");
            if (CurrentPage is ChipAssignmentPage)
            {
                Log.D("UI.MainWindow", "Chips page already displayed.");
                return;
            }
            UncheckAll();
            ChipsButton.IsChecked = true;
            SwitchPage(new ChipAssignmentPage(this, database!));
        }

        private void LocationsButton_Click(object sender, RoutedEventArgs e)
        {
            Log.D("UI.MainWindow", "Locations button clicked.");
            if (CurrentPage is LocationsPage)
            {
                Log.D("UI.MainWindow", "Locations page already displayed.");
                return;
            }
            UncheckAll();
            LocationsButton.IsChecked = true;
            SwitchPage(new LocationsPage(this, database!));
        }
        private void DistancesButton_Click(object sender, RoutedEventArgs e)
        {
            Log.D("UI.MainWindow", "Distances button clicked.");
            if (CurrentPage is DistancesPage)
            {
                Log.D("UI.MainWindow", "Distances page already displayed.");
                return;
            }
            UncheckAll();
            DistancesButton.IsChecked = true;
            SwitchPage(new DistancesPage(this, database!));
        }

        private void SegmentsButton_Click(object sender, RoutedEventArgs e)
        {
            Log.D("UI.MainWindow", "Segments button clicked.");
            if (CurrentPage is SegmentsPage)
            {
                Log.D("UI.MainWindow", "Segments page already displayed.");
                return;
            }
            UncheckAll();
            SegmentsButton.IsChecked = true;
            SwitchPage(new SegmentsPage(this, database!));
        }

        private void AgeGroupsButton_Click(object sender, RoutedEventArgs e)
        {
            Log.D("UI.MainWindow", "Age Groups button clicked.");
            if (CurrentPage is AgeGroupsPage)
            {
                Log.D("UI.MainWindow", "Age groups page already displayed.");
                return;
            }
            UncheckAll();
            AgeGroupsButton.IsChecked = true;
            SwitchPage(new AgeGroupsPage(this, database!));
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Log.D("UI.MainWindow", "Settings button clicked.");
            if (CurrentPage is SettingsPage)
            {
                Log.D("UI.MainWindow", "Settings page already displayed.");
                return;
            }
            UncheckAll();
            SettingsButton.IsChecked = true;
            SwitchPage(new SettingsPage(this, database!));
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            Log.D("UI.MainWindow", "About button clicked.");
            if (CurrentPage is AboutPage)
            {
                Log.D("UI.MainWindow", "About page already displayed.");
                return;
            }
            UncheckAll();
            AboutButton.IsChecked = true;
            SwitchPage(new AboutPage(this, database!));
        }

        private void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            ParentSplitView.IsPaneOpen = !ParentSplitView.IsPaneOpen;
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

        private void UncheckAll()
        {
            DashboardButton.IsChecked = false;
            TimingButton.IsChecked = false;
            ParticipantsButton.IsChecked = false;
            DistancesButton.IsChecked = false;
            LocationsButton.IsChecked = false;
            ChipsButton.IsChecked = false;
            AgeGroupsButton.IsChecked = false;
            SegmentsButton.IsChecked = false;
            SettingsButton.IsChecked = false;
            AboutButton.IsChecked = false;
        }

        public bool IsRegistrationRunning()
        {
            return (RegistrationWorker != null && RegistrationWorker.IsRunning()) && (ZConfServer != null && ZeroConf.IsRunning());
        }

        public bool StopRegistration()
        {
            bool output = true;
            try
            {
                Log.D("UI.MainWindow", "Stopping zero conf.");
                ZConfServer?.Stop();
            }
            catch
            {
                output = false;
            }
            try
            {
                Log.D("UI.MainWindow", "Stopping registration.");
                RegistrationWorker?.Stop();
            }
            catch
            {
                output = false;
            }
            return output;
        }

        public bool StartRegistration()
        {
            bool output = true;
            try
            {
                Log.D("UI.MainWindow", "Starting zero conf.");
                AppSetting? zconfName = database!.GetAppSetting(Constants.Settings.SERVER_NAME);
                ZConfServer = new ZeroConf(zconfName != null && zconfName.Value != null ? zconfName.Value : null);
                ZConfThread = new Thread(new ThreadStart(ZConfServer.Run));
                ZConfThread.Start();
            }
            catch
            {
                output = false;
            }
            try
            {
                Log.D("UI.MainWindow", "Starting registration.");
                RegistrationWorker = new RegistrationWorker(database!, this);
                RegistrationThread = new Thread(new ThreadStart(RegistrationWorker.Run));
                RegistrationThread.Start();
            }
            catch
            {
                output = false;
            }
            return output;
        }

        public void UpdateRegistrationDistances()
        {
            RegistrationWorker?.UpdateDistances();
        }

        private static bool StopTimingWorker()
        {
            try
            {
                Log.D("UI.MainWindow", "Stopping Timing Worker.");
                TimingWorker.Shutdown();
                TimingWorker.Notify();
            }
            catch
            {
                return false;
            }
            return true;
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

        public bool StopAPIController()
        {
            try
            {
                Log.D("UI.MainWindow", "Stopping API Controller");
                if (APIController != null)
                {
                    APIController.Shutdown();
                }
                APIController = null;
            }
            catch
            {
                return false;
            }
            CurrentPage?.UpdateView();
            return true;
        }

        public async void StartAPIController()
        {
            await Task.Run(() =>
            {
                if (!APIController.IsRunning())
                {
                    APIController = new APIController(this, database!);
                    APIControllerThread = new Thread(new ThreadStart(APIController.Run));
                    APIControllerThread.Start();
                }
            });
        }

        public bool IsAPIControllerRunning()
        {
            return APIController != null && APIController.IsRunning();
        }

        public int APIErrors()
        {
            return APIController != null ? APIController.Errors : 0;
        }

        public void UpdateParticipantsFromRegistration()
        {
            Application.Current!.Dispatcher.Invoke(new Action(delegate ()
            {
                if (CurrentPage is ParticipantsPage)
                {
                    CurrentPage.UpdateView();
                }
            }));
        }

        public void UpdateTimingFromController()
        {
            TimingWorker.Notify();
            Application.Current!.Dispatcher.Invoke(new Action(delegate ()
            {
                if (CurrentPage is TimingPage timingPage)
                {
                    timingPage.UpdateView();
                    timingPage.NewMessage();
                }
                announcerWindow?.UpdateView();
            }));
        }

        public void StartRemote()
        {
            Task.Run(() =>
            {
                Log.D("UI.MainWindow", "Checking Remote Thread");
                if (RemoteReadsController.IsRunning() == RemoteReadsController.RemoteStatus.STOPPED)
                {
                    Log.D("UI.MainWindow", "Starting Remote Thread");
                    RemoteController = new RemoteReadsController(this, database!);
                    RemoteThread = new Thread(new ThreadStart(RemoteController.Run));
                    RemoteThread.Start();
                }
            });
        }

        public void StopRemote()
        {
            Task.Run(() =>
            {
                Log.D("UI.MainWindow", "Stopping Remote Controller");
                RemoteReadsController.Shutdown();
                RemoteController = null;
            });
        }

        public RemoteReadsController.RemoteStatus IsRemoteRunning()
        {
            return RemoteReadsController.IsRunning();
        }

        public int RemoteErrors()
        {
            return RemoteController != null ? RemoteController.Errors : 0;
        }

        public void UpdateAnnouncerWindow()
        {
            // Let the announcer window know that it has new information.
            Application.Current!.Dispatcher.Invoke(new Action(delegate ()
            {
                announcerWindow?.UpdateView();
            }));
        }

        public void UpdateTiming()
        {
            // Let the timing page know that it has new information.
            Application.Current!.Dispatcher.Invoke(new Action(delegate ()
            {
                if (CurrentPage is TimingPage)
                {
                    CurrentPage.UpdateView();
                }
            }));
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
                    if (CurrentPage is TimingPage)
                    {
                        CurrentPage.UpdateView();
                    }
                }));
            }));
            newThread.Start();
        }

        public void UpdateTimingTick(object? sender, EventArgs e)
        {
            if (TimingWorker.NewResultsExist())
            {
                if (CurrentPage is TimingPage timingPage)
                {
                    timingPage.UpdateSubView();
                }
                announcerWindow?.UpdateTiming();
            }
        }

        public void AddWindow(Window w)
        {
            openWindows.Add(w);
        }

        public void UpdateStatus()
        {
            Event? theEvent = database!.GetCurrentEvent();
            Alarm.ClearAlarms();
            if (theEvent == null || theEvent.Identifier == -1)
            {
                ParticipantsButton.IsEnabled = false;
                ChipsButton.IsEnabled = false;
                DistancesButton.IsEnabled = false;
                LocationsButton.IsEnabled = false;
                SegmentsButton.IsEnabled = false;
                AgeGroupsButton.IsEnabled = false;
                TimingButton.IsEnabled = false;
                AnnouncerButton.IsEnabled = false;

                ParticipantsButton.Opacity = 0.2;
                ChipsButton.Opacity = 0.2;
                DistancesButton.Opacity = 0.2;
                LocationsButton.Opacity = 0.2;
                SegmentsButton.Opacity = 0.2;
                AgeGroupsButton.Opacity = 0.2;
                TimingButton.Opacity = 0.2;
                AnnouncerButton.Opacity = 0.2;
            }
            else
            {
                ParticipantsButton.IsEnabled = true;
                ChipsButton.IsEnabled = true;
                DistancesButton.IsEnabled = true;
                LocationsButton.IsEnabled = true;
                SegmentsButton.IsEnabled = true;
                AgeGroupsButton.IsEnabled = true;
                TimingButton.IsEnabled = true;
                AnnouncerButton.IsEnabled = true;

                ParticipantsButton.Opacity = 1.0;
                ChipsButton.Opacity = 1.0;
                DistancesButton.Opacity = 1.0;
                LocationsButton.Opacity = 1.0;
                SegmentsButton.Opacity = 1.0;
                AgeGroupsButton.Opacity = 1.0;
                TimingButton.Opacity = 1.0;
                AnnouncerButton.Opacity = 1.0;

                // Pull alarms from the database.
                Alarm.AddAlarms(database.GetAlarms(theEvent.Identifier));
            }
            DashboardButton.IsChecked = CurrentPage is DashboardPage;
            TimingButton.IsChecked = CurrentPage is TimingPage;
            AnnouncerButton.IsChecked = announcerWindow != null;
            ParticipantsButton.IsChecked = CurrentPage is ParticipantsPage;
            ChipsButton.IsChecked = CurrentPage is ChipAssignmentPage;
            LocationsButton.IsChecked = CurrentPage is LocationsPage;
            DistancesButton.IsChecked = CurrentPage is DistancesPage;
            SegmentsButton.IsChecked = CurrentPage is SegmentsPage;
            AgeGroupsButton.IsChecked = CurrentPage is AgeGroupsPage;
            SettingsButton.IsChecked = CurrentPage is SettingsPage;
            AboutButton.IsChecked = CurrentPage is AboutPage;
            UpdateTimingBadge();
        }

        public void UpdateTimingBadge()
        {
            if (CurrentPage is not TimingPage)
            {
                List<ReaderMessage> messages = GetReaderMessages();
                messages.RemoveAll(x => x.Notified);
                if (messages.Count > 0)
                { }
                else
                { }
            }
            else
            { }
        }

        public async void ConnectTimingSystem(TimingSystem system)
        {
            await Task.Run(() =>
            {
                TimingController!.ConnectTimingSystem(system);
            });
            UpdateTiming();
            announcerWindow?.UpdateView();
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
            announcerWindow?.UpdateView();
        }

        public void ShutdownTimingController()
        {
            TimingController!.Shutdown();
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
            try
            {
                Application.Current!.Dispatcher.Invoke(new Action(delegate ()
                {
                    if (system.SystemInterface != null)
                    {
                        if (!system.SystemInterface.WasShutdown())
                        {
                            DialogBox.Show(string.Format("Reader at {0} has unexpectedly disconnected. IP Address was {1}.", system.LocationName, system.IPAddress));
                        }
                    }
                    system.Status = SYSTEM_STATUS.DISCONNECTED;
                    UpdateTiming();
                    announcerWindow?.UpdateView();
                }));
            }
            catch (TaskCanceledException) { }
            catch (Exception e)
            {
                Log.E("UI.MainWindow", string.Format("Exception occurred trying to update disconnected timing system. {0}", e));
            }
        }

        public void NotifyTimingWorker()
        {
            Log.D("UI.MainWindow", "MainWindow notifying timer.");
            TimingWorker.ResetDictionaries();
            TimingWorker.Notify();
            // Let the AnnouncerWorker know there are new reads (potentially).
            AnnouncerWorker.Notify();
        }

        private void Announcer_Click(object sender, RoutedEventArgs e)
        {
            Log.D("UI.MainWindow", "Announer window button clicked.");
            Log.E("UI.MainWindow", string.Format("announcerWindow is null? {0}", announcerWindow == null));
            if (announcerWindow != null)
            {
                announcerWindow.Hide();
                announcerWindow.Show();
                UpdateStatus();
                return;
            }
            Log.E("UI.MainWindow", string.Format("beep boop"));
            announcerWindow = new AnnouncerWindow(this, database!);
            announcerWindow.Show();
            UpdateStatus();
        }

        public void NetworkUpdateResults()
        {
            httpServer?.UpdateInformation();
        }

        public static void NetworkAddResults() { }

        public void NetworkClearResults()
        {
            httpServer?.UpdateInformation();
        }

        public void StartHttpServer()
        {
            httpServer?.Stop();
            httpServer = new HttpServer(database!, httpServerPort);
        }

        public void StopHttpServer()
        {
            httpServer!.Stop();
            httpServer = null;
        }

        public bool HttpServerActive()
        {
            return httpServer != null;
        }

        public bool AnnouncerConnected()
        {
            foreach (TimingSystem system in TimingController!.GetConnectedSystems())
            {
                if (system.LocationID == Constants.Timing.LOCATION_ANNOUNCER)
                {
                    return true;
                }
            }
            return false;
        }

        public void AnnouncerClosing()
        {
            if (announcerWindow != null)
            {
                announcerWindow = null;
                UpdateStatus();
                Log.D("UI.MainWindow", "Announcer Window has closed.");
            }
            else
            {
                Log.D("UI.MainWindow", "Announcer Window was supposed to close but did not.");
            }
        }

        public bool AnnouncerOpen()
        {
            return announcerWindow != null;
        }

        public void StopAnnouncer()
        {
            announcerWindow?.Close();
        }

        public bool InDidNotStartMode()
        {
            bool output = false;
            if (dnsLock.TryEnter(3000))
            {
                try
                {
                    output = didNotStartMode;
                }
                finally
                {
                    dnsLock.Exit();
                }
            }
            else
            {
                Log.D("UI.MainWindow", "Error getting DNS Lock.");
            }
            return output;
        }

        public bool BackgroundProcessesRunning()
        {
            return TimingController.IsRunning() || AnnouncerOpen() || IsRegistrationRunning() || IsAPIControllerRunning() || IsRemoteRunning() == RemoteReadsController.RemoteStatus.RUNNING;
        }

        public void StopBackgroundProcesses()
        {
            try
            {
                StopTimingController();
            }
            catch { }
            try
            {
                StopAnnouncer();
            }
            catch { }
            try
            {
                StopRegistration();
            }
            catch { }
            try
            {
                StopAPIController();
            }
            catch { }
            try
            {
                StopRemote();
            }
            catch { }
        }

        public bool StartDidNotStartMode()
        {
            if (dnsLock.TryEnter(3000))
            {
                try
                {
                    didNotStartMode = true;
                    return true;
                }
                finally
                {
                    dnsLock.Exit();
                }
            }
            return false;
        }

        public bool StopDidNotStartMode()
        {
            if (dnsLock.TryEnter(3000))
            {
                try
                {
                    didNotStartMode = false;
                    return true;
                }
                finally
                {
                    dnsLock.Exit();
                }
            }
            return false;
        }

        public void NotifyAlarm(string Bib, string Chip)
        {
            Event? theEvent = database!.GetCurrentEvent();
            Application.Current!.Dispatcher.Invoke(new Action(async delegate ()
            {
                Alarm? alarm = null;
                if (Bib.Length > 0)
                {
                    alarm = Alarm.GetAlarmByBib(Bib);
                }
                else if (Chip.Length > 0)
                {
                    alarm = Alarm.GetAlarmByChip(Chip);
                }
                if (alarm != null && alarm.Enabled)
                {
                    alarm.Enabled = false;
                    Alarm.SaveAlarm(theEvent!.Identifier, database, alarm);
                    string soundFile = Environment.CurrentDirectory;
                    int sound = alarm.AlarmSound;
                    // Any value not between 1-10 (inclusive both) is defined to be the default sound.
                    if (sound < 1 || sound > 11)
                    {
                        // If for some reason we can't parse the value into integer, set it to 1.
                        if (!int.TryParse(database.GetAppSetting(Constants.Settings.ALARM_SOUND)!.Value, out sound))
                        {
                            sound = 0;
                        }
                    }
                    else
                    {
                        sound -= 1; // Sound in the 1-11 range is off by 1.
                    }
                    AudioPlaybackEngine.PlaySound(sound);
                }
                if (CurrentPage is TimingPage page)
                {
                    page.UpdateAlarms();
                }
            }));
        }

        public void ShowNotificationDialog(string ReaderName, string Address, RemoteNotification notification)
        {
            Log.D("UI.MainWindow", $"Show Notification Dialog called. When '{notification.When}' - Type '{notification.Type}' - ReaderName '{ReaderName}' - Address '{Address}'");
            ReaderMessage msg = new()
            {
                Message = notification,
                SystemName = ReaderName,
                Address = Address,
                Severity = notification.Type switch
                {
                    // All of the portal errors should display a dialogbox
                    // with information about what happened
                    PortalError.TOO_MANY_REMOTE_API or
                    PortalError.TOO_MANY_CONNECTIONS or
                    PortalError.SERVER_ERROR or
                    PortalError.DATABASE_ERROR or
                    PortalError.INVALID_READER_TYPE or
                    PortalError.READER_CONNECTION or
                    PortalError.NOT_FOUND or
                    PortalError.INVALID_SETTING or
                    PortalError.INVALID_API_TYPE or
                    PortalError.ALREADY_SUBSCRIBED or
                    PortalError.ALREADY_RUNNING or
                    PortalError.NOT_RUNNING or
                    PortalError.NO_REMOTE_API or
                    PortalError.STARTING_UP or
                    PortalError.INVALID_READ or
                    PortalError.NOT_ALLOWED or
                    PortalNotification.UPS_DISCONNECTED or
                    PortalNotification.UPS_ON_BATTERY or
                    PortalNotification.UPS_LOW_BATTERY or
                    PortalNotification.SHUTTING_DOWN or
                    PortalNotification.BATTERY_LOW or
                    PortalNotification.BATTERY_CRITICAL => ReaderMessage.SeverityLevel.High,
                    PortalNotification.MAX_TEMP => ReaderMessage.SeverityLevel.Moderate,
                    _ => ReaderMessage.SeverityLevel.Low,
                }
            };
            AddReaderMessage(msg);
            UpdateTimingNonBlocking();
        }

        public void Exit()
        {
            Close();
        }

        public void WindowFinalize(Window? w)
        {
            CurrentPage?.UpdateView();
            UpdateStatus();
        }
    }
}