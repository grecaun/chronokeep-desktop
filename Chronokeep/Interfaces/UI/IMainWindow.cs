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

