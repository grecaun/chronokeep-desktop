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

using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.Timing;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Chronokeep.Timing
{
    internal class TimingController(IMainWindow mainWindow, IdbInterface database)
    {
        // Connecting and disconnecting happen on other threads while the loop runs,
        // so the socket list and dictionary are only touched while holding TimingLock.
        private readonly List<Socket> timingSystemSockets = [], readList = [];
        private readonly Dictionary<Socket, TimingSystem> timingSystemDict = [];

        private static readonly Lock TimingLock = new();
        private static readonly Lock ReadsLock = new();
        private static bool running;
        private static bool newReads;

        public static bool IsRunning()
        {
            bool output = false;
            Log.D("Timing.TimingController", "Lock Wait 01");
            if (!TimingLock.TryEnter(6000)) return output;
            try
            {
                output = running;
            }
            finally
            {
                TimingLock.Exit();
            }
            return output;
        }

        public static bool NewReadsExist()
        {
            bool output = false;
            Log.D("Timing.TimingController", "Lock Wait 02");
            if (!ReadsLock.TryEnter(3000)) return output;
            try
            {
                output = newReads;
                newReads = false;
            }
            finally
            {
                ReadsLock.Exit();
            }
            return output;
        }

        public List<TimingSystem> GetConnectedSystems()
        {
            lock (TimingLock)
            {
                return [.. timingSystemDict.Values];
            }
        }

        public void ConnectTimingSystem(TimingSystem system)
        {
            Log.D("Timing.TimingController", "-- UPDATE -- Creating interface for communication with timing system.");
            system.CreateTimingSystemInterface(database, mainWindow);
            // Connecting can take a few seconds, so don't hold the lock for it.
            List<Socket>? sockets = system.Connect();
            if (sockets == null || sockets.Count < 1)
            {
                Log.D("Timing.TimingController", "No sockets returned.");
                system.Status = SYSTEM_STATUS.DISCONNECTED;
                return;
            }
            bool anyConnected = false;
            lock (TimingLock)
            {
                foreach (Socket sock in sockets)
                {
                    if (!sock.Connected) continue;
                    Log.D("Timing.TimingController", $"Connected to {system.IpAddress}");
                    timingSystemDict[sock] = system;
                    timingSystemSockets.Add(sock);
                    anyConnected = true;
                }
            }
            system.Status = anyConnected ? SYSTEM_STATUS.CONNECTED : SYSTEM_STATUS.DISCONNECTED;
        }

        /// <summary>
        /// Starts the loop that reads from the connected timing systems, unless it's already running.
        /// Call this after connecting a timing system.
        /// </summary>
        public void Start()
        {
            lock (TimingLock)
            {
                if (running || timingSystemSockets.Count == 0) return;
                // Marked running before the thread starts so two quick connects can't start two loops.
                running = true;
            }
            new Thread(Loop).Start();
        }

        public void Shutdown()
        {
            lock (TimingLock)
            {
                foreach (Socket sock in timingSystemSockets)
                {
                    sock.Close();
                }
            }
        }

        public void DisconnectTimingSystem(TimingSystem system)
        {
            // Forget the sockets first so the loop doesn't report this as an unexpected disconnect.
            lock (TimingLock)
            {
                foreach (Socket sock in system.Sockets ?? [])
                {
                    timingSystemSockets.Remove(sock);
                    timingSystemDict.Remove(sock);
                }
            }
            system.Disconnect();
            system.Status = SYSTEM_STATUS.DISCONNECTED;
        }

        /// <summary>Removes a socket, returning its timing system if it hadn't already been removed.</summary>
        private TimingSystem? RemoveSocket(Socket sock)
        {
            lock (TimingLock)
            {
                if (!timingSystemDict.Remove(sock, out TimingSystem? system)) return null;
                timingSystemSockets.Remove(sock);
                return system;
            }
        }

        /// <summary>Runs the read loop on the calling thread, unless it's already running.</summary>
        public void Run()
        {
            Log.D("Timing.TimingController", "Timing Controller is now running.");
            lock (TimingLock)
            {
                if (running)
                {
                    Log.D("Timing.TimingController", "Timing Controller Thread already running.");
                    return;
                }
                running = true;
            }
            Loop();
        }

        private void Loop()
        {
            while (true)
            {
                Log.D("Timing.TimingController", "Loop start.");
                lock (TimingLock)
                {
                    // Deciding to stop and marking the loop stopped happen together, so a timing system
                    // connected at the same moment either gets read here or gets a new loop from Start().
                    if (timingSystemSockets.Count == 0)
                    {
                        running = false;
                        return;
                    }
                    readList.Clear();
                    readList.AddRange(timingSystemSockets);
                }
                try
                {
                    Socket.Select(readList, null, null, 3000000);
                }
                catch
                {
                    Log.D("Timing.TimingController", "Socket select not working.");
                }
                foreach (Socket sock in readList)
                {
                    TimingSystem? system;
                    lock (TimingLock)
                    {
                        timingSystemDict.TryGetValue(sock, out system);
                    }
                    if (system == null)
                    {
                        // Disconnected on purpose since the select.
                        continue;
                    }
                    Log.D("Timing.TimingController", "Reading from socket.");
                    bool chipRead = false;
                    bool updateTiming = false;
                    byte[] received = new byte[4112];
                    try
                    {
                        int numReceived = sock.Receive(received);
                        if (numReceived == 0)
                        {
                            Log.D("Timing.TimingController", "No longer connected to Timing System");
                            if (RemoveSocket(sock) != null)
                            {
                                mainWindow.TimingSystemDisconnected(system);
                            }
                        }
                        else
                        {
                            string msg = Encoding.UTF8.GetString(received, 0, numReceived);
                            Log.D("Timing.TimingController", $"Timing System - Message is :{msg.Trim()}");
                            Dictionary<MessageType, List<string>> messageTypes = system.SystemInterface!.ParseMessages(msg, sock);
                            foreach (MessageType type in messageTypes.Keys)
                            {
                                switch (type)
                                {
                                    case MessageType.CONNECTED:
                                        Log.D("Timing.TimingController", "Timing system successfully connected.");
                                        system.Status = SYSTEM_STATUS.CONNECTED;
                                        updateTiming = true;
                                        break;
                                    case MessageType.CHIPREAD:
                                        Log.D("Timing.TimingController", "ChipReads found");
                                        chipRead = true;
                                        break;
                                    case MessageType.SETTINGCHANGE:
                                        Log.D("Timing.TimingController", "Setting value changed.");
                                        break;
                                    case MessageType.SETTINGVALUE:
                                        Log.D("Timing.TimingController", "Setting value given.");
                                        break;
                                    case MessageType.VOLTAGENORMAL:
                                        Log.D("Timing.TimingController", "System voltage normal.");
                                        break;
                                    case MessageType.VOLTAGELOW:
                                        Log.D("Timing.TimingController", "System voltage low.");
                                        break;
                                    case MessageType.TIME:
                                        Log.D("Timing.TimingController", "Time value received.");
                                        system.SystemTime = messageTypes[MessageType.TIME].First();
                                        updateTiming = true;
                                        break;
                                    case MessageType.STATUS:
                                        Log.D("Timing.TimingController", "Status message received.");
                                        system.SystemStatus = messageTypes[MessageType.STATUS].Last();
                                        updateTiming = true;
                                        break;
                                    case MessageType.ERROR:
                                        Log.D("Timing.TimingController", "Error from timing system.");
                                        break;
                                    case MessageType.UNKNOWN:
                                    case MessageType.NONE:
                                    case MessageType.SUCCESS:
                                    case MessageType.DISCONNECT:
                                    default:
                                        break;
                                }
                            }
                        }
                        if (updateTiming && !sock.Poll(100, SelectMode.SelectRead))
                        {
                            mainWindow.UpdateTimingFromController();
                        }
                        if (chipRead)
                        {
                            Log.D("Timing.TimingController", "Lock Wait 05");
                            if (ReadsLock.TryEnter(3000))
                            {
                                try
                                {
                                    newReads = true;
                                    mainWindow.NotifyTimingWorker();
                                }
                                finally
                                {
                                    ReadsLock.Exit();
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.E("Timing.TimingController", $"Error trying to parse messages. {e.Message}");
                        if (RemoveSocket(sock) != null)
                        {
                            Log.D("Timing.TimingController", "Socket errored on us.");
                            try
                            {
                                system.SystemInterface!.CloseSettings();
                            }
                            catch (Exception ex)
                            {
                                Log.E("Timing.TimingController", $"Error attempting to close settings. {ex.Message}");
                            }
                            mainWindow.TimingSystemDisconnected(system);
                        }
                        else
                        {
                            Log.D("Timing.TimingController", "Successful disconnect.");
                        }
                    }
                }
                Log.D("Timing.TimingController", "Loop end.");
            }
        }
    }
}
