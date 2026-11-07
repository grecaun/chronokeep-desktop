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
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Chronokeep.Network
{
    internal class ZeroConf
    {
        private bool keepAlive = true;
        private UdpClient? udpClient;
        private readonly string servername;
        private static string? serverid;

        private static bool running;
        private readonly IPAddress? listenAddress;
        private readonly int port;

        /// <summary>
        /// Answers discovery requests from the Chronokeep apps with this computer's name and registration port.
        /// </summary>
        /// <param name="name">The server name to give the apps.</param>
        /// <param name="listenAddress">The address to listen on. Defaults to every address, which also joins the discovery multicast group.</param>
        /// <param name="port">The port to listen on. Defaults to the discovery port.</param>
        public ZeroConf(string? name, IPAddress? listenAddress = null, int port = Constants.Network.CHRONOKEEP_ZCONF_PORT)
        {
            this.listenAddress = listenAddress;
            this.port = port;
            char[] chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();
            char[] serveridChars = new char[10];
            Random rng = new();
            for (int i = 0; i < serveridChars.Length; i++)
            {
                serveridChars[i] = chars[rng.Next(0, chars.Length)];
            }
            servername = name ?? Constants.Network.DEFAULT_CHRONOKEEP_SERVER_NAME;
            serverid = new string(serveridChars);
            Log.D("Network.ZeroConf", $"Server name is {servername} and has an id of {serverid}.");
        }

        public void Run()
        {
            running = true;
            udpClient = new UdpClient();
            IPEndPoint endPoint = new(listenAddress ?? IPAddress.Any, port);
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            // Only join the multicast group when listening everywhere, a single address (like 127.0.0.1) can't receive it.
            if (null == listenAddress)
            {
                IPAddress multicastAddr = IPAddress.Parse(Constants.Network.CHRONOKEEP_ZCONF_MULTICAST_IP);
                udpClient.JoinMulticastGroup(multicastAddr);
            }
            udpClient.Client.Bind(endPoint);

            int counter = 0;
            while (keepAlive)
            {
                Log.D("Network.ZeroConf", $"{counter} clients have contacted me.");
                counter++;
                try
                {
                    byte[] receiveByteArray = udpClient.Receive(ref endPoint);
                    string receivedData = Encoding.UTF8.GetString(receiveByteArray, 0, receiveByteArray.Length).Trim();
                    Log.D("Network.ZeroConf", $"Received broadcast from '{endPoint}' with data '{receivedData}'");
                    if (receivedData.Equals(Constants.Network.CHRONOKEEP_ZCONF_CONNECT_MSG, StringComparison.OrdinalIgnoreCase))
                    {
                        string outString = $"[{servername}|{serverid}|{NetCore.GetTcpPort()}]";
                        byte[] outData = Encoding.UTF8.GetBytes(outString);
                        Log.D("Network.ZeroConf", $"Sending '{outString}'");
                        udpClient.Send(outData, outData.Length, endPoint);
                    }
                }
                catch (Exception e)
                {
                    // Stop() closes the socket to end the wait for a message, that's not an error.
                    if (keepAlive)
                    {
                        Log.E("Network.ZeroConf", $"Error receiving a discovery request. {e.Message}");
                    }
                }
            }
            running = false;
        }

        public void Stop()
        {
            Log.D("Network.ZeroConf", "Zero Conf is instructed to stop.");
            keepAlive = false;
            udpClient?.Close();
        }

        public static bool IsRunning() => running;
    }
}
