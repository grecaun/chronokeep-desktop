using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.Timing;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Chronokeep.Timing.Interfaces
{
    internal partial class IpicoInterface(IdbInterface database, int locationId, string type, IMainWindow window) : ITimingSystemInterface
    {
        private readonly Event theEvent = database.GetCurrentEvent()!;
        private readonly Dictionary<Socket, StringBuilder> bufferDict = [];
        private Socket? controlSocket;
        private Socket? streamSocket;
        private Socket? rewindSocket;
        private string ipadd = "";


        // private static readonly Regex voltage/connected/chipread/settinginfo/settingconfirmation/time/status/msg
        [GeneratedRegex(@"aa[0-9a-fA-F]{34,36}")]
        private static partial Regex ChipRead();
        [GeneratedRegex(@"date\.\w{3} \w{3} {1,2}\d{1,2} \d{2}:\d{2}:\d{2} \w{3} \d{4} *")]
        private static partial Regex Time();
        [GeneratedRegex(@"^[^\n]+\n")]
        private static partial Regex Msg();

        public List<Socket>? Connect(string ipAddress, int port)
        {
            this.ipadd = ipAddress;
            List<Socket> output = [];
            controlSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            streamSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            if (type != Constants.Readers.SYSTEM_IPICO_LITE)
            {
                try
                {
                    Log.D("Timing.Interfaces.IpicoInterface", $"Attempting to connect to {ipAddress}:{Constants.Readers.IPICO_CONTROL_PORT}");
                    IAsyncResult result = controlSocket.BeginConnect(ipAddress, Constants.Readers.IPICO_CONTROL_PORT, null, null);
                    result.AsyncWaitHandle.WaitOne(Constants.Readers.TIMEOUT, true);
                    if (controlSocket.Connected)
                    {
                        controlSocket.EndConnect(result);
                    }
                    else
                    {
                        controlSocket.Close();
                        throw new ApplicationException("Failed to connect to reader's control socket.");
                    }
                    output.Add(controlSocket);
                }
                catch
                {
                    Log.D("Timing.Interfaces.IpicoInterface", "Unable to connect to control socket...");
                    controlSocket = null;
                }
            }
            else
            {
                Log.D("Timing.Interfaces.IpicoInterface", "IPICO Lite Reader found.");
                controlSocket = null;
            }
            try
            {
                Log.D("Timing.Interfaces.IpicoInterface", $"Attempting to connect to {ipAddress}:{Constants.Readers.IPICO_CONTROL_PORT}");
                IAsyncResult result = streamSocket.BeginConnect(ipAddress, Constants.Readers.IPICO_DEFAULT_PORT, null, null);
                result.AsyncWaitHandle.WaitOne(Constants.Readers.TIMEOUT, true);
                if (streamSocket.Connected)
                {
                    streamSocket.EndConnect(result);
                }
                else
                {
                    streamSocket.Close();
                    throw new ApplicationException("Failed to connect to reader's stream socket.");
                }
                output.Add(streamSocket);
            }
            catch
            {
                Log.D("Timing.Interfaces.IpicoInterface", "Unable to connect to stream socket...");
                streamSocket = null;
            }
            if ((controlSocket == null && type != Constants.Readers.SYSTEM_IPICO_LITE) || streamSocket == null)
            {
                return null;
            }
            return output;
        }

        public Dictionary<MessageType, List<string>> ParseMessages(string inMessage, Socket? sock)
        {
            Log.D("Timing.Interfaces.IpicoInterface", "IpicoInterface -- parsing message.");
            Dictionary<MessageType, List<string>> output = [];
            if (sock == null)
            {
                return output;
            }
            if (!bufferDict.TryGetValue(sock, out StringBuilder? buff))
            {
                buff = new StringBuilder();
                bufferDict[sock] = buff;
            }

            buff.Append(inMessage);
            Log.D("Timing.Interfaces.IpicoInterface", $"IpicoInterface -- new message is '{inMessage}' with a length of {inMessage.Length}");
            Match m = Msg().Match(buff.ToString());
            HashSet<string> ignoredChips = [];
            foreach (BibChipAssociation ignore in database.GetBibChips(-1))
            {
                ignoredChips.Add(ignore.Chip);
            }
            List<ChipRead> chipReads = [];
            Log.D("Timing.Interfaces.IpicoInterface", "IpicoInterface -- matching all lines for messages");
            int count = 1;
            while (m.Success)
            {
                Log.D("Timing.Interfaces.IpicoInterface", $"IpicoInterface -- message {count}");
                count++;
                buff.Remove(m.Index, m.Length);
                string message = m.Value;
                Log.D("Timing.Interfaces.IpicoInterface", $"IpicoInterface -- message is : {message}");
                // a chipread is as follows: (note that milliseconds don't appear to be an actual millisecond but a hundredth of a second)
                // aa[ReaderId{2}][TagID{12}(Starts with 058)][ICount?{2}][QCount{2}][Date{yyMMdd}][Time{HHmmss}][Milliseconds{2}(Hex)][Checksum{2}][FS|LS]
                if (ChipRead().IsMatch(message))
                {
                    // Only add the chip if it isn't in the ignored list.
                    string chip = message.Substring(4, 12).Trim();
                    if (!ignoredChips.Contains(chip))
                    {
                        Log.D("Timing.Interfaces.IpicoInterface", "IpicoInterface -- chipread found");
                        DateTime time = DateTime.ParseExact(message.Substring(20, 12), "yyMMddHHmmss", CultureInfo.InvariantCulture);
                        int.TryParse(message.AsSpan(32, 2), NumberStyles.HexNumber, null, out int milliseconds);
                        milliseconds *= 10;
                        time = time.AddMilliseconds(milliseconds);
                        ChipRead chipRead = new(
                            theEvent.Identifier,
                            locationId,
                            chip,
                            time,
                            Convert.ToInt32(message.Substring(2, 2)),
                            message.Length == 36 ? 0 : 1
                            );
                        if (window != null && window.InDidNotStartMode())
                        {
                            chipRead.Status = Constants.Timing.CHIPREAD_STATUS_DNS;
                        }
                        chipReads.Add(chipRead);
                        output[MessageType.CHIPREAD] = [];
                    }
                }
                // Time is as follows:
                // date.Wed Nov 12 15:30:30 CST 2008
                else if (Time().IsMatch(message))
                {
                    Log.D("Timing.Interfaces.IpicoInterface", "IpicoInterface -- It's a time message.");
                    string month = message.Substring(9, 3);
                    string day = message.Substring(13, 2).Trim();
                    int dayVal = int.Parse(day);
                    string time = message.Substring(16, 8);
                    string year = message.Substring(29, 4);
                    string dateStr = $"{dayVal:D2} {month} {year}  {time}";
                    Log.D("Timing.Interfaces.IpicoInterface", $"IpicoInterface -- date string is {dateStr}");
                    output[MessageType.TIME] =
                    [
                        dateStr
                    ];
                }
                m = Msg().Match(buff.ToString());
            }
            Log.D("Timing.Interfaces.IpicoInterface", "IpicoInterface -- messages parsed successfully adding chipreads");
            if (chipReads.Count > 0)
            {
                database.AddChipReads(chipReads);
            }
            return output;
        }

        public void StartReading() { }

        public void StopReading() { }

        public void SetTime(DateTime date)
        {
            // setdate.YYMMDDHH:MM:SS
            SendMessage($"setdate.{DateTime.Now:yyMMddHH:mm:ss}");
        }

        public void GetTime()
        {
            SendMessage("getdate");
        }

        public void GetStatus() { }

        public void StartSending() { }

        public void StopSending() { }

        public void GetRewind()
        {
            Log.D("Timing.Interfaces.IpicoInterface", "IpicoInterface -- connecting to rewind socket");
            rewindSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                rewindSocket.Connect(ipadd, 10300);
            }
            catch
            {
                Log.D("Timing.Interfaces.IpicoInterface", "Upable to connect to rewind socket...");
                rewindSocket = null;
                return;
            }
            Log.D("Timing.Interfaces.IpicoInterface", "IpicoInterface -- looking for messages");
            byte[] recvd = new byte[4112];
            try
            {
                rewindSocket.Send(Encoding.ASCII.GetBytes("\n"));
                int numRecvd = rewindSocket.Receive(recvd);
                while (numRecvd > 0)
                {
                    string msg = Encoding.UTF8.GetString(recvd, 0, numRecvd);
                    Log.D("Timing.Interfaces.IpicoInterface", $"IpicoInterface -- Rewind -- message :{msg}");
                    ParseMessages(msg, rewindSocket);
                    numRecvd = rewindSocket.Receive(recvd);
                }
            }
            catch
            {
                Log.E("Timing.Interfaces.IpicoInterface", "Something went wrong with the rewind.");
            }
            finally
            {
                if (rewindSocket != null && bufferDict.TryGetValue(rewindSocket, out StringBuilder? oBuff))
                {
                    oBuff.Clear();
                }
                rewindSocket?.Close();
                rewindSocket = null;
            }
            Log.D("Timing.Interfaces.IpicoInterface", "IpicoInterface -- finished rewind");
        }

        public void Rewind(DateTime start, DateTime end, int reader = 1)
        {
            // yyMMddHHmmss
            if (reader == 1)
            {
                SendMessage($"replay_start file.ttyS0 port.10300 datetime.{start:yyMMddHHmmss}");
            }
            else
            {
                SendMessage($"replay_start file.ttyS1 port.10300 datetime.{start:yyMMddHHmmss}");
            }
        }

        public void Rewind(int from, int to, int reader = 1) { }

        public void Rewind(int reader = 1)
        {
            SendMessage(reader == 1 ? "replay_start file.ttyS0 port.10300" : "replay_start file.ttyS1 port.10300");
        }

        public void SetMainSocket(Socket iSock)
        {
            streamSocket = iSock;
        }

        public void SetSettingsSocket(Socket sock)
        {
            controlSocket = sock;
        }

        public void Disconnect() { }

        private void SendMessage(string msg)
        {
            Log.D("Timing.Interfaces.IpicoInterface", $"Sending message '{msg}'");
            controlSocket?.Send(Encoding.ASCII.GetBytes($"{msg}\n"));
        }

        public bool SettingsEditable()
        {
            return false;
        }

        public void OpenSettings() { }

        public void CloseSettings() { }

        public bool WasShutdown()
        {
            return false;
        }
    }
}
