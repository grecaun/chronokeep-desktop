using Chronokeep.Interfaces.Timing;
using Chronokeep.Objects.ChronokeepPortal;
using Chronokeep.Timing.Interfaces;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Timing
{
    /// <summary>
    /// A connected pair of loopback sockets. The reader interface sends on Client,
    /// and the tests read what arrived on Server, byte for byte.
    /// </summary>
    internal sealed class SocketPair : IDisposable
    {
        public Socket Client { get; }
        public Socket Server { get; }

        public SocketPair()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Client.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
            Server = listener.AcceptSocket();
            listener.Stop();
            Server.ReceiveTimeout = 2000;
        }

        /// <summary>Reads until the given number of line feeds have arrived.</summary>
        public byte[] ReadLines(int lines = 1)
        {
            List<byte> output = [];
            byte[] buf = new byte[1024];
            while (output.Count(b => b == (byte)'\n') < lines)
            {
                int n = Server.Receive(buf);
                if (n == 0) break;
                output.AddRange(buf[..n]);
            }
            return [.. output];
        }

        /// <summary>One message, decoded so each byte is one char (0xFF stays 0xFF).</summary>
        public string Line() => Encoding.Latin1.GetString(ReadLines());

        public bool NothingSent() => !Server.Poll(300_000, SelectMode.SelectRead);

        public void Dispose()
        {
            Client.Dispose();
            Server.Dispose();
        }
    }

    internal static class CultureScope
    {
        /// <summary>Runs code with a culture whose time separator is '.', like Finnish or Danish.</summary>
        public static T In<T>(string name, Func<T> action)
        {
            var old = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo(name);
            try
            {
                return action();
            }
            finally
            {
                CultureInfo.CurrentCulture = old;
            }
        }

        public static void In(string name, Action action) => In(name, () => { action(); return 0; });
    }

    public class UltraCommandTests : ReaderTestBase
    {
        private readonly SocketPair pair = new();
        private readonly RfidUltraInterface reader;

        public UltraCommandTests()
        {
            reader = new RfidUltraInterface(Db, Location, Window);
            reader.SetMainSocket(pair.Client);
        }

        public override void Dispose()
        {
            pair.Dispose();
            base.Dispose();
        }

        private const char Term = (char)0xFF;

        [Fact]
        public void TextCommands()
        {
            List<(Action Send, string Expected)> commands =
            [
                (reader.StartReading, "R\n"),
                (reader.StopReading, "S\n"),
                (reader.GetStatus, "?\n"),
                (reader.GetTime, "r\n"),
                (reader.QuerySettings, "U\n"),
                (reader.StopRewind, "9\n"),
                (reader.StartSending, "700\n"),
                (reader.StopSending, "s\n"),
            ];
            foreach (var (send, expected) in commands)
            {
                send();
                Assert.Equal(expected, pair.Line());
            }
        }

        [Fact]
        public void SetTime_SendsTimeThenDate()
        {
            reader.SetTime(new DateTime(2026, 6, 15, 8, 5, 3));
            Assert.Equal("t 08:05:03 15-06-2026\n", pair.Line());
        }

        [Theory]
        [InlineData("fi-FI")]
        [InlineData("da-DK")]
        public void SetTime_UsesColons_WhateverTheComputersLanguage(string culture)
        {
            CultureScope.In(culture, () => reader.SetTime(new DateTime(2026, 6, 15, 8, 5, 3)));
            Assert.Equal("t 08:05:03 15-06-2026\n", pair.Line());
        }

        [Fact]
        public void Rewind_ByTime_SendsUltraSecondsSplitByCarriageReturn()
        {
            var start = new DateTime(2026, 6, 15, 8, 0, 0);
            var end = new DateTime(2026, 6, 15, 9, 0, 0);
            reader.Rewind(start, end);
            Assert.Equal($"800{TimingConstants.RfidDateToEpoch(start)}\r{TimingConstants.RfidDateToEpoch(end)}\n", pair.Line());
        }

        [Fact]
        public void Rewind_Everything()
        {
            reader.Rewind();
            Assert.Equal("8000\r0\n", pair.Line());
        }

        [Fact]
        public void Rewind_ByIndex_StartsAtOneOrMore()
        {
            reader.Rewind(0, 25);
            Assert.Equal("6001\r25\n", pair.Line());
            reader.Rewind(5, 25);
            Assert.Equal("6005\r25\n", pair.Line());
        }

        [Fact]
        public void Settings_AreTerminatedWithByteFF()
        {
            reader.SetGatingMode((char)0x01);
            byte[] sent = pair.ReadLines();
            Assert.Equal(new byte[] { (byte)'u', 0x1D, 0x01, 0xFF, (byte)'\n' }, sent);
        }

        [Fact]
        public void SettingsUsedByTheSettingsWindow()
        {
            List<(Action Send, string Expected)> commands =
            [
                (() => reader.SetChipOutputType((char)0x01), $"u\u0009\u0001{Term}\n"),
                (() => reader.SetGatingMode((char)0x02), $"u\u001D\u0002{Term}\n"),
                (() => reader.SetGatingInterval(5), $"u\u001E5{Term}\n"),
                (() => reader.SetWhenToBeep((char)0x01), $"u\u0028\u0001{Term}\n"),
                (() => reader.SetBeeperVolume((char)0x02), $"u\u0021\u0002{Term}\n"),
                (() => reader.SetAutoGpsTime((char)0x01), $"u\u0022\u0001{Term}\n"),
                (() => reader.SetTimeZone(-7), $"u\u0023-7{Term}\n"),
                (() => reader.SetUltraId(200), $"u\u0025200{Term}\n"),
            ];
            foreach (var (send, expected) in commands)
            {
                send();
                Assert.Equal(expected, pair.Line());
            }
        }

        [Theory]
        [InlineData(24)]
        [InlineData(-24)]
        public void SetTimeZone_OutOfRange_SendsNothing(int zone)
        {
            reader.SetTimeZone(zone);
            Assert.True(pair.NothingSent());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(256)]
        public void SetUltraId_OutOfRange_SendsNothing(int id)
        {
            reader.SetUltraId(id);
            Assert.True(pair.NothingSent());
        }

        /// <summary>Ultra manual, section H.3: "A character representing the Antenna 4 backup flag. 0x00: Off 0x01: On".</summary>
        [Fact]
        public void SetAntenna4Backup_SendsAFlagByte()
        {
            reader.SetAntenna4Backup(1, true);
            Assert.Equal(new byte[] { (byte)'u', 0x26, 0x01, 0xFF, (byte)'\n' }, pair.ReadLines());
            reader.SetAntenna4Backup(2, false);
            Assert.Equal(new byte[] { (byte)'u', 0x27, 0x00, 0xFF, (byte)'\n' }, pair.ReadLines());
        }

        /// <summary>Ultra manual, section H.3: gating interval is 0x1E and channel number is 0x1F.</summary>
        [Fact]
        public void SetChannelNumber_UsesTheChannelSetting()
        {
            reader.SetChannelNumber((char)0x02);
            Assert.Equal($"u{RfidUltraCodes.CHANNEL_NUMBER}\u0002{Term}\n", pair.Line());
        }

        [Fact]
        public void SetReaderIp_SendsEachOctetAsAByte()
        {
            reader.SetReaderIp(1, "192.168.1.10");
            Assert.Equal(new byte[] { (byte)'u', 0x1A, 192, 168, 1, 10, 0xFF, (byte)'\n' }, pair.ReadLines());
        }

        [Fact]
        public void SetReaderPower_IsCappedAt30()
        {
            reader.SetReaderPower(2, 45);
            Assert.Equal($"u\u001930{Term}\n", pair.Line());
        }

        // Ultra manual, section H.3: each setting change is acknowledged with u[setting][0x02].
        private Dictionary<MessageType, List<string>> Reply(string text)
            => new RfidUltraInterface(Db, Location, Window).ParseMessages(text, Sock);

        [Fact]
        public void SettingSaved_IsNotAnError()
        {
            var output = Reply("u\u001D\u0002\n");
            Assert.True(output.ContainsKey(MessageType.SETTINGCHANGE));
            Assert.False(output.ContainsKey(MessageType.ERROR));
        }

        [Fact]
        public void SettingNotSaved_SaysWhichSetting()
        {
            var output = Reply("u\u001E\u0000\n");
            Assert.True(output.ContainsKey(MessageType.SETTINGCHANGE));
            Assert.Equal("The reader didn't save the gating interval setting.", Assert.Single(output[MessageType.ERROR]));
        }

        [Fact]
        public void SeveralReplies_OnlyTheUnsavedOnesAreErrors()
        {
            var output = Reply("u\u001D\u0002\nu#\u0001\nu%\u0002\nu\u0009\u0000\n");
            Assert.Equal(
                ["The reader didn't save the time zone setting.", "The reader didn't save the chip output type setting."],
                output[MessageType.ERROR]);
        }

        [Fact]
        public void ReplyWithoutAResult_IsNotSaved()
        {
            var output = Reply("u(\n");
            Assert.Equal("The reader didn't save the when to beep setting.", Assert.Single(output[MessageType.ERROR]));
        }

        [Fact]
        public void ReplyWithoutASetting_IsIgnored()
        {
            var output = Reply("u\n");
            Assert.False(output.ContainsKey(MessageType.ERROR));
        }

        [Fact]
        public void UnknownSetting_IsNamedByNumber()
        {
            Assert.Equal("0x07", RfidUltraInterface.SettingName((char)0x07));
        }

        [Theory]
        [InlineData("fi-FI")]
        [InlineData("da-DK")]
        public void TimeMessage_IsReadWhateverTheComputersLanguage(string culture)
        {
            var parser = new RfidUltraInterface(Db, Location, Window);
            var output = CultureScope.In(culture, () => parser.ParseMessages("8:5:3 15-6-2026 (1)\n", Sock));
            Assert.True(output.ContainsKey(MessageType.TIME));
        }
    }

    public class IpicoCommandTests : ReaderTestBase
    {
        private readonly SocketPair pair = new();
        private readonly IpicoInterface reader;

        public IpicoCommandTests()
        {
            reader = new IpicoInterface(Db, Location, Constants.Readers.SYSTEM_IPICO, Window);
            reader.SetSettingsSocket(pair.Client);
        }

        public override void Dispose()
        {
            pair.Dispose();
            base.Dispose();
        }

        [Fact]
        public void GetTime()
        {
            reader.GetTime();
            Assert.Equal("getdate\n", pair.Line());
        }

        [Fact]
        public void SetTime_SendsTheTimeAskedFor()
        {
            reader.SetTime(new DateTime(2026, 6, 15, 8, 5, 3));
            Assert.Equal("setdate.26061508:05:03\n", pair.Line());
        }

        [Theory]
        [InlineData("fi-FI")]
        [InlineData("da-DK")]
        public void SetTime_UsesColons_WhateverTheComputersLanguage(string culture)
        {
            CultureScope.In(culture, () => reader.SetTime(DateTime.Now));
            Assert.Matches(@"^setdate\.\d{8}:\d\d:\d\d\n$", pair.Line());
        }

        [Theory]
        [InlineData(1, "ttyS0")]
        [InlineData(2, "ttyS1")]
        public void Rewind_FromATime_UsesThatReadersLog(int readerNo, string file)
        {
            reader.Rewind(new DateTime(2026, 6, 15, 8, 0, 0), new DateTime(2026, 6, 15, 9, 0, 0), readerNo);
            Assert.Equal($"replay_start file.{file} port.10300 datetime.260615080000\n", pair.Line());
        }

        [Theory]
        [InlineData(1, "ttyS0")]
        [InlineData(2, "ttyS1")]
        public void Rewind_Everything(int readerNo, string file)
        {
            reader.Rewind(readerNo);
            Assert.Equal($"replay_start file.{file} port.10300\n", pair.Line());
        }
    }

    public class PortalCommandTests : ReaderTestBase
    {
        private readonly SocketPair pair = new();
        private readonly ChronokeepInterface portal;

        public PortalCommandTests()
        {
            portal = new ChronokeepInterface(Db, Location, Window);
            // Connect() finds the portal over UDP first, so plug the socket in directly.
            typeof(ChronokeepInterface).GetField("sock", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(portal, pair.Client);
        }

        public override void Dispose()
        {
            pair.Dispose();
            base.Dispose();
        }

        private JsonElement Sent()
        {
            string line = Encoding.UTF8.GetString(pair.ReadLines());
            Assert.EndsWith("\n", line);
            return JsonDocument.Parse(line).RootElement;
        }

        [Fact]
        public void SimpleCommands_UseThePortalsCommandNames()
        {
            List<(Action Send, string Command)> commands =
            [
                (portal.StartReading, "reader_start_all"),
                (portal.StopReading, "reader_stop_all"),
                (portal.GetTime, "time_get"),
                (() => portal.Rewind(), "reads_get_all"),
                (portal.SendQuit, "quit"),
                (portal.SendRestart, "restart"),
                (portal.SendUpdate, "update"),
                (portal.SendShutdown, "shutdown"),
                (portal.SendGetSettings, "settings_get_all"),
                (portal.SendManualResultsUpload, "api_remote_manual_upload"),
                (portal.SendDeleteAllReads, "reads_delete_all"),
                (portal.Disconnect, "disconnect"),
            ];
            foreach (var (send, command) in commands)
            {
                send();
                Assert.Equal(command, Sent().GetProperty("command").GetString());
            }
        }

        [Fact]
        public void Shutdown_Commands_AreNotUnexpectedDisconnects()
        {
            Assert.False(portal.WasShutdown());
            portal.SendQuit();
            Assert.True(portal.WasShutdown());
        }

        [Fact]
        public void Rewind_ByTime_SendsUnixSeconds()
        {
            var start = new DateTime(2026, 6, 15, 8, 0, 0, DateTimeKind.Local);
            var end = new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Local);
            portal.Rewind(start, end);
            var json = Sent();
            Assert.Equal("reads_get", json.GetProperty("command").GetString());
            Assert.Equal(new DateTimeOffset(start).ToUnixTimeSeconds(), json.GetProperty("start_seconds").GetInt64());
            Assert.Equal(new DateTimeOffset(end).ToUnixTimeSeconds(), json.GetProperty("end_seconds").GetInt64());
        }

        private static string ExpectedPortalTime(DateTime local)
            => local.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "+00:00";

        [Fact]
        public void SetTime_SendsUtc()
        {
            var when = new DateTime(2026, 6, 15, 8, 5, 3, 250, DateTimeKind.Local);
            portal.SetTime(when);
            var json = Sent();
            Assert.Equal("time_set", json.GetProperty("command").GetString());
            Assert.Equal(ExpectedPortalTime(when), json.GetProperty("time").GetString());
        }

        [Theory]
        [InlineData("fi-FI")]
        [InlineData("da-DK")]
        public void SetTime_UsesColons_WhateverTheComputersLanguage(string culture)
        {
            var when = new DateTime(2026, 6, 15, 8, 5, 3, 250, DateTimeKind.Local);
            CultureScope.In(culture, () => portal.SetTime(when));
            Assert.Equal(ExpectedPortalTime(when), Sent().GetProperty("time").GetString());
        }

        [Fact]
        public void SaveReader_SendsEveryField()
        {
            portal.SendSaveReader(new PortalReader { Id = 3, Name = "Finish", Kind = "ZEBRA", IpAddress = "10.0.0.9", Port = 5084, AutoConnect = true });
            var json = Sent();
            Assert.Equal("reader_add", json.GetProperty("command").GetString());
            Assert.Equal(3, json.GetProperty("id").GetInt64());
            Assert.Equal("Finish", json.GetProperty("name").GetString());
            Assert.Equal("ZEBRA", json.GetProperty("kind").GetString());
            Assert.Equal("10.0.0.9", json.GetProperty("ip_address").GetString());
            Assert.Equal(5084, json.GetProperty("port").GetInt32());
            Assert.True(json.GetProperty("auto_connect").GetBoolean());
        }

        [Fact]
        public void ReaderCommands_SendTheReaderId()
        {
            var r = new PortalReader { Id = 7 };
            List<(Action Send, string Command)> commands =
            [
                (() => portal.SendStartReader(r), "reader_start"),
                (() => portal.SendStopReader(r), "reader_stop"),
                (() => portal.SendRemoveReader(r), "reader_remove"),
            ];
            foreach (var (send, command) in commands)
            {
                send();
                var json = Sent();
                Assert.Equal(command, json.GetProperty("command").GetString());
                Assert.Equal(7, json.GetProperty("id").GetInt64());
            }
        }

        [Fact]
        public void SaveAndDeleteApi()
        {
            var api = new PortalApi { Id = 2, Nickname = "Results", Kind = "CHRONOKEEP_REMOTE", Uri = "https://x/", Token = "t", Enabled = true, Heartbeat = true, DeviceName = "Finish" };
            portal.SendSaveApi(api);
            var json = Sent();
            Assert.Equal("api_save", json.GetProperty("command").GetString());
            Assert.Equal(2, json.GetProperty("id").GetInt64());
            Assert.Equal("Results", json.GetProperty("name").GetString());
            Assert.Equal("CHRONOKEEP_REMOTE", json.GetProperty("kind").GetString());
            Assert.Equal("https://x/", json.GetProperty("uri").GetString());
            Assert.Equal("t", json.GetProperty("token").GetString());
            Assert.True(json.GetProperty("enabled").GetBoolean());
            Assert.True(json.GetProperty("heartbeat").GetBoolean());
            Assert.Equal("Finish", json.GetProperty("device_name").GetString());

            portal.SendDeleteApi(api);
            json = Sent();
            Assert.Equal("api_remove", json.GetProperty("command").GetString());
            Assert.Equal(2, json.GetProperty("id").GetInt64());
        }

        [Theory]
        [InlineData(AutoUploadQuery.START, "start")]
        [InlineData(AutoUploadQuery.STOP, "stop")]
        [InlineData(AutoUploadQuery.STATUS, "status")]
        public void AutoUpload_SendsTheQuery(AutoUploadQuery query, string expected)
        {
            portal.SendAutoUploadResults(query);
            var json = Sent();
            Assert.Equal("api_remote_auto_upload", json.GetProperty("command").GetString());
            Assert.Equal(expected, json.GetProperty("query").GetString());
        }

        [Fact]
        public void SetSettings_SendsEveryNameAndValue()
        {
            portal.SendSetSettings(new PortalSettingsHolder
            {
                Name = "Finish Portal",
                ReadWindow = 20,
                ChipType = PortalSettingsHolder.ChipTypeEnum.HEX,
                Volume = 0.5,
                PlaySound = true,
            });
            var json = Sent();
            Assert.Equal("settings_set", json.GetProperty("command").GetString());
            var settings = json.GetProperty("settings").EnumerateArray()
                .ToDictionary(s => s.GetProperty("name").GetString()!, s => s.GetProperty("value").GetString());
            Assert.Equal(13, settings.Count);
            Assert.Equal("Finish Portal", settings[PortalSetting.SETTING_PORTAL_NAME]);
            Assert.Equal("20", settings[PortalSetting.SETTING_READ_WINDOW]);
            Assert.Equal(PortalSetting.TYPE_CHIP_HEX, settings[PortalSetting.SETTING_CHIP_TYPE]);
            Assert.Equal("0.5", settings[PortalSetting.SETTING_VOLUME]);
            Assert.Equal("true", settings[PortalSetting.SETTING_PLAY_SOUND]);
        }

        [Fact]
        public void SetSettings_VolumeUsesAPoint_WhateverTheComputersLanguage()
        {
            CultureScope.In("fi-FI", () => portal.SendSetSettings(new PortalSettingsHolder { Volume = 0.5 }));
            var settings = Sent().GetProperty("settings").EnumerateArray()
                .ToDictionary(s => s.GetProperty("name").GetString()!, s => s.GetProperty("value").GetString());
            Assert.Equal("0.5", settings[PortalSetting.SETTING_VOLUME]);
        }
    }
}
