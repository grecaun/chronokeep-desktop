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

using Avalonia;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.Timing;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronokeepPortal;
using Chronokeep.UI.Timing.ReaderSettings;
using Chronokeep.UI.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Chronokeep.Objects.ChronokeepRemote;

namespace Chronokeep.Timing.Interfaces
{
    public partial class ChronokeepInterface(IdbInterface database, int locationId, IMainWindow window) : ITimingSystemInterface
    {
        private readonly Event theEvent = database.GetCurrentEvent()!;
        private readonly StringBuilder buffer = new();
        private Socket? sock;
        private bool wasShutdown;

        private ChronokeepSettings? settingsWindow;
        private string readerIp = "";
        private string readerName = "";

        [GeneratedRegex(@"^\[(?'PORTAL_NAME'[^|]*)\|(?'PORTAL_ID'[^|]*)\|(?'PORTAL_PORT'\d{1,5})\]")]
        private static partial Regex ZeroConf();
        [GeneratedRegex(@"^[^\n]*\n")]
        private static partial Regex Msg();

        public List<Socket>? Connect(string ipAddress, int _)
        {
            readerIp = ipAddress;
            List<Socket> output = [];
            sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                Log.D("Timing.Interfaces.ChronokeepInterface", "Attempting to get port from server.");
                using UdpClient client = new(AddressFamily.InterNetwork);
                byte[] msg = Encoding.Default.GetBytes(Constants.Network.CHRONOKEEP_ZCONF_CONNECT_MSG);
                IPEndPoint endPoint = new(IPAddress.Parse(ipAddress), Constants.Network.CHRONOKEEP_ZCONF_PORT);
                client.Send(msg, msg.Length, endPoint);
                client.Client.ReceiveTimeout = Constants.Readers.TIMEOUT;
                byte[] data = client.Receive(ref endPoint);
                string response = Encoding.Default.GetString(data);
                Match match = ZeroConf().Match(response);
                if (match.Success)
                {
                    Log.D("Timing.Interfaces.ChronokeepInterface", $"Successfully received message from reader. Name is {match.Groups["PORTAL_NAME"].Value}. Id is {match.Groups["PORTAL_ID"].Value}. Port is {match.Groups["PORTAL_PORT"].Value}");
                    readerName = match.Groups["PORTAL_NAME"].Value;
                    if (!int.TryParse(match.Groups["PORTAL_PORT"].Value, out int port))
                    {
                        Log.E("Timing.Interfaces.ChronokeepInterface", "Error parsing port.");
                        return null;
                    }
                    sock.Connect(ipAddress, port);
                    SendMessage(JsonSerializer.Serialize(new ConnectRequest()
                    {
                        Reads = true,
                    }));
                    output.Add(sock);
                }
                else
                {
                    Log.E("Timing.Interfaces.ChronokeepInterface", $"Unable to parse message from server. Unknown value. '{response}'");
                    return null;
                }
            }
            catch
            {
                Log.E("Timing.Interfaces.ChronokeepInterface", "Error connecting to reader.");
                return null;
            }
            Log.D("Timing.Interfaces.ChronokeepInterface", "Connected. Returning socket.");
            return output;
        }

        public void GetStatus() { }

        public void GetTime()
        {
            Log.D("Timing.Interfaces.ChronokeepInterface", "Requesting time.");
            SendMessage(JsonSerializer.Serialize(new TimeGetRequest()));
        }

        public Dictionary<MessageType, List<string>> ParseMessages(string inMessage, Socket _)
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
            while (m.Success)
            {
                buffer.Remove(m.Index, m.Length);
                string message = m.Value;
                try
                {
                    Response res = JsonSerializer.Deserialize<Response>(message)!;
                    switch (res.Command)
                    {
                        case Response.KEEPALIVE:
                            Log.D("Timing.Interfaces.ChronokeepInterface", "Reader sent keepalive message.");
                            SendMessage(JsonSerializer.Serialize(new KeepaliveAckRequest()));
                            break;
                        case Response.READERS:
                            Log.D("Timing.Interfaces.ChronokeepInterface", "Reader sent readers message.");
                            try
                            {
                                ReadersResponse readRes = JsonSerializer.Deserialize<ReadersResponse>(message)!;
                                settingsWindow?.UpdateView(new PortalSettingsHolder
                                {
                                    Readers = readRes.List,
                                    Changes = [PortalSettingsHolder.ChangeType.READERS]
                                });
                                int oneReadingCount = readRes.List.Count(reader => reader.Reading);
                                if (!output.TryGetValue(MessageType.STATUS, out List<string>? readersStatusList))
                                {
                                    readersStatusList = [];
                                    output[MessageType.STATUS] = readersStatusList;
                                }
                                if (oneReadingCount == 0)
                                {
                                    readersStatusList.Add(TimingSystem.READING_STATUS_STOPPED);
                                }
                                else if (oneReadingCount == readRes.List.Count)
                                {
                                    output[MessageType.STATUS].Add(TimingSystem.READING_STATUS_READING);
                                }
                                else
                                {
                                    output[MessageType.STATUS].Add(TimingSystem.READING_STATUS_PARTIAL);
                                }
                                if (!output.TryGetValue(MessageType.SETTINGVALUE, out List<string>? settingList))
                                {
                                    settingList = [];
                                    output[MessageType.SETTINGVALUE] = settingList;
                                }

                                settingList.Add(message);
                            }
                            catch (Exception e)
                            {
                                Log.E("Timing.Interfaces.ChronokeepInterface", $"Error processing readers. {e.Message}");
                                if (!output.TryGetValue(MessageType.ERROR, out List<string>? errorList))
                                {
                                    errorList = [];
                                    output[MessageType.ERROR] = errorList;
                                }

                                errorList.Add("Error processing readers.");
                            }
                            break;
                        case Response.READER_ANTENNAS:
                            Log.D("Timing.Interfaces.ChronokeepInterface", "Reader sent reader antennas message.");
                            try
                            {
                                ReaderAntennasResponse antRes = JsonSerializer.Deserialize<ReaderAntennasResponse>(message)!;
                                settingsWindow?.UpdateView(new PortalSettingsHolder
                                {
                                    Antennas = new PortalSettingsHolder.ReaderAntennas
                                    {
                                        ReaderName = antRes.ReaderName,
                                        Antennas = antRes.Antennas,
                                    },
                                    Changes = [PortalSettingsHolder.ChangeType.ANTENNAS]
                                });
                            }
                            catch (Exception e)
                            {
                                Log.E("Timing.Interfaces.ChronokeepInterface", $"Error processing reader antennas. {e.Message}");
                                if (!output.TryGetValue(MessageType.ERROR, out List<string>? errorList))
                                {
                                    errorList = [];
                                    output[MessageType.ERROR] = errorList;
                                }

                                errorList.Add("Error processing reader antennas.");
                            }
                            break;
                        case Response.ERROR:
                            Log.D("Timing.Interfaces.ChronokeepInterface", "Reader sent error message.");
                            try
                            {
                                ErrorResponse err = JsonSerializer.Deserialize<ErrorResponse>(message)!;
                                if (!output.TryGetValue(MessageType.ERROR, out List<string>? errorList))
                                {
                                    errorList = [];
                                    output[MessageType.ERROR] = errorList;
                                }
                                Log.E("Timing.Interfaces.ChronokeepInterface", $"Error sent to us is of type '{err.Value.Type}' and has message '{err.Value.Message}'.");
                                window?.ShowNotificationDialog(readerName, readerIp, new RemoteNotification
                                {
                                    Type = err.Value.Type,
                                    When = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                    Message = err.Value.Message
                                });
                                errorList.Add(err.Value.Message);
                            }
                            catch (Exception e)
                            {
                                Log.E("Timing.Interfaces.ChronokeepInterface", $"Unable to process chip read. {e.Message}");
                                if (!output.TryGetValue(MessageType.ERROR, out List<string>? errorList))
                                {
                                    errorList = [];
                                    output[MessageType.ERROR] = errorList;
                                }
                                errorList.Add("Error processing chip read.");
                            }
                            break;
                        case Response.SETTINGS:
                            Log.D("Timing.Interfaces.ChronokeepInterface", "Reader sent settings message.");
                            try
                            {
                                SettingsResponse settingsList = JsonSerializer.Deserialize<SettingsResponse>(message)!;
                                if (settingsWindow != null)
                                {
                                    PortalSettingsHolder updSettings = new();
                                    foreach (PortalSetting set in settingsList.List)
                                    {
                                        switch (set.Name)
                                        {
                                            case PortalSetting.SETTING_PORTAL_NAME:
                                                updSettings.Name = set.Value;
                                                break;
                                            case PortalSetting.SETTING_READ_WINDOW:
                                                updSettings.ReadWindow = int.Parse(set.Value);
                                                break;
                                            case PortalSetting.SETTING_CHIP_TYPE:
                                                updSettings.ChipType = set.Value == PortalSetting.TYPE_CHIP_DEC ? PortalSettingsHolder.ChipTypeEnum.DEC : PortalSettingsHolder.ChipTypeEnum.HEX;
                                                break;
                                            case PortalSetting.SETTING_PLAY_SOUND:
                                                updSettings.PlaySound = bool.Parse(set.Value);
                                                break;
                                            case PortalSetting.SETTING_VOLUME:
                                                updSettings.Volume = double.Parse(set.Value);
                                                break;
                                            case PortalSetting.SETTING_VOICE:
                                                updSettings.Voice = set.Value switch
                                                {
                                                    PortalSetting.VOICE_EMILY => PortalSettingsHolder.VoiceType.EMILY,
                                                    PortalSetting.VOICE_MICHAEL => PortalSettingsHolder.VoiceType
                                                        .MICHAEL,
                                                    PortalSetting.VOICE_CUSTOM => PortalSettingsHolder.VoiceType.CUSTOM,
                                                    _ => PortalSettingsHolder.VoiceType.EMILY
                                                };
                                                break;
                                            case PortalSetting.SETTING_UPLOAD_INTERVAL:
                                                updSettings.UploadInterval = int.Parse(set.Value);
                                                break;
                                            case PortalSetting.SETTING_BEEP_INTERVAL:
                                                updSettings.BeepInterval = int.Parse(set.Value);
                                                break;
                                            case PortalSetting.SETTING_NTFY_URL:
                                                updSettings.NtfyUrl = set.Value;
                                                break;
                                            case PortalSetting.SETTING_NTFY_TOPIC:
                                                updSettings.NtfyTopic = set.Value;
                                                break;
                                            case PortalSetting.SETTING_NTFY_USER:
                                                updSettings.NtfyUser = set.Value;
                                                break;
                                            case PortalSetting.SETTING_NTFY_PASS:
                                                updSettings.NtfyPass = set.Value;
                                                break;
                                            case PortalSetting.SETTING_ENABLE_NTFY:
                                                updSettings.EnableNtfy = set.Value.Equals("true", StringComparison.OrdinalIgnoreCase);
                                                break;
                                            case PortalSetting.SETTING_SCREEN_TYPE:
                                                updSettings.ScreenType = set.Value;
                                                break;
                                        }
                                        updSettings.Changes.Add(PortalSettingsHolder.ChangeType.SETTINGS);
                                    }
                                    settingsWindow.UpdateView(updSettings);
                                }
                                if (!output.TryGetValue(MessageType.SETTINGVALUE, out List<string>? settingList))
                                {
                                    settingList = [];
                                    output[MessageType.SETTINGVALUE] = settingList;
                                }

                                settingList.Add(message);
                            }
                            catch (Exception e)
                            {
                                Log.E("Timing.Interfaces.ChronokeepInterface", $"Error processing settings. {e.Message}");
                                if (!output.TryGetValue(MessageType.ERROR, out List<string>? errorList))
                                {
                                    errorList = [];
                                    output[MessageType.ERROR] = errorList;
                                }
                                errorList.Add("Error processing settings.");
                            }
                            break;
                        case Response.API_LIST:
                            Log.D("Timing.Interfaces.ChronokeepInterface", "Reader sent api list message.");
                            try
                            {
                                ApiListResponse apiList = JsonSerializer.Deserialize<ApiListResponse>(message)!;
                                settingsWindow?.UpdateView(new PortalSettingsHolder
                                {
                                    ApIs = apiList.List,
                                    Changes = [PortalSettingsHolder.ChangeType.APIS]
                                });
                                if (!output.TryGetValue(MessageType.SETTINGVALUE, out List<string>? settingList))
                                {
                                    settingList = [];
                                    output[MessageType.SETTINGVALUE] = settingList;
                                }

                                settingList.Add(message);
                            }
                            catch (Exception e)
                            {
                                Log.E("Timing.Interfaces.ChronokeepInterface", $"Error processing api list. {e.Message}");
                                if (!output.TryGetValue(MessageType.ERROR, out List<string>? errorList))
                                {
                                    errorList = [];
                                    output[MessageType.ERROR] = errorList;
                                }
                                errorList.Add("Error processing api list.");
                            }
                            break;
                        case Response.SETTINGS_ALL:
                            Log.D("Timing.Interfaces.ChronokeepInterface", "Reader sent all settings message.");
                            try
                            {
                                SettingsAllResponse allSettings = JsonSerializer.Deserialize<SettingsAllResponse>(message)!;
                                if (settingsWindow != null)
                                {
                                    PortalSettingsHolder updSettings = new()
                                    {
                                        Readers = allSettings.Readers,
                                        ApIs = allSettings.ApIs,
                                        AutoUpload = allSettings.AutoUpload,
                                        PortalVersion = allSettings.PortalVersion,
                                    };
                                    foreach (PortalSetting set in allSettings.Settings)
                                    {
                                        switch (set.Name)
                                        {
                                            case PortalSetting.SETTING_PORTAL_NAME:
                                                updSettings.Name = set.Value;
                                                break;
                                            case PortalSetting.SETTING_READ_WINDOW:
                                                updSettings.ReadWindow = int.Parse(set.Value);
                                                break;
                                            case PortalSetting.SETTING_CHIP_TYPE:
                                                updSettings.ChipType = set.Value == PortalSetting.TYPE_CHIP_DEC ? PortalSettingsHolder.ChipTypeEnum.DEC : PortalSettingsHolder.ChipTypeEnum.HEX;
                                                break;
                                            case PortalSetting.SETTING_PLAY_SOUND:
                                                updSettings.PlaySound = bool.Parse(set.Value);
                                                break;
                                            case PortalSetting.SETTING_VOLUME:
                                                updSettings.Volume = double.Parse(set.Value);
                                                break;
                                            case PortalSetting.SETTING_VOICE:
                                                updSettings.Voice = set.Value switch
                                                {
                                                    PortalSetting.VOICE_EMILY => PortalSettingsHolder.VoiceType.EMILY,
                                                    PortalSetting.VOICE_MICHAEL => PortalSettingsHolder.VoiceType
                                                        .MICHAEL,
                                                    PortalSetting.VOICE_CUSTOM => PortalSettingsHolder.VoiceType.CUSTOM,
                                                    _ => PortalSettingsHolder.VoiceType.EMILY
                                                };
                                                break;
                                            case PortalSetting.SETTING_UPLOAD_INTERVAL:
                                                updSettings.UploadInterval = int.Parse(set.Value);
                                                break;
                                            case PortalSetting.SETTING_BEEP_INTERVAL:
                                                updSettings.BeepInterval = int.Parse(set.Value);
                                                break;
                                            case PortalSetting.SETTING_NTFY_URL:
                                                updSettings.NtfyUrl = set.Value;
                                                break;
                                            case PortalSetting.SETTING_NTFY_TOPIC:
                                                updSettings.NtfyTopic = set.Value;
                                                break;
                                            case PortalSetting.SETTING_NTFY_USER:
                                                updSettings.NtfyUser = set.Value;
                                                break;
                                            case PortalSetting.SETTING_NTFY_PASS:
                                                updSettings.NtfyPass = set.Value;
                                                break;
                                            case PortalSetting.SETTING_ENABLE_NTFY:
                                                updSettings.EnableNtfy = set.Value.Equals("true", StringComparison.OrdinalIgnoreCase);
                                                break;
                                            case PortalSetting.SETTING_SCREEN_TYPE:
                                                updSettings.ScreenType = set.Value;
                                                break;
                                        }
                                    }
                                    int settingsReadingCount = allSettings.Readers.Count(reader => reader.Reading);
                                    if (!output.TryGetValue(MessageType.STATUS, out List<string>? settingsStatusList))
                                    {
                                        settingsStatusList = [];
                                        output[MessageType.STATUS] = settingsStatusList;
                                    }
                                    if (settingsReadingCount == 0)
                                    {
                                        settingsStatusList.Add(TimingSystem.READING_STATUS_STOPPED);
                                    }
                                    else if (settingsReadingCount == allSettings.Readers.Count)
                                    {
                                        output[MessageType.STATUS].Add(TimingSystem.READING_STATUS_READING);
                                    }
                                    else
                                    {
                                        output[MessageType.STATUS].Add(TimingSystem.READING_STATUS_PARTIAL);
                                    }
                                    updSettings.Changes.Add(PortalSettingsHolder.ChangeType.SETTINGS);
                                    updSettings.Changes.Add(PortalSettingsHolder.ChangeType.READERS);
                                    updSettings.Changes.Add(PortalSettingsHolder.ChangeType.APIS);
                                    settingsWindow.UpdateView(
                                        updSettings
                                        );
                                }
                                if (!output.TryGetValue(MessageType.SETTINGVALUE, out List<string>? settingList))
                                {
                                    settingList = [];
                                    output[MessageType.SETTINGVALUE] = settingList;
                                }

                                settingList.Add(message);
                            }
                            catch (Exception e)
                            {
                                Log.E("Timing.Interfaces.ChronokeepInterface", $"Error processing all settings. {e.Message}");
                                if (!output.TryGetValue(MessageType.ERROR, out List<string>? errorList))
                                {
                                    errorList = [];
                                    output[MessageType.ERROR] = errorList;
                                }
                                errorList.Add("Error processing settings.");
                            }
                            break;
                        case Response.READS:
                            Log.D("Timing.Interfaces.ChronokeepInterface", "Reader sent reads message.");
                            try
                            {
                                ReadsResponse reads = JsonSerializer.Deserialize<ReadsResponse>(message)!;
                                if (reads.List.Count > 0)
                                {
                                    foreach (ChipRead chipRead in from pRead in reads.List where pRead.IdentType != PortalRead.READ_IDENT_TYPE_CHIP || !ignoredChips.Contains(pRead.Identifier) select new ChipRead(
                                                 theEvent.Identifier,
                                                 locationId,
                                                 pRead.IdentType == PortalRead.READ_IDENT_TYPE_CHIP,
                                                 pRead.Identifier,
                                                 Constants.Timing.UtcSecondsToRfidSeconds(pRead.Seconds),
                                                 pRead.Milliseconds,
                                                 pRead.Antenna,
                                                 pRead.Rssi,
                                                 pRead.Reader,
                                                 pRead.Type == PortalRead.READ_KIND_CHIP ? Constants.Timing.CHIPREAD_TYPE_CHIP : Constants.Timing.CHIPREAD_TYPE_MANUAL,
                                                 Constants.Timing.UtcToLocalDate(pRead.ReaderSeconds, pRead.ReaderMilliseconds).ToString("yyyy/MM/dd HH:mm:ss.fff"),
                                                 readerName
                                             ))
                                    {
                                        if (window != null && window.InDidNotStartMode())
                                        {
                                            chipRead.Status = Constants.Timing.CHIPREAD_STATUS_DNS;
                                        }
                                        chipReads.Add(chipRead);
                                    }

                                    if (chipReads.Count > 0)
                                    {
                                        output[MessageType.CHIPREAD] = [];
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Log.E("Timing.Interfaces.ChronokeepInterface", $"Unable to process chip read. {e.Message}");
                                if (!output.TryGetValue(MessageType.ERROR, out List<string>? errorList))
                                {
                                    errorList = [];
                                    output[MessageType.ERROR] = errorList;
                                }
                                errorList.Add("Error processing chip read.");
                            }
                            break;
                        case Response.SUCCESS:
                            Log.D("Timing.Interfaces.ChronokeepInterface", "Reader sent success message.");
                            output[MessageType.SUCCESS] = [];
                            break;
                        case Response.TIME:
                            Log.D("Timing.Interfaces.ChronokeepInterface", "Reader sent time message.");
                            try
                            {
                                TimeResponse t = JsonSerializer.Deserialize<TimeResponse>(message)!;
                                if (!output.TryGetValue(MessageType.TIME, out List<string>? timeList))
                                {
                                    timeList = [];
                                    output[MessageType.TIME] = timeList;
                                }

                                timeList.Clear();
                                DateTime timeDt = DateTime.ParseExact(t.Local, "yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
                                timeList.Add(timeDt.ToString("dd MMM yyyy  HH:mm:ss"));
                            }
                            catch (Exception e)
                            {
                                Log.E("Timing.Interfaces.ChronokeepInterface", $"Unable to process time message. {e.Message}");
                                if (!output.TryGetValue(MessageType.ERROR, out List<string>? errorList))
                                {
                                    errorList = [];
                                    output[MessageType.ERROR] = errorList;
                                }
                                errorList.Add("Error processing time message.");
                            }
                            break;
                        case Response.READ_AUTO_UPLOAD:
                            Log.D("Timing.Interfaces.ChronokeepInterface", "Reader sent read auto upload message.");
                            try
                            {
                                ReadAutoUploadResponse autoUploadResponse = JsonSerializer.Deserialize<ReadAutoUploadResponse>(message)!;
                                settingsWindow?.UpdateView(new PortalSettingsHolder
                                {
                                    AutoUpload = autoUploadResponse.Status,
                                });
                            }
                            catch (Exception e)
                            {
                                Log.E("Timing.Interfaces.ChronokeepInterface", $"Error auto upload message. {e.Message}");
                                if (!output.TryGetValue(MessageType.ERROR, out List<string>? errorList))
                                {
                                    errorList = [];
                                    output[MessageType.ERROR] = errorList;
                                }
                                errorList.Add("Error processing auto upload message.");
                            }
                            break;
                        case Response.CONNECTION_SUCCESSFUL:
                            Log.D("Timing.Interfaces.ChronokeepInterface", "Reader sent connection successful message.");
                            ConnectionSuccessfulResponse connectionResponse = JsonSerializer.Deserialize<ConnectionSuccessfulResponse>(message)!;
                            int readingCount = connectionResponse.Readers.Count(reader => reader.Reading);
                            if (!output.TryGetValue(MessageType.STATUS, out List<string>? statusList))
                            {
                                statusList = [];
                                output[MessageType.STATUS] = statusList;
                            }
                            if (readingCount == 0)
                            {
                                statusList.Add(TimingSystem.READING_STATUS_STOPPED);
                            }
                            else if (readingCount == connectionResponse.Readers.Count)
                            {
                                output[MessageType.STATUS].Add(TimingSystem.READING_STATUS_READING);
                            }
                            else
                            {
                                output[MessageType.STATUS].Add(TimingSystem.READING_STATUS_PARTIAL);
                            }
                            output[MessageType.CONNECTED] = [];
                            break;
                        case Response.DISCONNECT:
                            Log.D("Timing.Interfaces.ChronokeepInterface", "Reader sent disconnect message.");
                            output[MessageType.DISCONNECT] = [];
                            break;
                        case Response.NOTIFICATION:
                            Log.D("Timing.Interfaces.ChronokeepInterface", "Reader sent notification message.");
                            try
                            {
                                NotificationResponse notRes = JsonSerializer.Deserialize<NotificationResponse>(message)!;
                                string msg = notRes.Type switch
                                {
                                    PortalNotification.UPS_DISCONNECTED => $"Portal at {readerIp} UPS has been disconnected.",
                                    PortalNotification.UPS_CONNECTED => $"Portal at {readerIp} UPS connection has been re-established.",
                                    PortalNotification.UPS_ON_BATTERY => $"Portal at {readerIp} UPS is working from battery power.",
                                    PortalNotification.UPS_LOW_BATTERY => $"Portal at {readerIp} UPS battery is low. Shutdown imminent.",
                                    PortalNotification.UPS_ONLINE => $"Portal at {readerIp} UPS is back on line power.",
                                    PortalNotification.SHUTTING_DOWN => $"Portal at {readerIp} is shutting down.",
                                    PortalNotification.RESTARTING => $"Portal at {readerIp} is restarting.",
                                    PortalNotification.HIGH_TEMP => $"Portal at {readerIp} temperature is high.",
                                    PortalNotification.MAX_TEMP => $"Portal at {readerIp} temperature is very high. Throttling will most likely occur.",
                                    PortalNotification.BATTERY_LOW => $"Portal at {readerIp} is indicating the battery is low.",
                                    PortalNotification.BATTERY_CRITICAL => $"Portal at {readerIp} is indicating the battery is critical.",
                                    _ => ""
                                };
                                Application.Current!.Dispatcher.Invoke(() =>
                                {
                                    DialogBox.AsyncShow(msg);
                                });
                            }
                            catch (Exception e)
                            {
                                Log.E("Timing.Interfaces.ChronokeepInterface", $"Error processing reader antennas. {e.Message}");
                                if (!output.TryGetValue(MessageType.ERROR, out List<string>? errorList))
                                {
                                    errorList = [];
                                    output[MessageType.ERROR] = errorList;
                                }
                                errorList.Add("Error processing reader antennas.");
                            }
                            break;
                        default:
                            Log.E("Timing.Interfaces.ChronokeepInterface", $"Unknown message received: {res.Command}");
                            output[MessageType.UNKNOWN] = [];
                            break;
                    }
                }
                catch (Exception e)
                {
                    Log.E("Timing.Interfaces.ChronokeepInterface", $"Error deserializing json. {e.Message}");
                }
                m = Msg().Match(buffer.ToString());
            }
            if (chipReads.Count > 0)
            {
                database.AddChipReads(chipReads);
            }
            return output;
        }

        public void Rewind(DateTime start, DateTime end, int reader = 1)
        {
            SendMessage(JsonSerializer.Serialize(new ReadsGetRequest()
            {
                StartSeconds = Constants.Timing.UnixDateToEpoch(start.ToUniversalTime()),
                EndSeconds = Constants.Timing.UnixDateToEpoch(end.ToUniversalTime())
            }));
        }

        public void Rewind(int from, int to, int reader = 1) { }

        public void Rewind(int reader = 1)
        {
            SendMessage(JsonSerializer.Serialize(new ReadsGetAllRequest()));
        }

        public void SetMainSocket(Socket iSock) { }

        public void SetSettingsSocket(Socket iSock) { }

        public void SetTime(DateTime date)
        {
            SendMessage(JsonSerializer.Serialize(new TimeSetRequest()
            {
                Time = date.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.fffzzz")
            }));
        }

        public void StartReading()
        {
            SendMessage(JsonSerializer.Serialize(new ReaderStartAllRequest()));
        }

        public void StartSending() { }

        public void StopReading()
        {
            SendMessage(JsonSerializer.Serialize(new ReaderStopAllRequest()));
        }

        public void StopSending() { }

        public void SendQuit()
        {
            SendMessage(JsonSerializer.Serialize(new QuitRequest()));
            wasShutdown = true;
        }

        public void SendRestart()
        {
            SendMessage(JsonSerializer.Serialize(new RestartRequest()));
            wasShutdown = true;
        }

        public void SendUpdate()
        {
            SendMessage(JsonSerializer.Serialize(new UpdateRequest()));
            wasShutdown = true;
        }

        public void SendShutdown()
        {
            SendMessage(JsonSerializer.Serialize(new ShutdownRequest()));
            wasShutdown = true;
        }

        public void SendGetSettings()
        {
            SendMessage(JsonSerializer.Serialize(new SettingsGetAllRequest()));
        }

        public void SendSetSettings(PortalSettingsHolder settings)
        {
            SettingsSetRequest settingsReq = new()
            {
                Settings =
                [
                    new PortalSetting
                    {
                        Name = PortalSetting.SETTING_PORTAL_NAME,
                        Value = settings.Name
                    },
                    new PortalSetting
                    {
                        Name = PortalSetting.SETTING_READ_WINDOW,
                        Value = settings.ReadWindow.ToString()
                    },
                    new PortalSetting
                    {
                        Name = PortalSetting.SETTING_CHIP_TYPE,
                        Value = settings.ChipType == PortalSettingsHolder.ChipTypeEnum.DEC
                            ? PortalSetting.TYPE_CHIP_DEC
                            : PortalSetting.TYPE_CHIP_HEX
                    },
                    new PortalSetting
                    {
                        Name = PortalSetting.SETTING_VOLUME,
                        Value = settings.Volume.ToString(CultureInfo.InvariantCulture)
                    },
                    new PortalSetting
                    {
                        Name = PortalSetting.SETTING_PLAY_SOUND,
                        Value = settings.PlaySound ? "true" : "false"
                    },
                    new PortalSetting
                    {
                        Name = PortalSetting.SETTING_UPLOAD_INTERVAL,
                        Value = settings.UploadInterval.ToString()
                    },
                    new PortalSetting
                    {
                        Name = PortalSetting.SETTING_BEEP_INTERVAL,
                        Value = settings.BeepInterval.ToString()
                    },
                    new PortalSetting
                    {
                        Name = PortalSetting.SETTING_VOICE,
                        Value = settings.Voice switch
                        {
                            PortalSettingsHolder.VoiceType.EMILY => PortalSetting.VOICE_EMILY,
                            PortalSettingsHolder.VoiceType.MICHAEL => PortalSetting.VOICE_MICHAEL,
                            _ => PortalSetting.VOICE_CUSTOM
                        }
                    },
                    new PortalSetting
                    {
                        Name = PortalSetting.SETTING_NTFY_URL,
                        Value = settings.NtfyUrl
                    },
                    new PortalSetting
                    {
                        Name = PortalSetting.SETTING_NTFY_TOPIC,
                        Value = settings.NtfyTopic
                    },
                    new PortalSetting
                    {
                        Name = PortalSetting.SETTING_NTFY_USER,
                        Value = settings.NtfyUser
                    },
                    new PortalSetting
                    {
                        Name = PortalSetting.SETTING_NTFY_PASS,
                        Value = settings.NtfyPass
                    },
                    new PortalSetting
                    {
                        Name = PortalSetting.SETTING_ENABLE_NTFY,
                        Value = settings.EnableNtfy ? "true" : "false",
                    },
                    new PortalSetting
                    {
                        Name = PortalSetting.SETTING_SCREEN_TYPE,
                        Value = settings.ScreenType
                    }

                ]
            };
            SendMessage(JsonSerializer.Serialize(settingsReq));
        }

        public void SendSaveApi(PortalApi api)
        {
            SendMessage(JsonSerializer.Serialize(new ApiSaveRequest()
            {
                Id = api.Id,
                Name = api.Nickname,
                Type = api.Kind,
                Uri = api.Uri,
                Token = api.Token,
            }));
        }

        public void SendDeleteApi(PortalApi api)
        {
            SendMessage(JsonSerializer.Serialize(new ApiRemoveRequest()
            {
                Id = api.Id
            }));
        }

        public void SendSaveReader(PortalReader reader)
        {
            SendMessage(JsonSerializer.Serialize(new ReaderAddRequest()
            {
                Id = reader.Id,
                Name = reader.Name,
                Type = reader.Kind,
                IpAddress = reader.IpAddress,
                Port = reader.Port,
                AutoConnect = reader.AutoConnect,
            }));
        }

        public void SendStartReader(PortalReader reader)
        {
            SendMessage(JsonSerializer.Serialize(new ReaderStartRequest()
            {
                Id = reader.Id,
            }));
        }

        public void SendStopReader(PortalReader reader)
        {
            SendMessage(JsonSerializer.Serialize(new ReaderStopRequest()
            {
                Id = reader.Id,
            }));
        }

        public void SendRemoveReader(PortalReader reader)
        {
            SendMessage(JsonSerializer.Serialize(new ReaderRemoveRequest()
            {
                Id = reader.Id,
            }));
        }

        public void SendManualResultsUpload()
        {
            SendMessage(JsonSerializer.Serialize(new ApiRemoteManualUploadRequest()));
        }

        public void SendAutoUploadResults(AutoUploadQuery query)
        {
            string qString = query switch
            {
                AutoUploadQuery.STOP => Request.AUTO_UPLOAD_QUERY_STOP,
                AutoUploadQuery.START => Request.AUTO_UPLOAD_QUERY_START,
                AutoUploadQuery.STATUS => Request.AUTO_UPLOAD_QUERY_STATUS,
                _ => ""
            };
            SendMessage(JsonSerializer.Serialize(new ApiRemoteAutoUploadRequest()
            {
                Query = qString
            }));
        }

        public void SendDeleteAllReads()
        {
            SendMessage(JsonSerializer.Serialize(new ReadsDeleteAllRequest()));
        }

        public void Disconnect()
        {
            SendMessage(JsonSerializer.Serialize(new DisconnectRequest()));
        }

        private void SendMessage(string msg)
        {
            Log.D("Timing.Interfaces.ChronokeepInterface", $"Sending message '{msg}'");
            sock!.Send(Encoding.Default.GetBytes($"{msg}\n"));
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
            settingsWindow = new ChronokeepSettings(this);
            window.AddWindow(settingsWindow);
            settingsWindow.Show();
        }

        public void CloseSettings()
        {
            settingsWindow?.CloseWindow();
        }

        public void SettingsWindowFinalize()
        {
            window.WindowFinalize();
            settingsWindow = null;
        }

        public bool WasShutdown()
        {
            return wasShutdown;
        }
    }
}

