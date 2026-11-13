using Avalonia.Controls;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronokeepRemote;
using static Chronokeep.Timing.Remote.RemoteReadsController;

namespace Chronokeep.Test.UI.Infrastructure
{
    /// <summary>
    /// An <see cref="IMainWindow"/> that does nothing but record what pages and windows ask of it,
    /// so UI code can run without the real MainWindow, timing workers, or network services.
    /// It is a real Window because pages cast the main window to one to own dialogs
    /// (for example <c>ShowDialog((Window)mWindow)</c>).
    /// </summary>
    public sealed class FakeMainWindow : Window, IMainWindow
    {
        public List<IMainPage> SwitchedPages { get; } = [];
        public List<Window> AddedWindows { get; } = [];
        public int NetworkUpdateResultsCalls { get; private set; }
        public int NetworkClearResultsCalls { get; private set; }
        public int WindowFinalizeCalls { get; private set; }
        public int UpdateTimingCalls { get; private set; }
        public int UpdateStatusCalls { get; private set; }
        public List<string> Themes { get; } = [];

        // IWindowCallback
        public void WindowFinalize() => WindowFinalizeCalls++;

        // Window related calls.
        public void AddWindow(Window w) => AddedWindows.Add(w);
        public void SwitchPage(IMainPage iPage) => SwitchedPages.Add(iPage);
        public void Exit() { }

        // Networking services related calls.
        public void NetworkUpdateResults() => NetworkUpdateResultsCalls++;
        public void NetworkClearResults() => NetworkClearResultsCalls++;
        public int StartHttpServerCalls { get; private set; }
        public int StopHttpServerCalls { get; private set; }
        /// <summary>Set to make starting the web server fail, as it does without the URL reservation.</summary>
        public bool HttpServerFails { get; set; }
        public bool HttpServerRunning { get; set; }
        public void StartHttpServer()
        {
            StartHttpServerCalls++;
            if (HttpServerFails) throw new InvalidOperationException("Access denied.");
            HttpServerRunning = true;
        }
        public void StopHttpServer()
        {
            StopHttpServerCalls++;
            HttpServerRunning = false;
        }
        public bool HttpServerActive() => HttpServerRunning;

        // Tools.
        public void UpdateStatus() => UpdateStatusCalls++;
        public int UpdateTimingFromControllerCalls { get; private set; }
        public void UpdateTimingFromController() => UpdateTimingFromControllerCalls++;
        public void UpdateTiming() => UpdateTimingCalls++;
        public void UpdateRegistrationDistances() { }
        public void UpdateParticipantsFromRegistration() { }
        /// <summary>Set to make pages think timing or uploads are running in the background.</summary>
        public bool BackgroundRunning { get; set; }
        public bool BackgroundProcessesRunning() => BackgroundRunning;
        public int StopBackgroundProcessesCalls { get; private set; }
        public void StopBackgroundProcesses()
        {
            StopBackgroundProcessesCalls++;
            BackgroundRunning = false;
        }

        // Timing System related calls.
        /// <summary>Readers the Timing page asked to connect, in order.</summary>
        public List<TimingSystem> ConnectRequests { get; } = [];
        /// <summary>Readers the Timing page asked to disconnect, in order.</summary>
        public List<TimingSystem> DisconnectRequests { get; } = [];
        /// <summary>What <see cref="GetConnectedSystems"/> reports; set to put readers on the Timing page.</summary>
        public List<TimingSystem> ConnectedSystems { get; } = [];
        public void ConnectTimingSystem(TimingSystem system) => ConnectRequests.Add(system);
        public void DisconnectTimingSystem(TimingSystem system) => DisconnectRequests.Add(system);
        public void TimingSystemDisconnected(TimingSystem system) { }
        public void ShutdownTimingController() { }
        public List<TimingSystem> GetConnectedSystems() => [.. ConnectedSystems];
        public int NotifyTimingWorkerCalls { get; private set; }
        public void NotifyTimingWorker() => NotifyTimingWorkerCalls++;
        public bool DnsMode { get; private set; }
        public bool InDidNotStartMode() => DnsMode;
        public bool StartDidNotStartMode() => DnsMode = true;
        public bool StopDidNotStartMode()
        {
            DnsMode = false;
            return true;
        }
        public void NotifyAlarm(string bib, string chip) { }

        // Announcer related calls.
        public bool AnnouncerOpen() => false;
        public void StopAnnouncer() { }
        public int StartAnnouncerWorkerCalls { get; private set; }
        public bool StartAnnouncerWorker()
        {
            StartAnnouncerWorkerCalls++;
            return false;
        }
        public bool StopAnnouncerWorker() => false;

        // API System related calls.
        public int StartApiControllerCalls { get; private set; }
        public int StopApiControllerCalls { get; private set; }
        public void StartApiController() => StartApiControllerCalls++;
        public bool StopApiController()
        {
            StopApiControllerCalls++;
            return true;
        }
        public bool IsApiControllerRunning() => false;
        public int ApiErrors() => 0;

        // Remote Controller related calls.
        public int StartRemoteCalls { get; private set; }
        public int StopRemoteCalls { get; private set; }
        /// <summary>What the remote reads downloader reports, for pages that show its state.</summary>
        public RemoteStatus RemoteState { get; set; } = RemoteStatus.STOPPED;
        public int RemoteErrorCount { get; set; }
        public void StartRemote() => StartRemoteCalls++;
        public void StopRemote() => StopRemoteCalls++;
        public RemoteStatus IsRemoteRunning() => RemoteState;
        public int RemoteErrors() => RemoteErrorCount;
        public void ShowNotificationDialog(string readerName, string address, RemoteNotification notification) { }

        // Theme related calls
        public void UpdateTheme(string theme) => Themes.Add(theme);

        // Registration related calls
        /// <summary>Whether starting registration succeeds; a failed start leaves it stopped.</summary>
        public bool RegistrationCanStart { get; set; } = true;
        public bool RegistrationRunning { get; private set; }
        public bool StartRegistration() => RegistrationRunning = RegistrationCanStart;
        public bool StopRegistration()
        {
            RegistrationRunning = false;
            return true;
        }
        public bool IsRegistrationRunning() => RegistrationRunning;
    }
}
