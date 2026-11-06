using Chronokeep.Objects;
using Chronokeep.Objects.ChronokeepRemote;
using Chronokeep.Test.Database;
using System.Text.Json;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Logic
{
    public class RemoteReadTests
    {
        private const string Json =
            "{\"count\":2,\"reads\":[" +
            "{\"identifier\":\"CHIP1\",\"seconds\":1781539200,\"milliseconds\":250,\"ident_type\":\"chip\",\"type\":\"reader\",\"antenna\":2,\"reader\":\"R1\",\"rssi\":\"-50\"}," +
            "{\"identifier\":\"42\",\"seconds\":1781539260,\"milliseconds\":0,\"ident_type\":\"bib\",\"type\":\"manual\",\"antenna\":0,\"reader\":\"\",\"rssi\":\"\"}]}";

        [Fact]
        public void Json_UsesStringEnums()
        {
            var response = JsonSerializer.Deserialize<GetReadsResponse>(Json)!;
            Assert.Equal(2, response.Reads!.Count);
            Assert.Equal(IdentType.chip, response.Reads[0].IdentType);
            Assert.Equal(ReadType.reader, response.Reads[0].Type);
            Assert.Equal(IdentType.bib, response.Reads[1].IdentType);
            Assert.Equal(ReadType.manual, response.Reads[1].Type);
        }

        [Fact]
        public void ConvertToChipRead_ChipRead()
        {
            var read = JsonSerializer.Deserialize<GetReadsResponse>(Json)!.Reads![0].ConvertToChipRead(5, TimingConstants.LOCATION_FINISH);

            Assert.Equal(5, read.EventId);
            Assert.Equal(TimingConstants.LOCATION_FINISH, read.LocationId);
            Assert.Equal("CHIP1", read.ChipNumber);
            Assert.Equal(TimingConstants.CHIPREAD_DUMMYBIB, read.ReadBib);
            Assert.Equal(TimingConstants.UtcSecondsToRfidSeconds(1781539200), read.TimeSeconds);
            Assert.Equal(250, read.TimeMilliseconds);
            Assert.Equal(2, read.Antenna);
            Assert.Equal("R1", read.Reader);
            Assert.Equal("-50", read.Rssi);
            Assert.Equal(TimingConstants.CHIPREAD_TYPE_CHIP, read.Type);
            Assert.Equal(TimingConstants.CHIPREAD_STATUS_NONE, read.Status);
        }

        [Fact]
        public void ConvertToChipRead_BibRead_IsManual()
        {
            var read = JsonSerializer.Deserialize<GetReadsResponse>(Json)!.Reads![1].ConvertToChipRead(5, TimingConstants.LOCATION_START);

            Assert.Equal(TimingConstants.CHIPREAD_DUMMYCHIP, read.ChipNumber);
            Assert.Equal("42", read.ReadBib);
            Assert.Equal("42", read.Bib);
            Assert.Equal(TimingConstants.CHIPREAD_TYPE_MANUAL, read.Type);
        }
    }

    public class VersionTests
    {
        private static Updates.Version V(int major, int minor, int patch) => new() { Major = major, Minor = minor, Patch = patch };

        [Theory]
        [InlineData(2, 0, 0, 1, 9, 10, true)]
        [InlineData(1, 10, 0, 1, 9, 10, true)]
        [InlineData(1, 9, 11, 1, 9, 10, true)]
        [InlineData(1, 9, 10, 1, 9, 10, false)]
        [InlineData(1, 9, 9, 1, 9, 10, false)]
        [InlineData(1, 8, 20, 1, 9, 0, false)]
        [InlineData(0, 99, 99, 1, 0, 0, false)]
        public void Newer_ComparesMajorThenMinorThenPatch(int a1, int a2, int a3, int b1, int b2, int b3, bool expected)
        {
            Assert.Equal(expected, V(a1, a2, a3).Newer(V(b1, b2, b3)));
        }

        [Fact]
        public void Equal_Set_And_ToString()
        {
            var v = V(1, 9, 10);
            Assert.True(v.Equal(V(1, 9, 10)));
            Assert.False(v.Equal(V(1, 9, 11)));
            v.Set(V(2, 0, 1));
            Assert.Equal("v2.0.1", v.ToString());
        }
    }

    /// <summary>The alarm list is shared static state that the timing routines read, so each test clears it.</summary>
    public class AlarmListTests : DatabaseTestBase
    {
        public AlarmListTests() => Alarm.ClearAlarms();

        [Fact]
        public void AddAlarm_IsFoundByBibAndChip()
        {
            Alarm.AddAlarm(new Alarm(1, "12", "", true, 1));
            Alarm.AddAlarm(new Alarm(2, "", "CHIP9", true, 2));

            Assert.Equal(1, Alarm.GetAlarmByBib("12")!.Identifier);
            Assert.Equal(2, Alarm.GetAlarmByChip("CHIP9")!.Identifier);
            Assert.Null(Alarm.GetAlarmByBib("13"));
            Assert.Equal(2, Alarm.GetAlarms().Count);
            Alarm.ClearAlarms();
        }

        [Fact]
        public void RemoveAlarm_RemovesItEverywhere()
        {
            var bibAlarm = new Alarm(1, "12", "", true, 1);
            var chipAlarm = new Alarm(2, "", "CHIP12", true, 1);
            Alarm.AddAlarm(bibAlarm);
            Alarm.AddAlarm(chipAlarm);

            Assert.True(Alarm.RemoveAlarm(bibAlarm));
            Assert.True(Alarm.RemoveAlarm(chipAlarm));

            Assert.Null(Alarm.GetAlarmByBib("12"));
            Assert.Null(Alarm.GetAlarmByChip("CHIP12"));
            Assert.Empty(Alarm.GetAlarms());
            var (bibs, chips) = Alarm.GetAlarmDictionaries();
            Assert.Empty(bibs);
            Assert.Empty(chips);
            Assert.False(Alarm.RemoveAlarm(bibAlarm));   // already gone
            Alarm.ClearAlarms();
        }

        [Fact]
        public void RemoveAlarm_UnsavedAlarms_OnlyRemovesTheMatchingOne()
        {
            Alarm.AddAlarm(new Alarm(-1, "12", "", true, 1));
            Alarm.AddAlarm(new Alarm(-1, "13", "", true, 1));

            Alarm.RemoveAlarm(new Alarm(-1, "13", "", true, 1));

            Assert.Equal("12", Assert.Single(Alarm.GetAlarms()).Bib);
            Assert.NotNull(Alarm.GetAlarmByBib("12"));
            Alarm.ClearAlarms();
        }

        [Fact]
        public void AddAlarms_ListsBibAndChipAlarms()
        {
            // The alarm editor only keeps the chip when there's no bib, so an alarm has one or the other.
            Alarm.AddAlarms([new Alarm(1, "12", "", true, 1), new Alarm(2, "", "CHIP13", true, 1)]);
            Assert.Equal(2, Alarm.GetAlarms().Count);
            Assert.NotNull(Alarm.GetAlarmByChip("CHIP13"));
            Alarm.ClearAlarms();
        }

        [Fact]
        public void GetAlarmDictionaries_ReturnsCopies()
        {
            Alarm.AddAlarm(new Alarm(1, "12", "", true, 1));
            var (bibs, _) = Alarm.GetAlarmDictionaries();
            bibs.Clear();
            Assert.NotNull(Alarm.GetAlarmByBib("12"));
            Alarm.ClearAlarms();
        }

        [Fact]
        public void SaveAlarms_PersistsTheListAndReloadsIt()
        {
            var e = AddEvent();
            Alarm.AddAlarm(new Alarm(-1, "12", "", true, 3));
            Alarm.AddAlarm(new Alarm(-1, "13", "", false, 1));

            Alarm.SaveAlarms(e.Identifier, Db);

            Assert.Equal(["12", "13"], Db.GetAlarms(e.Identifier).Select(a => a.Bib).Order());
            Assert.All(Alarm.GetAlarms(), a => Assert.True(a.Identifier > 0));
            Alarm.ClearAlarms();
        }
    }

    public class TimingSystemTests
    {
        [Theory]
        [InlineData(Constants.Readers.SYSTEM_RFID, Constants.Readers.RFID_DEFAULT_PORT)]
        [InlineData(Constants.Readers.SYSTEM_IPICO, Constants.Readers.IPICO_DEFAULT_PORT)]
        [InlineData(Constants.Readers.SYSTEM_IPICO_LITE, Constants.Readers.IPICO_DEFAULT_PORT)]
        [InlineData(Constants.Readers.SYSTEM_CHRONOKEEP_PORTAL, Constants.Network.CHRONOKEEP_ZCONF_PORT)]
        public void NewSystem_UsesTypeDefaultPort(string type, int port)
        {
            var sys = new TimingSystem("10.0.0.5", type);
            Assert.Equal(port, sys.Port);
            Assert.Equal(SYSTEM_STATUS.DISCONNECTED, sys.Status);
            Assert.False(sys.Saved());
        }

        [Fact]
        public void UpdateSystemType_ChangesPort()
        {
            var sys = new TimingSystem("10.0.0.5", Constants.Readers.SYSTEM_RFID);
            sys.UpdateSystemType(Constants.Readers.SYSTEM_IPICO);
            Assert.Equal(Constants.Readers.SYSTEM_IPICO, sys.Type);
            Assert.Equal(Constants.Readers.IPICO_DEFAULT_PORT, sys.Port);
        }

        [Fact]
        public void CopyFrom_And_Equals()
        {
            var a = new TimingSystem(1, "10.0.0.5", 23, TimingConstants.LOCATION_FINISH, Constants.Readers.SYSTEM_RFID);
            var b = new TimingSystem("x", Constants.Readers.SYSTEM_IPICO);
            b.CopyFrom(a);
            Assert.True(a.Equals(b));
            Assert.True(a.Equals(new TimingSystem(9, " 10.0.0.5 ", 23, TimingConstants.LOCATION_FINISH, Constants.Readers.SYSTEM_RFID)));
            Assert.False(a.Equals(new TimingSystem(1, "10.0.0.5", 24, TimingConstants.LOCATION_FINISH, Constants.Readers.SYSTEM_RFID)));
            Assert.True(a.Saved());
        }
    }
}
