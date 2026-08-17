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
using Chronokeep.Objects.RFID;
using Chronokeep.UI.Timing.ReaderSettings;
using Chronokeep.UI.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Chronokeep.Timing.Interfaces
{
    public partial class RfidUltraInterface(IdbInterface database, int locationId, IMainWindow window) : ITimingSystemInterface
    {
        private readonly Event theEvent = database.GetCurrentEvent()!;
        private readonly StringBuilder buffer = new();
        private RfidSettings? settingsWindow;
        private Socket? sock;

        [GeneratedRegex(@"^V=.*")]
        private static partial Regex Voltage();
        [GeneratedRegex(@"^Connected,.*")]
        private static partial Regex Connected();
        [GeneratedRegex(@"^0,.*")]
        private static partial Regex ChipRead();
        [GeneratedRegex(@"^U.*")]
        private static partial Regex SettingInfo();
        [GeneratedRegex(@"^u.*")]
        private static partial Regex SettingConfirmation();
        [GeneratedRegex(@"^(\d{1,2}:\d{1,2}:\d{1,2} \d{1,2}-\d{1,2}-\d{4}) \(\d*\)")]
        private static partial Regex Time();
        [GeneratedRegex(@"^S=(\d)(\d)")]
        private static partial Regex Status();
        [GeneratedRegex(@"^[^\n]*\n")]
        private static partial Regex Msg();

        public List<Socket>? Connect(string ipAddress, int port)
        {
            List<Socket> output = [];
            sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Log.D("Timing.Interfaces.RFIDUltraInterface", $"Attempting to connect to {ipAddress}:{port}");
            try
            {
                IAsyncResult result = sock.BeginConnect(ipAddress, port, null, null);
                result.AsyncWaitHandle.WaitOne(Constants.Readers.TIMEOUT, true);
                if (sock.Connected)
                {
                    sock.EndConnect(result);
                }
                else
                {
                    sock.Close();
                    throw new ApplicationException("Failed to connect to reader.");
                }
                output.Add(sock);
            }
            catch
            {
                Log.D("Timing.Interfaces.RFIDUltraInterface", "Unable to connect.");
                return null;
            }
            Log.D("Timing.Interfaces.RFIDUltraInterface", "connected. Returning socket.");
            // Query current status of the reader
            GetStatus();
            return output;
        }

        public Dictionary<MessageType, List<string>> ParseMessages(string inMessage, Socket iSock)
        {
            Dictionary<MessageType, List<string>> output = [];
            buffer.Append(inMessage);
            Match m = Msg().Match(buffer.ToString());
            HashSet<string> ignoredChips = [];
            foreach (BibChipAssociation ignore in database.GetBibChips(-1))
            {
                ignoredChips.Add(ignore.Chip);
            }
            List<ChipRead> chipReads = [];
            RfidSettingsHolder? settingsHolder = null;
            while (m.Success)
            {
                buffer.Remove(m.Index, m.Length);
                string message = m.Value;
                // all incoming messages are terminated by a linefeed character (0x0A)
                // If "0,[...]" Chip read
                if (ChipRead().IsMatch(message))
                {
                    // Only add a chip read if it isn't on the ignore list.
                    string[] chipVals = message.Split(',');
                    string chip = chipVals[1].Trim();
                    if (!ignoredChips.Contains(chip))
                    {
                        ChipRead chipRead = new(
                            theEvent.Identifier,
                            locationId,
                            chip,
                            long.Parse(chipVals[2]),
                            int.Parse(chipVals[3]),
                            int.Parse(chipVals[4]),
                            chipVals[5],
                            int.Parse(chipVals[6]),
                            chipVals[7],
                            chipVals[8],
                            chipVals[9],
                            long.Parse(chipVals[10]),
                            int.Parse(chipVals[11])
                        );
                        if (window != null && window.InDidNotStartMode())
                        {
                            chipRead.Status = Constants.Timing.CHIPREAD_STATUS_DNS;
                        }
                        chipReads.Add(chipRead);
                        // we don't need to do anything other than notify of a chip read
                        output[MessageType.CHIPREAD] = [];
                    }
                }
                // If "V=" then it's a voltage status.
                else if (Voltage().IsMatch(message))
                {
                    double voltVal = 0;
                    try
                    {
                        voltVal = double.Parse(message[2..]);
                    }
                    catch
                    {
                        if (!output.TryGetValue(MessageType.ERROR, out List<string>? errorList))
                        {
                            errorList = [];
                            output[MessageType.ERROR] = errorList;
                        }
                        errorList.Add("Invalid voltage value given.");
                    }
                    if (voltVal != 0 && voltVal < 23)
                    {
                        // Voltage low and normal don't require anything else.
                        output[MessageType.VOLTAGELOW] = [];
                    }
                    else
                    {
                        // Voltage low and normal don't require anything else.
                        output[MessageType.VOLTAGENORMAL] = [];
                    }
                }
                // If "U[...]" Setting information
                else if (SettingInfo().IsMatch(message))
                {
                    Log.D("Timing.Interfaces.RFIDUltraInterface", $"It's a setting information message. {message}");
                    settingsHolder ??= new RfidSettingsHolder();
                    char settingId = message[1];
                    string subMsg = message[2..^1];
                    int tmp;
                    switch (settingId)
                    {
                        case RfidUltraCodes.ULTRA_ID:
                            Log.D("Timing.Interfaces.RFIDUltraInterface", $"Ultra ID: {subMsg}");
                            if (int.TryParse(subMsg, out tmp))
                            {
                                settingsHolder.UltraId = tmp;
                            }
                            break;
                        case RfidUltraCodes.CHIP_OUT_TYPE:
                            Log.D("Timing.Interfaces.RFIDUltraInterface", $"Chip out type: {message[2]}");
                            settingsHolder.ChipType = message[2] switch
                            {
                                '0' => RfidSettingsHolder.ChipTypeEnum.DEC,
                                '1' => RfidSettingsHolder.ChipTypeEnum.HEX,
                                _ => RfidSettingsHolder.ChipTypeEnum.UNKNOWN,
                            };
                            break;
                        case RfidUltraCodes.GATING_MODE:
                            Log.D("Timing.Interfaces.RFIDUltraInterface", $"Gating Mode: {message[2]}");
                            settingsHolder.GatingMode = message[2] switch
                            {
                                '0' => RfidSettingsHolder.GatingModeEnum.PER_READER,
                                '1' => RfidSettingsHolder.GatingModeEnum.PER_BOX,
                                '2' => RfidSettingsHolder.GatingModeEnum.FIRST_TIME_SEEN,
                                _ => RfidSettingsHolder.GatingModeEnum.UNKNOWN,
                            };
                            break;
                        case RfidUltraCodes.GATING_INTERVAL:
                            Log.D("Timing.Interfaces.RFIDUltraInterface", $"Gating Interval: {subMsg}");
                            if (int.TryParse(subMsg, out tmp))
                            {
                                settingsHolder.GatingInterval = tmp;
                            }
                            break;
                        case RfidUltraCodes.WHEN_BEEP:
                            Log.D("Timing.Interfaces.RFIDUltraInterface", $"When beep: {message[2]}");
                            settingsHolder.Beep = message[2] switch
                            {
                                '0' => RfidSettingsHolder.BeepEnum.ALWAYS,
                                '1' => RfidSettingsHolder.BeepEnum.ONLY_FIRST_SEEN,
                                _ => RfidSettingsHolder.BeepEnum.UNKNOWN,
                            };
                            break;
                        case RfidUltraCodes.BEEPER_VOLUME:
                            Log.D("Timing.Interfaces.RFIDUltraInterface", $"Beeper volume: {message[2]}");
                            settingsHolder.BeepVolume = message[2] switch
                            {
                                '0' => RfidSettingsHolder.BeepVolumeEnum.OFF,
                                '1' => RfidSettingsHolder.BeepVolumeEnum.SOFT,
                                '2' => RfidSettingsHolder.BeepVolumeEnum.LOUD,
                                _ => RfidSettingsHolder.BeepVolumeEnum.UNKNOWN,
                            };
                            break;
                        case RfidUltraCodes.AUTO_SET_GPS:
                            Log.D("Timing.Interfaces.RFIDUltraInterface", $"Auto set gps: {message[2]}");
                            settingsHolder.SetFromGps = message[2] switch
                            {
                                '0' => RfidSettingsHolder.GpsEnum.DONT_SET,
                                '1' => RfidSettingsHolder.GpsEnum.SET,
                                _ => RfidSettingsHolder.GpsEnum.UNKNOWN,
                            };
                            break;
                        case RfidUltraCodes.TIME_ZONE:
                            Log.D("Timing.Interfaces.RFIDUltraInterface", $"Timezone: {subMsg}");
                            if (int.TryParse(subMsg, out tmp))
                            {
                                settingsHolder.TimeZone = tmp;
                            }
                            break;
                    }
                    output[MessageType.SETTINGVALUE] = [];
                }
                // If "u[...]" setting changed
                else if (SettingConfirmation().IsMatch(message))
                {
                    Log.D("Timing.Interfaces.RFIDUltraInterface", $"It's a settings confirmation message. {message}{BitConverter.ToString([.. message.Select(c => (byte)c)])}");
                    settingsHolder ??= new RfidSettingsHolder();
                    char settingId = message[1];
                    switch (settingId)
                    {
                        case RfidUltraCodes.ULTRA_ID:
                            Log.D("Timing.Interfaces.RFIDUltraInterface", "Ultra ID set");
                            if (message[2] != (char)0x02)
                            {
                                Log.E("Timing.Interfaces.RFIDUltraInterface", "Setting not saved.");
                            }
                            break;
                        case RfidUltraCodes.CHIP_OUT_TYPE:
                            Log.D("Timing.Interfaces.RFIDUltraInterface", "Chip out type set");
                            if (message[2] != (char)0x02)
                            {
                                Log.E("Timing.Interfaces.RFIDUltraInterface", "Setting not saved.");
                            }
                            break;
                        case RfidUltraCodes.GATING_MODE:
                            Log.D("Timing.Interfaces.RFIDUltraInterface", "Gating mode set");
                            if (message[2] != (char)0x02)
                            {
                                Log.E("Timing.Interfaces.RFIDUltraInterface", "Setting not saved.");
                            }
                            break;
                        case RfidUltraCodes.GATING_INTERVAL:
                            Log.D("Timing.Interfaces.RFIDUltraInterface", "Gating Interval set");
                            if (message[2] != (char)0x02)
                            {
                                Log.E("Timing.Interfaces.RFIDUltraInterface", "Setting not saved.");
                            }
                            break;
                        case RfidUltraCodes.WHEN_BEEP:
                            Log.D("Timing.Interfaces.RFIDUltraInterface", "When to beep set");
                            if (message[2] != (char)0x02)
                            {
                                Log.E("Timing.Interfaces.RFIDUltraInterface", "Setting not saved.");
                            }
                            break;
                        case RfidUltraCodes.BEEPER_VOLUME:
                            Log.D("Timing.Interfaces.RFIDUltraInterface", "Beeper volume set");
                            if (message[2] != (char)0x02)
                            {
                                Log.E("Timing.Interfaces.RFIDUltraInterface", "Setting not saved.");
                            }
                            break;
                        case RfidUltraCodes.AUTO_SET_GPS:
                            Log.D("Timing.Interfaces.RFIDUltraInterface", "Set time via gps set");
                            if (message[2] != (char)0x02)
                            {
                                Log.E("Timing.Interfaces.RFIDUltraInterface", "Setting not saved.");
                            }
                            break;
                        case RfidUltraCodes.TIME_ZONE:
                            Log.D("Timing.Interfaces.RFIDUltraInterface", "Timezone set");
                            if (message[2] != (char)0x02)
                            {
                                Log.E("Timing.Interfaces.RFIDUltraInterface", "Setting not saved.");
                            }
                            break;
                    }
                    output[MessageType.SETTINGCHANGE] = [];
                }
                // If "HH:MM:SS DD-MM-YYYY" then it's a time message
                else if (Time().IsMatch(message))
                {
                    Log.D("Timing.Interfaces.RFIDUltraInterface", "It's a time message.");
                    Match match = Time().Match(message);
                    if (!output.TryGetValue(MessageType.TIME, out List<string>? timeList))
                    {
                        timeList = ([]);
                        output[MessageType.TIME] = timeList;
                    }

                    timeList.Clear();
                    DateTime timeDt = DateTime.ParseExact(match.Groups[1].Value, "H:m:s d-M-yyyy", CultureInfo.CurrentCulture);
                    timeList.Add(timeDt.ToString("dd MMM yyyy HH:mm:ss"));
                }
                // If "S=[...]" then status
                else if (Status().IsMatch(message))
                {
                    Log.D("Timing.Interfaces.RFIDUltraInterface", "It's a status message.");
                    settingsHolder ??= new RfidSettingsHolder();
                    Match match = Status().Match(message);
                    if (!output.TryGetValue(MessageType.STATUS, out List<string>? statusList))
                    {
                        statusList = ([]);
                        output[MessageType.STATUS] = statusList;
                    }
                    switch (Convert.ToInt32(match.Groups[1].Value))
                    {
                        case 0:
                            statusList.Add(TimingSystem.READING_STATUS_STOPPED);
                            settingsHolder.Status = RfidSettingsHolder.StatusEnum.STOPPED;
                            break;
                        case 1:
                            statusList.Add(TimingSystem.READING_STATUS_READING);
                            settingsHolder.Status = RfidSettingsHolder.StatusEnum.STARTED;
                            break;
                        default:
                            statusList.Add(TimingSystem.READING_STATUS_UNKNOWN);
                            settingsHolder.Status = RfidSettingsHolder.StatusEnum.UNKNOWN;
                            break;
                    }
                }
                // If "Connected,[LastTimeSent]" that's a connection successful message.
                else if (Connected().IsMatch(message))
                {
                    // Nothing other than connected care about for right now.
                    output[MessageType.CONNECTED] = [];
                }
                else
                {
                    output[MessageType.UNKNOWN] = [];
                }
                m = Msg().Match(buffer.ToString());
            }
            if (chipReads.Count > 0)
            {
                database.AddChipReads(chipReads);
            }
            if (settingsHolder != null && settingsWindow != null)
            {
                settingsWindow.UpdateView(settingsHolder);
            }
            return output;
        }

        public void StartReading()
        {
            SendMessage("R");
        }

        public void StopReading()
        {
            SendMessage("S");
        }

        public void Rewind(DateTime start, DateTime end, int reader = 1)
        {
            SendMessage($"800{Constants.Timing.RfidDateToEpoch(start)}{RfidUltraCodes.REWIND_DELIMITER}{Constants.Timing.RfidDateToEpoch(end)}");
        }

        public void Rewind(int reader = 1)
        {
            SendMessage($"8000{RfidUltraCodes.REWIND_DELIMITER}0");
        }

        public void Rewind(int start, int end, int reader = 1)
        {
            if (start < 1)
            {
                start = 1;
            }
            SendMessage($"600{start}{RfidUltraCodes.REWIND_DELIMITER}{end}");
        }

        public void StopRewind()
        {
            SendMessage("9");
        }

        public void SetTime(DateTime date)
        {
            SendMessage($"t{RfidUltraCodes.SET_TIME}{date:HH:mm:ss dd-MM-yyyy}");
        }

        public void SetTime()
        {
            SendMessage($"t{RfidUltraCodes.SET_TIME}{DateTime.Now:HH:mm:ss dd-MM-yyyy}");
        }

        public void GetTime()
        {
            SendMessage("r");
        }

        public void GetStatus()
        {
            SendMessage("?");
        }

        public void StartSending()
        {
            SendMessage("700");
        }

        public void StartSending(DateTime date)
        {
            SendMessage($"700{Constants.Timing.RfidDateToEpoch(date)}");
        }

        public void StopSending()
        {
            SendMessage("s");
        }

        public void Disconnect() { }

        /**
         * Changing settings on the Ultra 
         */
        public void SetGprs(bool turnOn)
        {
            SendMessage($"u{RfidUltraCodes.GPRS}{(turnOn ? "1" : "0")}{RfidUltraCodes.SETTINGS_TERM}");
        }

        public void SetGprsIp(string address)
        {
            string[] nums = address.Split('.');
            if (nums.Length != 4)
            {
                return;
            }
            char[] vals = new char[4];
            for (int i = 0; i < 4; i++)
            {
                vals[i] = (char)Convert.ToByte(nums[i]);
            }
            SendMessage($"u{RfidUltraCodes.GPRS_IP}{vals[0]}{vals[1]}{vals[2]}{vals[3]}{RfidUltraCodes.SETTINGS_TERM}");
        }

        public void SetGprsPort(int port)
        {
            SendMessage($"u{RfidUltraCodes.GPRS_PORT}{port}{RfidUltraCodes.SETTINGS_TERM}");
        }

        public void SetApnName(string name)
        {
            SendMessage($"u{RfidUltraCodes.APN_NAME}{name}{RfidUltraCodes.SETTINGS_TERM}");
        }

        public void SetApnUserName(string name)
        {
            SendMessage($"u{RfidUltraCodes.APN_USER}{name}{RfidUltraCodes.SETTINGS_TERM}");
        }

        public void SetApnPassword(string name)
        {
            SendMessage($"u{RfidUltraCodes.APN_PASS}{name}{RfidUltraCodes.SETTINGS_TERM}");
        }

        /**
         * 0x00 - FCC
         * 0x01 - ETSI EN 300-220
         * 0x02 - ETSI EN 302-208
         * 0x03 - Australia, New Zealand, Hong Kong
         * 0x04 - Taiwan
         * 0x05 - Japan
         * 0x06 - Japan (Max 10mW power)
         * 0x07 - ETSI EN 302-208
         * 0x08 - Korea
         * 0x09 - Malaysia
         * 0x0A - China
         */
        public void SetRegion(char regionCode)
        {
            SendMessage($"u{RfidUltraCodes.REGION}{regionCode}{RfidUltraCodes.SETTINGS_TERM}");
        }

        /**
         * 0x00 - MACH1
         * 0x01 - LLRP
         */
        public void SetComProtocol(char protocol)
        {
            SendMessage($"u{RfidUltraCodes.COM_PROTO}{protocol}{RfidUltraCodes.SETTINGS_TERM}");
        }

        /**
         * 0x00 - Decimal
         * 0x01 - Hexadecimal
         */
        public void SetChipOutputType(char type)
        {
            SendMessage($"u{RfidUltraCodes.CHIP_OUT_TYPE}{type}{RfidUltraCodes.SETTINGS_TERM}");
        }

        /**
         * 0x00 - Off
         * 0x01 - On
         */
        public void SetAntennaStatus(int readerNo, int antennaNo, char status)
        {
            char code;
            switch (readerNo)
            {
                case 1:
                    switch (antennaNo)
                    {
                        case 1:
                            code = RfidUltraCodes.READ1_ANT1;
                            break;
                        case 2:
                            code = RfidUltraCodes.READ1_ANT2;
                            break;
                        case 3:
                            code = RfidUltraCodes.READ1_ANT3;
                            break;
                        case 4:
                            code = RfidUltraCodes.READ1_ANT4;
                            break;
                        default:
                            return;
                    }

                    break;
                case 2:
                    switch (antennaNo)
                    {
                        case 1:
                            code = RfidUltraCodes.READ2_ANT1;
                            break;
                        case 2:
                            code = RfidUltraCodes.READ2_ANT2;
                            break;
                        case 3:
                            code = RfidUltraCodes.READ2_ANT3;
                            break;
                        case 4:
                            code = RfidUltraCodes.READ2_ANT4;
                            break;
                        default:
                            return;
                    }

                    break;
                default:
                    return;
            }
            SendMessage($"u{code}{status}{RfidUltraCodes.SETTINGS_TERM}");
        }

        /**
         * 0x00 - Start 
         * 0x01 - Desktop
         * 0x02 - Raw
         * 0x03 - Finish
         * 0x04 - MTB Downhill
         */
        public void SetReaderMode(int readerNo, char mode)
        {
            char code;
            switch (readerNo)
            {
                case 1:
                    code = RfidUltraCodes.READ1_MODE;
                    break;
                case 2:
                    code = RfidUltraCodes.READ2_MODE;
                    break;
                default:
                    return;
            }
            SendMessage($"u{code}{mode}{RfidUltraCodes.SETTINGS_TERM}");
        }

        /**
         * 0x00 - Session 0
         * 0x01 - Session 1
         * 0x02 - Session 2
         * 0x03 - Session 3
         */
        public void SetReaderSession(int readerNo, char session)
        {
            char code;
            switch (readerNo)
            {
                case 1:
                    code = RfidUltraCodes.READ1_SESSION;
                    break;
                case 2:
                    code = RfidUltraCodes.READ2_SESSION;
                    break;
                default:
                    return;
            }
            SendMessage($"u{code}{session}{RfidUltraCodes.SETTINGS_TERM}");
        }

        /**
         * Max Power of 30?
         */
        public void SetReaderPower(int readerNo, int power)
        {
            char code;
            switch (readerNo)
            {
                case 1:
                    code = RfidUltraCodes.READ1_POWER;
                    break;
                case 2:
                    code = RfidUltraCodes.READ2_POWER;
                    break;
                default:
                    return;
            }
            if (power > 30)
            {
                power = 30;
            }
            SendMessage($"u{code}{power}{RfidUltraCodes.SETTINGS_TERM}");
        }

        public void SetReaderIp(int readerNo, string address)
        {
            string[] nums = address.Split('.');
            if (nums.Length != 4)
            {
                return;
            }
            char[] vals = new char[4];
            for (int i = 0; i < 4; i++)
            {
                vals[i] = (char)Convert.ToByte(nums[i]);
            }
            char code;
            switch (readerNo)
            {
                case 1:
                    code = RfidUltraCodes.READ1_IP;
                    break;
                case 2:
                    code = RfidUltraCodes.READ2_IP;
                    break;
                default:
                    return;
            }
            SendMessage($"u{code}{vals[0]}{vals[1]}{vals[2]}{vals[3]}{RfidUltraCodes.SETTINGS_TERM}");
        }

        /**
         * 0x00 - Per reader
         * 0x01 - Per box
         * 0x02 - First time seen
         */
        public void SetGatingMode(char mode)
        {
            SendMessage($"u{RfidUltraCodes.GATING_MODE}{mode}{RfidUltraCodes.SETTINGS_TERM}");
        }

        /**
         * Largest value accepted is 20 seconds.
         */
        public void SetGatingInterval(int seconds)
        {
            SendMessage($"u{RfidUltraCodes.GATING_INTERVAL}{seconds}{RfidUltraCodes.SETTINGS_TERM}");
        }

        /**
         * 0x00 - Channel A
         * 0x01 - Channel B
         * 0x02 - Auto
         */
        public void SetChannelNumber(char number)
        {
            SendMessage($"u{RfidUltraCodes.GATING_INTERVAL}{number}{RfidUltraCodes.SETTINGS_TERM}");
        }

        /**
         * 0x00 - Off
         * 0x01 - Soft
         * 0x02 - Loud
         */
        public void SetBeeperVolume(char vol)
        {
            SendMessage($"u{RfidUltraCodes.BEEPER_VOLUME}{vol}{RfidUltraCodes.SETTINGS_TERM}");
        }

        /**
         * 0x00 - Don't set using GPS
         * 0x01 - Set using GPS
         * 0x02 - Loud ? (Probably an error in documentation...)
         */
        public void SetAutoGpsTime(char gps)
        {
            SendMessage($"u{RfidUltraCodes.AUTO_SET_GPS}{gps}{RfidUltraCodes.SETTINGS_TERM}");
        }

        /**
         * Valid values are -23 to 23
         */
        public void SetTimeZone(int zone)
        {
            if (zone is > 23 or < -23)
            {
                return;
            }
            SendMessage($"u{RfidUltraCodes.TIME_ZONE}{zone}{RfidUltraCodes.SETTINGS_TERM}");
        }

        /**
         * 0x00 - Always send
         * 0x01 - Send only when requested
         */
        public void SetDataSending(char value)
        {
            SendMessage($"u{RfidUltraCodes.DATA_SENDING}{value}{RfidUltraCodes.SETTINGS_TERM}");
        }

        /**
         * ID can be any value from 1 to 255.
         */
        public void SetUltraId(int id)
        {
            if (id is > 255 or < 1)
            {
                return;
            }
            SendMessage($"u{RfidUltraCodes.ULTRA_ID}{id}{RfidUltraCodes.SETTINGS_TERM}");
        }

        /**
         * 0x00 - Off
         * 0x01 - On
         */
        public void SetAntenna4Backup(int readerNo, bool value)
        {
            char code;
            switch (readerNo)
            {
                case 1:
                    code = RfidUltraCodes.READ1_ANTENNA4_BACKUP;
                    break;
                case 2:
                    code = RfidUltraCodes.READ2_ANTENNA4_BACKUP;
                    break;
                default:
                    return;
            }
            SendMessage($"u{code}{(value ? 0x01 : 0x00)}{RfidUltraCodes.SETTINGS_TERM}");
        }

        /**
         * 0x00 - beep always
         * 0x01 - beep when first seen
         */
        public void SetWhenToBeep(char value)
        {
            SendMessage($"u{RfidUltraCodes.WHEN_BEEP}{value}{RfidUltraCodes.SETTINGS_TERM}");
        }

        public void SetUploadUrl(string url)
        {
            SendMessage($"u{RfidUltraCodes.UPLOAD_URL}{url}{RfidUltraCodes.SETTINGS_TERM}");
        }

        public void SetGateway(string gateway)
        {
            string[] nums = gateway.Split('.');
            if (nums.Length != 4)
            {
                return;
            }
            char[] vals = new char[4];
            for (int i = 0; i < 4; i++)
            {
                vals[i] = (char)int.Parse(nums[i]);
            }
            SendMessage($"u{RfidUltraCodes.GATEWAY}{vals[0]}{vals[1]}{vals[2]}{vals[3]}{RfidUltraCodes.SETTINGS_TERM}");
        }

        public void SetDnsServer(string server)
        {
            string[] nums = server.Split('.');
            if (nums.Length != 4)
            {
                return;
            }
            char[] vals = new char[4];
            for (int i = 0; i < 4; i++)
            {
                vals[i] = (char)int.Parse(nums[i]);
            }
            SendMessage($"u{RfidUltraCodes.DNS_SERVER}{vals[0]}{vals[1]}{vals[2]}{vals[3]}{RfidUltraCodes.SETTINGS_TERM}");
        }

        public void SaveSettings()
        {
            SendMessage($"u{RfidUltraCodes.SETTINGS_TERM}");
        }

        public void QuerySettings()
        {
            SendMessage("U");
        }

        private void SendMessage(string msg)
        {
            Log.D("Timing.Interfaces.RFIDUltraInterface", $"Sending message '{msg}'");
            sock!.Send(Encoding.ASCII.GetBytes($"{msg}\n"));
        }

        public void SetMainSocket(Socket iSock)
        {
            sock = iSock;
        }

        public void SetSettingsSocket(Socket iSock)
        {
        }

        public bool SettingsEditable()
        {
            return true;
        }

        public void OpenSettings()
        {
            if (settingsWindow != null)
            {
                DialogBox.AsyncShow("Settings window already open.");
                return;
            }
            settingsWindow = new RfidSettings(this);
            window.AddWindow(settingsWindow);
            settingsWindow.Show();
        }

        public void SettingsWindowFinalize()
        {
            window.WindowFinalize();
            settingsWindow = null;
        }

        public void CloseSettings()
        {
            settingsWindow?.CloseWindow();
        }

        public bool WasShutdown()
        {
            return false;
        }

        public enum RfidMessage
        {
            CONNECTED, VOLTAGE_NORMAL, VOLTAGE_LOW, CHIPREAD, TIME, SETTING_VALUE, SETTING_CHANGE, STATUS, UNKNOWN, ERROR
        }
    }

    public static class RfidUltraCodes
    {
        public const char SETTINGS_TERM = (char)0xFF;
        public const char GPRS = (char)0x01;
        public const char GPRS_IP = (char)0x02;
        public const char GPRS_PORT = (char)0x03;
        public const char APN_NAME = (char)0x04;
        public const char APN_USER = (char)0x05;
        public const char APN_PASS = (char)0x06;
        public const char REGION = (char)0x07;
        public const char COM_PROTO = (char)0x08;
        public const char CHIP_OUT_TYPE = (char)0x09;
        public const char READ1_ANT1 = (char)0x0C;
        public const char READ1_ANT2 = (char)0x0D;
        public const char READ1_ANT3 = (char)0x0E;
        public const char READ1_ANT4 = (char)0x0F;
        public const char READ2_ANT1 = (char)0x10;
        public const char READ2_ANT2 = (char)0x11;
        public const char READ2_ANT3 = (char)0x12;
        public const char READ2_ANT4 = (char)0x13;
        public const char READ1_MODE = (char)0x14;
        public const char READ2_MODE = (char)0x15;
        public const char READ1_SESSION = (char)0x16;
        public const char READ2_SESSION = (char)0x17;
        public const char READ1_POWER = (char)0x18;
        public const char READ2_POWER = (char)0x19;
        public const char READ1_IP = (char)0x1A;
        public const char READ2_IP = (char)0x1B;
        public const char GATING_MODE = (char)0x1D;
        public const char GATING_INTERVAL = (char)0x1E;
        public const char CHANNEL_NUMBER = (char)0x1F;
        public const char BEEPER_VOLUME = (char)0x21;
        public const char AUTO_SET_GPS = (char)0x22;
        public const char TIME_ZONE = (char)0x23;
        public const char DATA_SENDING = (char)0x24;
        public const char ULTRA_ID = (char)0x25;
        public const char READ1_ANTENNA4_BACKUP = (char)0x26;
        public const char READ2_ANTENNA4_BACKUP = (char)0x27;
        public const char WHEN_BEEP = (char)0x28;
        public const char UPLOAD_URL = (char)0x29;
        public const char GATEWAY = (char)0x2A;
        public const char DNS_SERVER = (char)0x2B;
        public const char SET_TIME = (char)0x20;
        public const char REWIND_DELIMITER = (char)0x0D;
        public const char LOG_SIZE = (char)0x1C;
        public const char LINE_FEED = (char)0x0A;
    }
}

