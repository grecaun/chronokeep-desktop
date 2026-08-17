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

using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace Chronokeep.Interfaces.Timing
{
    public interface ITimingSystemInterface
    {
        Dictionary<MessageType, List<string>> ParseMessages(string message, Socket sock);
        List<Socket>? Connect(string ipAddress, int port);
        void Disconnect();
        void StartReading();
        void StopReading();
        void SetTime(DateTime date);
        void GetTime();
        void GetStatus();
        void StartSending();
        void StopSending();
        void Rewind(DateTime start, DateTime end, int reader = 1);
        void Rewind(int from, int to, int reader = 1);
        void Rewind(int reader = 1);
        void SetMainSocket(Socket iSock);
        void SetSettingsSocket(Socket sock);
        bool SettingsEditable();
        void OpenSettings();
        void CloseSettings();
        bool WasShutdown();
    }

    public enum MessageType
    {
        CONNECTED,
        VOLTAGENORMAL,
        VOLTAGELOW,
        CHIPREAD,
        TIME,
        SETTINGVALUE,
        SETTINGCHANGE,
        STATUS,
        UNKNOWN,
        ERROR,
        NONE,
        SUCCESS,
        DISCONNECT
    }
}

