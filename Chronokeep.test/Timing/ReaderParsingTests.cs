using Chronokeep.Interfaces.Timing;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronokeepPortal;
using Chronokeep.Test.Database;
using Chronokeep.Timing.Interfaces;
using System.Net.Sockets;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Timing
{
    /// <summary>
    /// Feeds raw reader text into each reader interface and checks what it reports and saves.
    /// The interfaces capture the current event when they're created, so the event is set up first.
    /// </summary>
    public abstract class ReaderTestBase : DatabaseTestBase
    {
        protected const int Location = TimingConstants.LOCATION_FINISH;
        internal Event TheEvent { get; }
        internal IMainWindow Window { get; }
        internal FakeMainWindow FakeWindow { get; }
        // Only used as a key for per-socket buffers; never connected.
        protected readonly Socket Sock = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        protected ReaderTestBase()
        {
            TheEvent = AddEvent(makeCurrent: true);
            Window = FakeMainWindow.Create(out FakeMainWindow fake);
            FakeWindow = fake;
        }

        internal List<ChipRead> SavedReads() => Db.GetChipReadsSafemode(TheEvent.Identifier);

        internal void IgnoreChip(string chip)
            => Db.AddBibChipAssociation(-1, [new BibChipAssociation { EventId = -1, Bib = "", Chip = chip }]);
    }

    public class IpicoParsingTests : ReaderTestBase
    {
        private IpicoInterface Reader() => new(Db, Location, Constants.Readers.SYSTEM_IPICO, Window);

        // aa | reader 2 | tag 12 | I 2 | Q 2 | yyMMdd | HHmmss | hundredths (hex) 2 | checksum 2
        private static string Line(string tag = "058000123456", string reader = "01", string hundredths = "0a", string suffix = "")
            => $"aa{reader}{tag}0000260615080000{hundredths}ff{suffix}\n";

        [Fact]
        public void ChipRead_IsSavedWithTimeAndReader()
        {
            var output = Reader().ParseMessages(Line(), Sock);

            Assert.True(output.ContainsKey(MessageType.CHIPREAD));
            var read = Assert.Single(SavedReads());
            Assert.Equal("058000123456", read.ChipNumber);
            Assert.Equal(new DateTime(2026, 6, 15, 8, 0, 0, 100), read.Time);   // 0x0a hundredths = 100ms
            Assert.Equal(1, read.Antenna);
            Assert.Equal(Location, read.LocationId);
            Assert.Equal(TimingConstants.CHIPREAD_STATUS_NONE, read.Status);
        }

        [Fact]
        public void LiveRead_IsNotMarkedAsRewind()
        {
            Reader().ParseMessages(Line(), Sock);
            Assert.Equal(0, Assert.Single(SavedReads()).IsRewind);
        }

        [Fact]
        public void LiveReadWithWindowsLineEnding_IsNotMarkedAsRewind()
        {
            Reader().ParseMessages(Line().Replace("\n", "\r\n"), Sock);
            Assert.Equal(0, Assert.Single(SavedReads()).IsRewind);
        }

        [Fact]
        public void LongerLine_IsMarkedAsRewind()
        {
            Reader().ParseMessages(Line(suffix: "FS"), Sock);
            Assert.Equal(1, Assert.Single(SavedReads()).IsRewind);
        }

        [Fact]
        public void ReaderIdIsHex()
        {
            Reader().ParseMessages(Line(reader: "0a"), Sock);
            Assert.Equal(10, Assert.Single(SavedReads()).Antenna);
        }

        [Fact]
        public void SeveralLinesInOneMessage_AreAllSaved()
        {
            Reader().ParseMessages(Line("058000000001") + Line("058000000002") + Line("058000000003"), Sock);
            Assert.Equal(3, SavedReads().Count);
        }

        [Fact]
        public void LineSplitAcrossMessages_IsSavedOnceComplete()
        {
            var reader = Reader();
            string line = Line();
            Assert.Empty(reader.ParseMessages(line[..10], Sock));
            Assert.Empty(SavedReads());

            reader.ParseMessages(line[10..], Sock);
            Assert.Single(SavedReads());
        }

        [Fact]
        public void IgnoredChip_IsNotSaved()
        {
            IgnoreChip("058000123456");
            var output = Reader().ParseMessages(Line(), Sock);
            Assert.Empty(SavedReads());
            Assert.False(output.ContainsKey(MessageType.CHIPREAD));
        }

        [Fact]
        public void DnsMode_MarksReadsDns()
        {
            FakeWindow.DnsMode = true;
            Reader().ParseMessages(Line(), Sock);
            Assert.Equal(TimingConstants.CHIPREAD_STATUS_DNS, Assert.Single(SavedReads()).Status);
        }

        [Theory]
        [InlineData("date.Wed Nov 12 15:30:30 CST 2008\n", "12 Nov 2008  15:30:30")]
        [InlineData("date.Wed Nov  5 15:30:30 CST 2008\n", "05 Nov 2008  15:30:30")]
        public void TimeMessage_IsReported(string message, string expected)
        {
            var output = Reader().ParseMessages(message, Sock);
            Assert.Equal(expected, Assert.Single(output[MessageType.TIME]));
        }

        [Fact]
        public void UnrecognizedLine_IsIgnored()
        {
            Assert.Empty(Reader().ParseMessages("hello reader\n", Sock));
            Assert.Empty(SavedReads());
        }

        [Fact]
        public void NoSocket_ReturnsNothing()
        {
            Assert.Empty(Reader().ParseMessages(Line(), null));
            Assert.Empty(SavedReads());
        }
    }

    public class RfidUltraParsingTests : ReaderTestBase
    {
        private RfidUltraInterface Reader() => new(Db, Location, Window);

        private static readonly long Seconds = TimingConstants.RfidDateToEpoch(new DateTime(2026, 6, 15, 8, 0, 0));

        // 0,chip,seconds,ms,antenna,rssi,isRewind,reader,box,readerTime,startTime,logIndex
        private static string Line(string chip = "1001", int ms = 250, int rewind = 0)
            => $"0,{chip},{Seconds},{ms},2,-45,{rewind},1,BOX1,8:00:00.250 15-6-2026,0,17\n";

        [Fact]
        public void ChipRead_IsSavedWithAllFields()
        {
            var output = Reader().ParseMessages(Line(), Sock);

            Assert.True(output.ContainsKey(MessageType.CHIPREAD));
            var read = Assert.Single(SavedReads());
            Assert.Equal("1001", read.ChipNumber);
            Assert.Equal(Seconds, read.TimeSeconds);
            Assert.Equal(250, read.TimeMilliseconds);
            Assert.Equal(new DateTime(2026, 6, 15, 8, 0, 0, 250), read.Time);
            Assert.Equal(2, read.Antenna);
            Assert.Equal("-45", read.Rssi);
            Assert.Equal("1", read.Reader);
            Assert.Equal("BOX1", read.Box);
            Assert.Equal(17, read.LogId);
        }

        [Fact]
        public void WindowsLineEndings_AreHandled()
        {
            Reader().ParseMessages(Line().Replace("\n", "\r\n"), Sock);
            Assert.Equal(17, Assert.Single(SavedReads()).LogId);
        }

        [Fact]
        public void LineSplitAcrossMessages_IsSavedOnceComplete()
        {
            var reader = Reader();
            string line = Line();
            reader.ParseMessages(line[..15], Sock);
            Assert.Empty(SavedReads());
            reader.ParseMessages(line[15..], Sock);
            Assert.Single(SavedReads());
        }

        [Fact]
        public void IgnoredChip_And_DnsMode()
        {
            IgnoreChip("1001");
            FakeWindow.DnsMode = true;
            Reader().ParseMessages(Line("1001") + Line("1002"), Sock);

            var read = Assert.Single(SavedReads());
            Assert.Equal("1002", read.ChipNumber);
            Assert.Equal(TimingConstants.CHIPREAD_STATUS_DNS, read.Status);
        }

        [Theory]
        [InlineData("V=24.8\n", MessageType.VOLTAGENORMAL)]
        [InlineData("V=22.1\n", MessageType.VOLTAGELOW)]
        [InlineData("Connected,0\n", MessageType.CONNECTED)]
        [InlineData("what is this\n", MessageType.UNKNOWN)]
        public void StatusLines_AreClassified(string message, MessageType expected)
        {
            Assert.True(Reader().ParseMessages(message, Sock).ContainsKey(expected));
        }

        [Fact]
        public void BadVoltage_IsReportedAsError()
        {
            var output = Reader().ParseMessages("V=abc\n", Sock);
            Assert.True(output.ContainsKey(MessageType.ERROR));
        }

        [Fact]
        public void TimeMessage_IsReported()
        {
            var output = Reader().ParseMessages("8:5:3 15-6-2026 (0)\n", Sock);
            Assert.Equal(new DateTime(2026, 6, 15, 8, 5, 3).ToString("dd MMM yyyy HH:mm:ss"), Assert.Single(output[MessageType.TIME]));
        }

        [Theory]
        [InlineData("S=00\n", TimingSystem.READING_STATUS_STOPPED)]
        [InlineData("S=10\n", TimingSystem.READING_STATUS_READING)]
        [InlineData("S=90\n", TimingSystem.READING_STATUS_UNKNOWN)]
        public void StatusMessage_IsReported(string message, string expected)
        {
            Assert.Equal(expected, Assert.Single(Reader().ParseMessages(message, Sock)[MessageType.STATUS]));
        }
    }

    public class ChronokeepPortalParsingTests : ReaderTestBase
    {
        private ChronokeepInterface Reader() => new(Db, Location, Window);

        private static readonly long UnixSeconds = TimingConstants.UnixDateToEpoch(new DateTime(2026, 6, 15, 15, 0, 0));

        private static string Read(string identifier, string identType = PortalRead.READ_IDENT_TYPE_CHIP, string kind = PortalRead.READ_KIND_CHIP)
            => $"{{\"identifier\":\"{identifier}\",\"seconds\":{UnixSeconds},\"milliseconds\":250,\"reader_seconds\":{UnixSeconds}," +
               $"\"reader_milliseconds\":250,\"antenna\":3,\"reader\":\"R1\",\"rssi\":\"-50\",\"ident_type\":\"{identType}\",\"type\":\"{kind}\"}}";

        private static string Reads(params string[] reads) => $"{{\"command\":\"reads\",\"list\":[{string.Join(",", reads)}]}}\n";

        [Fact]
        public void ChipRead_IsSavedInLocalRfidTime()
        {
            var output = Reader().ParseMessages(Reads(Read("CHIP1")), Sock);

            Assert.True(output.ContainsKey(MessageType.CHIPREAD));
            var read = Assert.Single(SavedReads());
            Assert.Equal("CHIP1", read.ChipNumber);
            Assert.Equal(TimingConstants.UtcSecondsToRfidSeconds(UnixSeconds), read.TimeSeconds);
            Assert.Equal(250, read.TimeMilliseconds);
            Assert.Equal(3, read.Antenna);
            Assert.Equal("R1", read.Reader);
            Assert.Equal(TimingConstants.CHIPREAD_TYPE_CHIP, read.Type);
        }

        [Fact]
        public void BibRead_IsSavedAsManualBibRead()
        {
            Reader().ParseMessages(Reads(Read("42", PortalRead.READ_IDENT_TYPE_BIB, PortalRead.READ_KIND_MANUAL)), Sock);

            var read = Assert.Single(SavedReads());
            Assert.Equal(TimingConstants.CHIPREAD_DUMMYCHIP, read.ChipNumber);
            Assert.Equal("42", read.ReadBib);
            Assert.Equal(TimingConstants.CHIPREAD_TYPE_MANUAL, read.Type);
        }

        [Fact]
        public void IgnoredChips_AreSkipped_ButBibReadsAreNot()
        {
            IgnoreChip("CHIP1");
            Reader().ParseMessages(Reads(Read("CHIP1"), Read("CHIP2"), Read("CHIP1", PortalRead.READ_IDENT_TYPE_BIB)), Sock);
            Assert.Equal(2, SavedReads().Count);
        }

        [Fact]
        public void DnsMode_MarksReadsDns()
        {
            FakeWindow.DnsMode = true;
            Reader().ParseMessages(Reads(Read("CHIP1")), Sock);
            Assert.Equal(TimingConstants.CHIPREAD_STATUS_DNS, Assert.Single(SavedReads()).Status);
        }

        [Fact]
        public void MessageSplitAcrossCalls_IsHandledOnceComplete()
        {
            var reader = Reader();
            string message = Reads(Read("CHIP1"));
            reader.ParseMessages(message[..30], Sock);
            Assert.Empty(SavedReads());
            reader.ParseMessages(message[30..], Sock);
            Assert.Single(SavedReads());
        }

        [Fact]
        public void EmptyReadList_ReportsNothing()
        {
            Assert.False(Reader().ParseMessages(Reads(), Sock).ContainsKey(MessageType.CHIPREAD));
        }

        [Theory]
        [InlineData(new[] { false, false }, TimingSystem.READING_STATUS_STOPPED)]
        [InlineData(new[] { true, true }, TimingSystem.READING_STATUS_READING)]
        [InlineData(new[] { true, false }, TimingSystem.READING_STATUS_PARTIAL)]
        public void ConnectionSuccessful_ReportsConnectedAndReadingStatus(bool[] reading, string expected)
        {
            string readers = string.Join(",", reading.Select((r, i) => $"{{\"id\":{i},\"name\":\"R{i}\",\"reading\":{r.ToString().ToLower()}}}"));
            var output = Reader().ParseMessages($"{{\"command\":\"connection_successful\",\"name\":\"Portal\",\"readers\":[{readers}]}}\n", Sock);

            Assert.True(output.ContainsKey(MessageType.CONNECTED));
            Assert.Equal(expected, Assert.Single(output[MessageType.STATUS]));
        }

        [Fact]
        public void TimeMessage_IsReported()
        {
            var output = Reader().ParseMessages("{\"command\":\"time\",\"local\":\"2026-06-15 08:05:03\",\"utc\":\"\"}\n", Sock);
            Assert.Equal(new DateTime(2026, 6, 15, 8, 5, 3).ToString("dd MMM yyyy  HH:mm:ss"), Assert.Single(output[MessageType.TIME]));
        }

        [Theory]
        [InlineData("{\"command\":\"success\",\"count\":1}\n", MessageType.SUCCESS)]
        [InlineData("{\"command\":\"disconnect\"}\n", MessageType.DISCONNECT)]
        [InlineData("{\"command\":\"something_new\"}\n", MessageType.UNKNOWN)]
        public void OtherCommands_AreClassified(string message, MessageType expected)
        {
            Assert.True(Reader().ParseMessages(message, Sock).ContainsKey(expected));
        }

        [Fact]
        public void InvalidJson_IsSkipped_AndLaterMessagesStillWork()
        {
            var output = Reader().ParseMessages("not json\n" + Reads(Read("CHIP1")), Sock);
            Assert.True(output.ContainsKey(MessageType.CHIPREAD));
            Assert.Single(SavedReads());
        }
    }
}
