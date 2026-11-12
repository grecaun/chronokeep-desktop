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
        public void StartHttpServer() { }
        public void StopHttpServer() { }
        public bool HttpServerActive() => false;

        // Tools.
        public void UpdateStatus() => UpdateStatusCalls++;
        public void UpdateTimingFromController() { }
        public void UpdateTiming() => UpdateTimingCalls++;
        public void UpdateAnnouncerWindow() { }
        public void UpdateRegistrationDistances() { }
        public void UpdateParticipantsFromRegistration() { }
        public bool BackgroundProcessesRunning() => false;
        public void StopBackgroundProcesses() { }

        // Timing System related calls.
        public void ConnectTimingSystem(TimingSystem system) { }
        public void DisconnectTimingSystem(TimingSystem system) { }
        public void TimingSystemDisconnected(TimingSystem system) { }
        public void ShutdownTimingController() { }
        public List<TimingSystem> GetConnectedSystems() => [];
        public void NotifyTimingWorker() { }
        public bool InDidNotStartMode() => false;
        public bool StartDidNotStartMode() => false;
        public bool StopDidNotStartMode() => false;
        public void NotifyAlarm(string bib, string chip) { }

        // Announcer related calls.
        public bool AnnouncerConnected() => false;
        public void AnnouncerClosing() { }
        public bool AnnouncerOpen() => false;
        public void StopAnnouncer() { }
        public bool StartAnnouncerWorker() => false;
        public bool StopAnnouncerWorker() => false;

        // API System related calls.
        public void StartApiController() { }
        public bool StopApiController() => false;
        public bool IsApiControllerRunning() => false;
        public int ApiErrors() => 0;

        // Remote Controller related calls.
        public void StartRemote() { }
        public void StopRemote() { }
        public RemoteStatus IsRemoteRunning() => RemoteStatus.STOPPED;
        public int RemoteErrors() => 0;
        public void ShowNotificationDialog(string readerName, string address, RemoteNotification notification) { }

        // Theme related calls
        public void UpdateTheme(string theme) => Themes.Add(theme);

        // Registration related calls
        public bool StartRegistration() => false;
        public bool StopRegistration() => false;
        public bool IsRegistrationRunning() => false;
    }
}
