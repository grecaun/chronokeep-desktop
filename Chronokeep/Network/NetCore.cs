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
                Log.D("Network.NetCore", "TCP Port is: " + i);
                return i;
            }
            return 0;
        }
    }
}