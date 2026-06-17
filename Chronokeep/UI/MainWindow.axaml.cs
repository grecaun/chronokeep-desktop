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
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static Chronokeep.Helpers.Globals;

namespace Chronokeep.UI
{
    public partial class MainWindow : ChronokeepWindow, IMainWindow
    {
        internal static Window? MWindow;
        private IMainPage? currentPage;
        private static bool forceClose;

        private readonly MemStore.MemStore? database;
        internal static string DatabaseFileName = "Chronokeep.sqlite";

        // Network objects
        private HttpServer? httpServer;
        private const int HttpServerPort = 6933;

        // Zero Conf/Registration objects.
        private Thread? zConfThread;
        private ZeroConf? zConfServer;
        private Thread? registrationThread;
        private RegistrationWorker? registrationWorker;

        // Timing objects.
        private Thread? timingControllerThread;
        private TimingController? timingController;
        private Thread? timingWorkerThread;
        private TimingWorker? timingWorker;

        // API objects.
        private Thread? apiControllerThread;
        private ApiController? apiController;

        // Remote Reads objects
        private Thread? remoteThread;
        private RemoteReadsController? remoteController;

        // Announcer objects
        private AnnouncerWindow? announcerWindow;

        private readonly List<Window> openWindows = [];

        // Setting to allow the user to enter a mode where we can record DNS chips.
        private bool didNotStartMode;
        private readonly Lock dnsLock = new();

        // Setup a timer for updating the view
        private readonly DispatcherTimer timingUpdater = new();

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
            ChronokeepInitialize();
            MWindow = this;

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
            database = MemStore.MemStore.GetMemStore(new SqLiteInterface(path));
            try
            {
                database.Initialize();
            }
            catch (InvalidDatabaseVersion db)
            {
                DialogBox.Show(
                    $"Database version greater than the max known by this client. Please update the client. Database version {db.FoundVersion}. Max version for this client {db.MaxVersion}");
                Close();
                return;
            }
            Constants.Settings.SetupSettings(database);

            // Ensure Global values are set up.
            SetupValues(database);

            // Setup AgeGroup static variables
            Event? theEvent = database.GetCurrentEvent();

            currentPage = new DashboardPage(this, database);
            CurrentContent.Content = currentPage;

            UpdateStatus();

            // Check for updates.
            if (database.GetAppSetting(Constants.Settings.CHECK_UPDATES)!.Value == Constants.Settings.SETTING_TRUE)
            {
                Updates.Check.Do(this);
            }

            DataContext = this;

            // Set timing update to every two tenths of a second.
            timingUpdater.Tick += UpdateTimingTick;
            timingUpdater.Interval = new TimeSpan(0, 0, 0, 0, 200);
            timingUpdater.Start();

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
            if (!forceClose && database.GetAppSetting(Constants.Settings.EXIT_NO_PROMPT)!.Value == Constants.Settings.SETTING_FALSE &&
                (BackgroundProcessesRunning()))
            {
                DialogBox.Show(
                    "Are you sure you wish to exit?",
                    "Yes",
                    "No",
                    () =>
                        {
                            forceClose = true;
                            MWindow?.Close();
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
            catch
            {
                Log.D("UI.MainWindow", "Error stopping timing controller.");    
            }
            try
            {
                StopTimingWorker();
            }
            catch
            {
                Log.D("UI.MainWindow", "Error stopping timing worker.");    
            }
            try
            {
                StopApiController();
            }
            catch
            {
                Log.D("UI.MainWindow", "Error stopping api controller.");    
            }
            try
            {
                StopAnnouncer();
            }
            catch
            {
                Log.D("UI.MainWindow", "Error stopping announcer.");    
            }
            try
            {
                StopRegistration();
            }
            catch
            {
                Log.D("UI.MainWindow", "Error stopping registration.");    
            }
            try
            {
                httpServer?.Stop();
            }
            catch
            {
                Log.D("UI.MainWindow", "Error stopping http server.");    
            }
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
                currentPage?.Closing();
            }
            catch
            {
                Log.D("UI.MainWindow", "Error closing current page.");    
            }
            try
            {
                timingUpdater.Stop();
            }
            catch
            {
                Log.D("UI.MainWindow", "Error stopping timing updater.");    
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            timingController = new TimingController(this, database!);
            timingWorker = TimingWorker.NewWorker(this, database!);
            timingWorkerThread = new Thread(timingWorker.Run);
            timingWorkerThread.Start();
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
            Thread hardwareThread = new(hwCheck.Run);
            hardwareThread.Start();
            UpdateTimingBadge();
            // Check last known program version
            Log.D("UI.MainWindow", "Starting changelog version checker.");
            string gitVersion;
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
            AppSetting? programVers = database.GetAppSetting(Constants.Settings.PROGRAM_VERSION);
            AppSetting? showChangelog = database.GetAppSetting(Constants.Settings.AUTO_SHOW_CHANGELOG);
            if (programVers == null && showChangelog is { Value: Constants.Settings.SETTING_TRUE })
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
                        if (showChangelog is { Value: Constants.Settings.SETTING_TRUE })
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
            currentPage?.Closing();
            currentPage = iPage;
            CurrentContent.Content = currentPage;
        }

        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            Log.D("UI.MainWindow", "Dashboard button clicked.");
            if (currentPage is DashboardPage)
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
            if (currentPage is TimingPage page)
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
            if (currentPage is ParticipantsPage)
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
            if (currentPage is ChipAssignmentPage)
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
            if (currentPage is LocationsPage)
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
            if (currentPage is DistancesPage)
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
            if (currentPage is SegmentsPage)
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
            if (currentPage is AgeGroupsPage)
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
            if (currentPage is SettingsPage)
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
            if (currentPage is AboutPage)
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
            return (registrationWorker != null && registrationWorker.IsRunning()) && (zConfServer != null && ZeroConf.IsRunning());
        }

        public bool StopRegistration()
        {
            bool output = true;
            try
            {
                Log.D("UI.MainWindow", "Stopping zero conf.");
                zConfServer?.Stop();
            }
            catch
            {
                output = false;
            }
            try
            {
                Log.D("UI.MainWindow", "Stopping registration.");
                registrationWorker?.Stop();
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
                zConfServer = new ZeroConf(zconfName?.Value);
                zConfThread = new Thread(zConfServer.Run);
                zConfThread.Start();
            }
            catch
            {
                output = false;
            }
            try
            {
                Log.D("UI.MainWindow", "Starting registration.");
                registrationWorker = new RegistrationWorker(database!, this);
                registrationThread = new Thread(registrationWorker.Run);
                registrationThread.Start();
            }
            catch
            {
                output = false;
            }
            return output;
        }

        public void UpdateRegistrationDistances()
        {
            registrationWorker?.UpdateDistances();
        }

        private static void StopTimingWorker()
        {
            try
            {
                Log.D("UI.MainWindow", "Stopping Timing Worker.");
                TimingWorker.Shutdown();
                TimingWorker.Notify();
            }
            catch
            {
                Log.D("UI.MainWindow", "Error stopping Timing Worker.");
            }
        }

        private void StopTimingController()
        {
            try
            {
                Log.D("UI.MainWindow", "Stopping Timing Controller.");
                timingController?.Shutdown();
            }
            catch
            {
                Log.D("UI.MainWindow", "Error stopping Timing Controller.");
            }
        }

        public bool StopApiController()
        {
            try
            {
                Log.D("UI.MainWindow", "Stopping API Controller");
                if (apiController != null)
                {
                    ApiController.Shutdown();
                }
                apiController = null;
            }
            catch
            {
                return false;
            }
            currentPage?.UpdateView();
            return true;
        }

        public async void StartApiController()
        {
            try
            {
                await Task.Run(() =>
                {
                    if (ApiController.IsRunning()) return;
                    apiController = new ApiController(this, database!);
                    apiControllerThread = new Thread(apiController.Run);
                    apiControllerThread.Start();
                });
            }
            catch (Exception)
            {
                Log.D("UI.MainWindow", "Error starting api controller.");
            }
        }

        public bool IsApiControllerRunning()
        {
            return apiController != null && ApiController.IsRunning();
        }

        public int ApiErrors()
        {
            return apiController?.Errors ?? 0;
        }

        public void UpdateParticipantsFromRegistration()
        {
            Application.Current!.Dispatcher.Invoke(delegate
            {
                if (currentPage is ParticipantsPage)
                {
                    currentPage.UpdateView();
                }
            });
        }

        public void UpdateTimingFromController()
        {
            TimingWorker.Notify();
            Application.Current!.Dispatcher.Invoke(delegate
            {
                if (currentPage is TimingPage timingPage)
                {
                    timingPage.UpdateView();
                    timingPage.NewMessage();
                }
                announcerWindow?.UpdateView();
            });
        }

        public void StartRemote()
        {
            Task.Run(() =>
            {
                Log.D("UI.MainWindow", "Checking Remote Thread");
                if (RemoteReadsController.IsRunning() != RemoteReadsController.RemoteStatus.STOPPED) return;
                Log.D("UI.MainWindow", "Starting Remote Thread");
                remoteController = new RemoteReadsController(this, database!);
                remoteThread = new Thread(remoteController.Run);
                remoteThread.Start();
            });
        }

        public void StopRemote()
        {
            Task.Run(() =>
            {
                Log.D("UI.MainWindow", "Stopping Remote Controller");
                RemoteReadsController.Shutdown();
                remoteController = null;
            });
        }

        public RemoteReadsController.RemoteStatus IsRemoteRunning()
        {
            return RemoteReadsController.IsRunning();
        }

        public int RemoteErrors()
        {
            return remoteController?.Errors ?? 0;
        }

        public void UpdateAnnouncerWindow()
        {
            // Let the announcer window know that it has new information.
            Application.Current!.Dispatcher.Invoke(delegate
            {
                announcerWindow?.UpdateView();
            });
        }

        public void UpdateTiming()
        {
            // Let the timing page know that it has new information.
            Application.Current!.Dispatcher.Invoke(delegate
            {
                if (currentPage is TimingPage)
                {
                    currentPage.UpdateView();
                }
            });
        }

        private void UpdateTimingNonBlocking()
        {
            Log.D("UI.MainWindow", "UpdateTimingNonBlocking called.");
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
                // show any dialogboxes that need to be shown due to importance
                foreach (ReaderMessage message in toShow)
                {
                    Application.Current!.Dispatcher.Invoke(delegate
                    {
                        DialogBox.Show(message.DialogBoxString);
                    });
                }
                // Let the announcer window know that it has new information.
                Application.Current!.Dispatcher.Invoke(delegate
                {
                    if (currentPage is TimingPage)
                    {
                        currentPage.UpdateView();
                    }
                });
            });
            newThread.Start();
        }

        private void UpdateTimingTick(object? sender, EventArgs e)
        {
            if (!TimingWorker.NewResultsExist()) return;
            if (currentPage is TimingPage timingPage)
            {
                timingPage.UpdateSubView();
            }
            announcerWindow?.UpdateTiming();
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
            DashboardButton.IsChecked = currentPage is DashboardPage;
            TimingButton.IsChecked = currentPage is TimingPage;
            AnnouncerButton.IsChecked = announcerWindow != null;
            ParticipantsButton.IsChecked = currentPage is ParticipantsPage;
            ChipsButton.IsChecked = currentPage is ChipAssignmentPage;
            LocationsButton.IsChecked = currentPage is LocationsPage;
            DistancesButton.IsChecked = currentPage is DistancesPage;
            SegmentsButton.IsChecked = currentPage is SegmentsPage;
            AgeGroupsButton.IsChecked = currentPage is AgeGroupsPage;
            SettingsButton.IsChecked = currentPage is SettingsPage;
            AboutButton.IsChecked = currentPage is AboutPage;
            UpdateTimingBadge();
        }

        private void UpdateTimingBadge()
        {
            if (currentPage is TimingPage) return;
            List<ReaderMessage> messages = GetReaderMessages();
            messages.RemoveAll(x => x.Notified);
            if (messages.Count > 0)
            { }
        }

        public async void ConnectTimingSystem(TimingSystem system)
        {
            try
            {
                await Task.Run(() =>
                {
                    timingController!.ConnectTimingSystem(system);
                });
                UpdateTiming();
                announcerWindow?.UpdateView();
                await Task.Run(() =>
                {
                    if (TimingController.IsRunning()) return;
                    timingControllerThread = new Thread(timingController!.Run);
                    timingControllerThread.Start();
                });
            }
            catch (Exception)
            {
                Log.D("UI.MainWindow", "Error starting timing system.");
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
                announcerWindow?.UpdateView();
            }
            catch (Exception)
            {
                Log.D("UI.MainWindow", "Error disconnecting from timing system.");
            }
        }

        public void ShutdownTimingController()
        {
            timingController!.Shutdown();
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
            try
            {
                Application.Current!.Dispatcher.Invoke(delegate
                {
                    if (system.SystemInterface != null)
                    {
                        if (!system.SystemInterface.WasShutdown())
                        {
                            DialogBox.Show(
                                $"Reader at {system.LocationName} has unexpectedly disconnected. IP Address was {system.IpAddress}.");
                        }
                    }
                    system.Status = SYSTEM_STATUS.DISCONNECTED;
                    UpdateTiming();
                    announcerWindow?.UpdateView();
                });
            }
            catch (TaskCanceledException) { }
            catch (Exception e)
            {
                Log.E("UI.MainWindow", $"Exception occurred trying to update disconnected timing system. {e}");
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
            Log.E("UI.MainWindow", $"announcerWindow is null? {announcerWindow == null}");
            if (announcerWindow != null)
            {
                announcerWindow.Hide();
                announcerWindow.Show();
                UpdateStatus();
                return;
            }
            Log.E("UI.MainWindow", "beep boop");
            announcerWindow = new AnnouncerWindow(this, database!);
            announcerWindow.Show();
            UpdateStatus();
        }

        public void NetworkUpdateResults()
        {
            httpServer?.UpdateInformation();
        }

        public void NetworkClearResults()
        {
            httpServer?.UpdateInformation();
        }

        public void StartHttpServer()
        {
            httpServer?.Stop();
            httpServer = new HttpServer(database!, HttpServerPort);
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
            foreach (TimingSystem system in timingController!.GetConnectedSystems())
            {
                if (system.LocationId == Constants.Timing.LOCATION_ANNOUNCER)
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
            return TimingController.IsRunning() || AnnouncerOpen() || IsRegistrationRunning() || IsApiControllerRunning() || IsRemoteRunning() == RemoteReadsController.RemoteStatus.RUNNING;
        }

        public void StopBackgroundProcesses()
        {
            try
            {
                StopTimingController();
            }
            catch
            {
                Log.D("UI.MainWindow", "Error stopping Timing Controller.");
            }
            try
            {
                StopAnnouncer();
            }
            catch
            {
                Log.D("UI.MainWindow", "Error stopping announcer.");
            }
            try
            {
                StopRegistration();
            }
            catch
            {
                Log.D("UI.MainWindow", "Error stopping registration.");
            }
            try
            {
                StopApiController();
            }
            catch
            {
                Log.D("UI.MainWindow", "Error stopping api controller.");
            }
            try
            {
                StopRemote();
            }
            catch
            {
                Log.D("UI.MainWindow", "Error stopping remote.");
            }
        }

        public bool StartDidNotStartMode()
        {
            if (!dnsLock.TryEnter(3000)) return false;
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

        public bool StopDidNotStartMode()
        {
            if (!dnsLock.TryEnter(3000)) return false;
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

        public void NotifyAlarm(string bib, string chip)
        {
            Event? theEvent = database!.GetCurrentEvent();
            Application.Current!.Dispatcher.Invoke(delegate
            {
                Alarm? alarm = null;
                if (bib.Length > 0)
                {
                    alarm = Alarm.GetAlarmByBib(bib);
                }
                else if (chip.Length > 0)
                {
                    alarm = Alarm.GetAlarmByChip(chip);
                }
                if (alarm is { Enabled: true })
                {
                    alarm.Enabled = false;
                    Alarm.SaveAlarm(theEvent!.Identifier, database, alarm);
                    int sound = alarm.AlarmSound;
                    // Any value not between 1-10 (inclusive both) is defined to be the default sound.
                    if (sound is < 1 or > 11)
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
                if (currentPage is TimingPage page)
                {
                    page.UpdateAlarms();
                }
            });
        }

        public void ShowNotificationDialog(string readerName, string address, RemoteNotification notification)
        {
            Log.D("UI.MainWindow", $"Show Notification Dialog called. When '{notification.When}' - Type '{notification.Type}' - ReaderName '{readerName}' - Address '{address}'");
            ReaderMessage msg = new()
            {
                Message = notification,
                SystemName = readerName,
                Address = address,
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

        public void WindowFinalize()
        {
            currentPage?.UpdateView();
            UpdateStatus();
        }
    }
}