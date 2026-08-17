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

using Chronokeep.Helpers;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;

namespace Chronokeep.Network
{
    internal static class NetCore
    {
        private static readonly int TcpPort = GetAvailableTcpPort(4488, 5588);

        public static int GetTcpPort()
        {
            return TcpPort;
        }

        private static int GetAvailableTcpPort(int start, int end)
        {
            Log.D("Network.NetCore", "Getting TCP Port.");
            List<int> portArray = [];
            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] connections = properties.GetActiveTcpConnections();
            portArray.AddRange(from n in connections
                               where n.LocalEndPoint.Port >= start && n.LocalEndPoint.Port <= end
                               select n.LocalEndPoint.Port);
            System.Net.IPEndPoint[] endPoints = properties.GetActiveTcpListeners();
            portArray.AddRange(from n in endPoints
                               where n.Port >= start && n.Port <= end
                               select n.Port);
            portArray.Sort();
            for (int i = start; i <= end; i++)
            {
                if (portArray.Contains(i)) continue;
                Log.D("Network.NetCore", $"TCP Port is: {i}");
                return i;
            }
            return 0;
        }
    }
}
