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

        public ZeroConf(string? name)
        {
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
            IPEndPoint endPoint = new(IPAddress.Any, Constants.Network.CHRONOKEEP_ZCONF_PORT);
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            IPAddress multicastAddr = IPAddress.Parse(Constants.Network.CHRONOKEEP_ZCONF_MULTICAST_IP);
            udpClient.JoinMulticastGroup(multicastAddr);
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
                catch
                {
                    Log.E("Network.ZeroConf", "Exception thrown - Shutting down.");
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