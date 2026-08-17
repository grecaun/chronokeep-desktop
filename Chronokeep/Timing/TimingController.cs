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
            List<TimingSystem> output = [.. timingSystemDict.Values];
            return output;
        }

        public void ConnectTimingSystem(TimingSystem system)
        {
            Log.D("Timing.TimingController", "-- UPDATE -- Creating interface for communication with timing system.");
            system.CreateTimingSystemInterface(database, mainWindow);
            List<Socket> sockets = system.Connect()!;
            if (sockets == null || sockets.Count < 1)
            {
                Log.D("Timing.TimingController", "No sockets returned.");
                system.Status = SYSTEM_STATUS.DISCONNECTED;
            }
            else
            {
                int i = 1;
                foreach (Socket sock in sockets)
                {
                    Log.D("Timing.TimingController", $"Socket {i}");
                    i++;
                    timingSystemDict[sock] = system;
                    if (sock.Connected)
                    {
                        Log.D("Timing.TimingController", $"Connected to {system.IpAddress}");
                        timingSystemSockets.Add(sock);
                        timingSystemDict[sock].SetLastCommunicationTime();
                        timingSystemDict[sock].Status = SYSTEM_STATUS.CONNECTED;
                    }
                    else
                    {
                        timingSystemDict.Remove(sock);
                        if (!timingSystemDict.ContainsValue(system))
                        {
                            system.Status = SYSTEM_STATUS.DISCONNECTED;
                        }
                    }
                }
            }
        }

        public void Shutdown()
        {
            foreach (Socket sock in timingSystemSockets)
            {
                sock.Close();
            }
        }

        public void DisconnectTimingSystem(TimingSystem system)
        {
            system.Disconnect();
            foreach (Socket sock in system.Sockets!)
            {
                timingSystemSockets.Remove(sock);
                timingSystemDict.Remove(sock);
            }
            system.Status = SYSTEM_STATUS.DISCONNECTED;
        }

        public void Run()
        {
            Log.D("Timing.TimingController", "Timing Controller is now running.");
            if (TimingLock.TryEnter(3000))
            {
                try
                {
                    if (running)
                    {
                        Log.D("Timing.TimingController", "Timing Controller Thread already running.");
                        return;
                    }
                    running = true;
                }
                finally
                {
                    TimingLock.Exit();
                }
            }
            else
            {
                Log.D("Timing.TimingController", "Unable to acquire lock.");
                return;
            }
            while (timingSystemSockets.Count > 0)
            {
                Log.D("Timing.TimingController", "Loop start.");
                readList.Clear();
                readList.AddRange(timingSystemSockets);
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
                            TimingSystem disconnected = timingSystemDict[sock];
                            timingSystemSockets.Remove(sock);
                            timingSystemDict.Remove(sock);
                            mainWindow.TimingSystemDisconnected(disconnected);
                        }
                        else
                        {
                            string msg = Encoding.UTF8.GetString(received, 0, numReceived);
                            Log.D("Timing.TimingController", $"Timing System - Message is :{msg.Trim()}");
                            Dictionary<MessageType, List<string>> messageTypes = timingSystemDict[sock].SystemInterface!.ParseMessages(msg, sock);
                            foreach (MessageType type in messageTypes.Keys)
                            {
                                switch (type)
                                {
                                    case MessageType.CONNECTED:
                                        Log.D("Timing.TimingController", "Timing system successfully connected.");
                                        timingSystemDict[sock].Status = SYSTEM_STATUS.CONNECTED;
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
                                        timingSystemDict[sock].SystemTime = messageTypes[MessageType.TIME].First();
                                        updateTiming = true;
                                        break;
                                    case MessageType.STATUS:
                                        Log.D("Timing.TimingController", "Status message received.");
                                        timingSystemDict[sock].SystemStatus = messageTypes[MessageType.STATUS].Last();
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
                        if (timingSystemDict.TryGetValue(sock, out TimingSystem? system))
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
                            timingSystemSockets.Remove(sock);
                            timingSystemDict.Remove(sock);
                            mainWindow.TimingSystemDisconnected(system);
                        }
                        else
                        {
                            Log.D("Timing.TimingController", "Successful disconnect.");
                        }
                    }
                }
                // Check Sockets we've started to connect to and verify that they've successfully connected.
                List<Socket> toRemove = [];
                try
                {
                    foreach (Socket sock in timingSystemSockets)
                    {
                        TimingSystem sys = timingSystemDict[sock];
                        if (sys.Status == SYSTEM_STATUS.CONNECTED || !sys.TimedOut()) continue; // Not connected & Timed out.
                        sys.Status = SYSTEM_STATUS.DISCONNECTED;
                        mainWindow.UpdateTimingFromController();
                        timingSystemDict.Remove(sock);
                        toRemove.Add(sock);
                    }
                }
                catch (Exception e)
                {
                    Log.E("Timing.TimingController", $"Something went wrong trying to remove a socket. {e.Message}");
                }
                timingSystemSockets.RemoveAll(toRemove.Contains);
                Log.D("Timing.TimingController", "Loop end.");
            }
            Log.D("Timing.TimingController", "Lock Wait 06");
            if (!TimingLock.TryEnter(6000)) return;
            try
            {
                running = false;
            }
            finally
            {
                TimingLock.Exit();
            }
        }
    }
}

