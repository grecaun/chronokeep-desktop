using Avalonia.Controls;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronokeepRemote;
using System.Collections.Generic;
using static Chronokeep.Timing.Remote.RemoteReadsController;

namespace Chronokeep.Interfaces.UI
{
    public interface IMainWindow : IWindowCallback
    {
        // Window related calls.
        void AddWindow(Window w);
        void SwitchPage(IMainPage iPage);
        void Exit();

        // Networking services related calls.
        void NetworkUpdateResults();
        void NetworkClearResults();
        void StartHttpServer();
        void StopHttpServer();
        bool HttpServerActive();

        // Tools.
        void UpdateStatus();
        void UpdateTimingFromController();
        void UpdateTiming();
        void UpdateAnnouncerWindow();
        void UpdateRegistrationDistances();
        void UpdateParticipantsFromRegistration();
        bool BackgroundProcessesRunning();
        void StopBackgroundProcesses();

        // Timing System related calls.
        void ConnectTimingSystem(TimingSystem system);
        void DisconnectTimingSystem(TimingSystem system);
        void TimingSystemDisconnected(TimingSystem system);
        void ShutdownTimingController();
        List<TimingSystem> GetConnectedSystems();
        void NotifyTimingWorker();
        bool InDidNotStartMode();
        bool StartDidNotStartMode();
        bool StopDidNotStartMode();
        void NotifyAlarm(string bib, string chip);

        // Announcer related calls.
        bool AnnouncerConnected();
        void AnnouncerClosing();
        bool AnnouncerOpen();
        void StopAnnouncer();

        // API System related calls.
        void StartApiController();
        bool StopApiController();
        bool IsApiControllerRunning();
        int ApiErrors();

        // Remote Controller related calls.
        void StartRemote();
        void StopRemote();
        RemoteStatus IsRemoteRunning();
        int RemoteErrors();
        void ShowNotificationDialog(string readerName, string address, RemoteNotification notification);

        // Theme related calls
        void UpdateTheme(string theme);

        // Registration related calls
        bool StartRegistration();
        bool StopRegistration();
        bool IsRegistrationRunning();
    }
}
