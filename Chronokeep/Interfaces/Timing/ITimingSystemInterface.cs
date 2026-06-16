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
