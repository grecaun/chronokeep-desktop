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
using Chronokeep.Timing.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Chronokeep.Constants;

namespace Chronokeep.Objects
{
    public class TimingSystem : IEquatable<TimingSystem>
    {
        public const string READING_STATUS_STOPPED = "STOPPED";
        public const string READING_STATUS_READING = "READING";
        public const string READING_STATUS_PARTIAL = "PARTIAL";
        public const string READING_STATUS_UNKNOWN = "UNKNOWN";

        public int SystemIdentifier { get; set; } = Constants.Timing.TIMINGSYSTEM_UNKNOWN;
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public int LocationId { get; set; } = Constants.Timing.LOCATION_FINISH;
        public string LocationName { get; set; } = "Unknown";
        public string Type { get; private set; }
        public SYSTEM_STATUS Status { get; set; }
        public List<Socket>? Sockets { get; private set; }
        public ITimingSystemInterface? SystemInterface;
        private DateTime connectedAt;

        public string SystemTime { get; set; } = "";
        public string SystemStatus { get; set; } = "";

        public TimingSystem(string ip, string type)
        {
            IpAddress = ip;
            Status = SYSTEM_STATUS.DISCONNECTED;
            Type = type;
            Port = type switch
            {
                Readers.SYSTEM_RFID => Readers.RFID_DEFAULT_PORT,
                Readers.SYSTEM_IPICO or Readers.SYSTEM_IPICO_LITE => Readers
                    .IPICO_DEFAULT_PORT,
                Readers.SYSTEM_CHRONOKEEP_PORTAL => Constants.Network.CHRONOKEEP_ZCONF_PORT,
                _ => Port
            };
        }

        public TimingSystem(string ip, int locId, string locName, SYSTEM_STATUS status, string type)
        {
            IpAddress = ip;
            LocationId = locId;
            LocationName = locName;
            Status = status;
            Type = type;
        }

        public TimingSystem(int sysId, string ip, int port, int location, string type)
        {
            SystemIdentifier = sysId;
            IpAddress = ip;
            Port = port;
            Type = type;
            LocationId = location;
            Status = SYSTEM_STATUS.DISCONNECTED;
        }

        public List<Socket>? Connect()
        {
            if (SystemInterface == null)
            {
                return null;
            }
            Log.D("Objects.TimingSystem", "TimingSystem class calling connect on interface.");
            Sockets = SystemInterface.Connect(IpAddress, Port);
            Log.D("Objects.TimingSystem", "TimingSystem class returning output from Connect.");
            return Sockets;
        }

        public void Disconnect()
        {
            SystemInterface?.Disconnect();
            if (Sockets == null) return;
            foreach (Socket sock in Sockets)
            {
                sock.Disconnect(false);
            }
        }

        public void UpdateSystemType(string type)
        {
            Type = type;
            Port = type switch
            {
                Readers.SYSTEM_RFID => Readers.RFID_DEFAULT_PORT,
                Readers.SYSTEM_IPICO or Readers.SYSTEM_IPICO_LITE => Readers.IPICO_DEFAULT_PORT,
                Readers.SYSTEM_CHRONOKEEP_PORTAL => Constants.Network.CHRONOKEEP_ZCONF_PORT,
                _ => Port
            };
        }

        public void CopyFrom(TimingSystem other)
        {
            IpAddress = other.IpAddress;
            LocationId = other.LocationId;
            LocationName = other.LocationName;
            Port = other.Port;
            Type = other.Type;
        }

        public void CreateTimingSystemInterface(IdbInterface database, IMainWindow window)
        {
            switch (Type)
            {
                case Readers.SYSTEM_RFID:
                    Log.D("Objects.TimingSystem", "System interface is RFID.");
                    SystemInterface = new RfidUltraInterface(database, LocationId, window);
                    break;
                case Readers.SYSTEM_IPICO:
                case Readers.SYSTEM_IPICO_LITE:
                    Log.D("Objects.TimingSystem", "System interface is IPICO.");
                    SystemInterface = new IpicoInterface(database, LocationId, Type, window);
                    break;
                case Readers.SYSTEM_CHRONOKEEP_PORTAL:
                    Log.D("Objects.TimingSystem", "System interface is CHRONOKEEP_PORTAL.");
                    SystemInterface = new ChronokeepInterface(database, LocationId, window);
                    break;
                default:
                    Log.E("Objects.TimingSystem", "Unknown interface selected.");
                    SystemInterface = null;
                    break;
            }
        }

        public void SetLastCommunicationTime()
        {
            connectedAt = DateTime.Now;
        }

        public bool TimedOut()
        {
            TimeSpan ellapsed = DateTime.Now - connectedAt;
            return ellapsed.Seconds > 5;
        }

        public bool Equals(TimingSystem? other)
        {
            return other != null && IpAddress.Trim().Equals(other.IpAddress.Trim()) && Port == other.Port
                && LocationId == other.LocationId && Type.Equals(other.Type);
        }

        public bool Saved()
        {
            return SystemIdentifier != Constants.Timing.TIMINGSYSTEM_UNKNOWN;
        }
    }

    public enum SYSTEM_STATUS { CONNECTED, WORKING, DISCONNECTED }
}

