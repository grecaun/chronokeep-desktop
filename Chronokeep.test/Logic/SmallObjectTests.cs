using Chronokeep.Objects;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Logic
{
    public class ChipReadLogicTests
    {
        private static ChipRead Read(string chip, int second = 0, int status = TimingConstants.CHIPREAD_STATUS_NONE)
        {
            var r = new ChipRead(1, TimingConstants.LOCATION_FINISH, chip, new DateTime(2026, 6, 15, 8, 0, 0).AddSeconds(second));
            r.Status = status;
            return r;
        }

        [Fact]
        public void Time_ComesFromRfidSeconds()
        {
            var when = new DateTime(2026, 6, 15, 8, 0, 0, 250);
            var r = new ChipRead(1, TimingConstants.LOCATION_FINISH, " ABC ", when);
            Assert.Equal(when, r.Time);
            Assert.Equal("ABC", r.ChipNumber);
            Assert.Equal("2026-06-15 08:00:00.250", r.TimeString);
        }

        [Fact]
        public void Bib_UsesChipBibForChipReads_AndReadBibForManualReads()
        {
            var chip = Read("A");
            chip.ChipBib = "5";
            Assert.Equal("5", chip.Bib);

            var manual = new ChipRead(1, TimingConstants.LOCATION_FINISH, "77", new DateTime(2026, 6, 15), TimingConstants.CHIPREAD_STATUS_NONE);
            Assert.Equal("77", manual.Bib);
        }

        [Theory]
        [InlineData(TimingConstants.CHIPREAD_STATUS_NONE, true, false, true)]
        [InlineData(TimingConstants.CHIPREAD_STATUS_USED, true, false, true)]
        [InlineData(TimingConstants.CHIPREAD_STATUS_STARTTIME, true, false, true)]
        [InlineData(TimingConstants.CHIPREAD_STATUS_DNF, true, false, false)]
        [InlineData(TimingConstants.CHIPREAD_STATUS_DNS, true, false, false)]
        [InlineData(TimingConstants.CHIPREAD_STATUS_IGNORE, false, true, false)]
        [InlineData(TimingConstants.CHIPREAD_STATUS_DNF_IGNORE, false, true, false)]
        [InlineData(TimingConstants.CHIPREAD_STATUS_DNS_IGNORE, false, true, false)]
        [InlineData(TimingConstants.CHIPREAD_STATUS_OVERMAX, false, false, true)]
        public void StatusPredicates(int status, bool useful, bool ignored, bool resettable)
        {
            var r = Read("A", status: status);
            Assert.Equal(useful, r.IsUseful());
            Assert.Equal(ignored, r.IsIgnored());
            Assert.Equal(resettable, r.CanBeReset());
        }

        [Fact]
        public void Sort_OrdersByTime()
        {
            var list = new List<ChipRead> { Read("late", 30), Read("early", 10), Read("mid", 20) };
            list.Sort();
            Assert.Equal(["early", "mid", "late"], list.Select(r => r.ChipNumber));
        }

        [Fact]
        public void CompareByBib_NumericThenTime()
        {
            var a = Read("A", 20); a.ChipBib = "10";
            var b = Read("B", 10); b.ChipBib = "9";
            var c = Read("C", 5); c.ChipBib = "10";
            var list = new List<ChipRead> { a, b, c };
            list.Sort(ChipRead.CompareByBib);
            Assert.Equal(["B", "C", "A"], list.Select(r => r.ChipNumber));
        }

        [Fact]
        public void IsNotMatch_SearchesBibNameAndChip()
        {
            var r = Read("ABC123");
            r.ChipBib = "42";
            r.Name = "Jane Runner";
            Assert.False(r.IsNotMatch("abc"));
            Assert.False(r.IsNotMatch("42"));
            Assert.False(r.IsNotMatch("runner"));
            Assert.True(r.IsNotMatch("zzz"));
        }
    }

    public class EventLogicTests
    {
        private static string D(int y, int m, int d) => new DateTime(y, m, d).ToShortDateString();

        [Fact]
        public void Sort_PutsNewestEventFirst()
        {
            var list = new List<Event>
            {
                new() { Name = "Old", Date = D(2024, 1, 1) },
                new() { Name = "New", Date = D(2026, 1, 1) },
                new() { Name = "Mid", Date = D(2025, 1, 1) },
            };
            list.Sort();
            Assert.Equal(["New", "Mid", "Old"], list.Select(e => e.Name));
        }

        [Fact]
        public void Equals_MatchesNameAndDateOrIdentifier()
        {
            var e = new Event { Identifier = 1, Name = "Race", Date = D(2026, 1, 1) };
            Assert.True(e.Equals(new Event { Identifier = 2, Name = "Race", Date = D(2026, 1, 1) }));
            Assert.True(e.Equals(new Event { Identifier = 1, Name = "Other", Date = D(2025, 1, 1) }));
            Assert.False(e.Equals(new Event { Identifier = 2, Name = "Other", Date = D(2026, 1, 1) }));
            Assert.False(e.Equals(null));
        }

        [Theory]
        [InlineData(TimingConstants.EVENT_TYPE_DISTANCE, "Distance Based")]
        [InlineData(TimingConstants.EVENT_TYPE_TIME, "Time Based")]
        [InlineData(TimingConstants.EVENT_TYPE_BACKYARD_ULTRA, "Backyard Ultra")]
        public void EventTypeString_NamesType(int type, string expected)
        {
            Assert.Equal(expected, new Event { EventType = type }.EventTypeString);
        }

        [Fact]
        public void CopyAll_CopiesEveryEditableField()
        {
            var source = new Event
            {
                Name = "Copied", Date = D(2026, 7, 4), YearCode = "26", RankedBy = RankingType.Mixed,
                CommonAgeGroups = false, CommonStartFinish = false, DistanceSpecificSegments = true,
                StartWindow = 5, FinishMaxOccurrences = 2, FinishIgnoreWithin = 3, StartSeconds = 100,
                StartMaxOccurrences = 4, StartMilliseconds = 50, EventType = TimingConstants.EVENT_TYPE_TIME,
                ApiId = 9, ApiEventId = "api", DisplayPlacements = false, UploadSpecific = true, UseMaleFemale = true,
            };
            var target = new Event { Identifier = 42 };

            target.CopyAll(source);

            Assert.Equal(42, target.Identifier);
            Assert.Equal("Copied", target.Name);
            Assert.Equal(source.Date, target.Date);
            Assert.Equal("26", target.YearCode);
            Assert.Equal(RankingType.Mixed, target.RankedBy);
            Assert.False(target.CommonAgeGroups);
            Assert.False(target.CommonStartFinish);
            Assert.True(target.DistanceSpecificSegments);
            Assert.Equal(100, target.StartSeconds);
            Assert.Equal(50, target.StartMilliseconds);
            Assert.Equal(4, target.StartMaxOccurrences);
            Assert.Equal(TimingConstants.EVENT_TYPE_TIME, target.EventType);
            Assert.Equal(9, target.ApiId);
            Assert.Equal("api", target.ApiEventId);
            Assert.False(target.DisplayPlacements);
            Assert.True(target.UploadSpecific);
            Assert.True(target.UseMaleFemale);
        }

        [Fact]
        public void CopyFrom_CopiesSettingsButNotIdentity()
        {
            var source = new Event { Name = "Source", Date = D(2026, 7, 4), StartWindow = 5, EventType = TimingConstants.EVENT_TYPE_TIME };
            var target = new Event { Name = "Target", Date = D(2025, 1, 1) };
            target.CopyFrom(source);
            Assert.Equal("Target", target.Name);
            Assert.Equal(5, target.StartWindow);
            Assert.Equal(TimingConstants.EVENT_TYPE_TIME, target.EventType);
        }
    }

    public class AgeGroupLogicTests
    {
        [Theory]
        [InlineData(20, 29, "", "20-29")]
        [InlineData(0, 19, "", "Under 20")]
        [InlineData(80, 99, "", "Over 80")]
        [InlineData(80, 120, "", "Over 80")]
        [InlineData(20, 29, "Twenties", "Twenties")]
        public void PrettyName(int start, int end, string custom, string expected)
        {
            Assert.Equal(expected, new AgeGroup(1, 1, start, end, custom).PrettyName());
        }

        [Fact]
        public void Equals_ComparesEventDistanceAndAgeRange()
        {
            var g = new AgeGroup(1, 1, 20, 29);
            Assert.True(g.Equals(new AgeGroup(1, 1, 20, 29)));
            Assert.False(g.Equals(new AgeGroup(1, 1, 20, 30)));
            Assert.False(g.Equals(new AgeGroup(1, 2, 20, 29)));
            Assert.False(g.Equals(null));
        }

        [Fact]
        public void Sort_ByEventDistanceThenStartAge()
        {
            var list = new List<AgeGroup> { new(1, 2, 0, 19), new(1, 1, 30, 39), new(1, 1, 20, 29) };
            list.Sort();
            Assert.Equal([(1, 20), (1, 30), (2, 0)], list.Select(g => (g.DistanceId, g.StartAge)));
        }
    }

    public class BibChipAssociationLogicTests
    {
        [Fact]
        public void Sort_UsesNumericBibOrderWhenPossible()
        {
            var list = new List<BibChipAssociation>
            {
                new() { EventId = 1, Bib = "10" }, new() { EventId = 1, Bib = "9" }, new() { EventId = 1, Bib = "A" },
            };
            list.Sort();
            Assert.Equal(["9", "10", "A"], list.Select(b => b.Bib));
        }

        [Fact]
        public void Equals_IgnoresBibCase()
        {
            Assert.True(new BibChipAssociation { EventId = 1, Bib = "a1" }.Equals(new BibChipAssociation { EventId = 1, Bib = "A1" }));
            Assert.False(new BibChipAssociation { EventId = 1, Bib = "a1" }.Equals(new BibChipAssociation { EventId = 2, Bib = "a1" }));
        }

        [Fact]
        public void TrimFields_TrimsBibAndChip()
        {
            var b = new BibChipAssociation { Bib = " 1 ", Chip = " A " };
            b.TrimFields();
            Assert.Equal("1", b.Bib);
            Assert.Equal("A", b.Chip);
        }
    }

    public class SegmentLogicTests
    {
        [Fact]
        public void Sort_ByDistanceThenCumulativeDistance()
        {
            var list = new List<Segment>
            {
                new(1, 2, 1, 1, 1, 1.0, 0, "B-1", "", ""),
                new(1, 1, 1, 2, 1, 6.0, 0, "A-6", "", ""),
                new(1, 1, 1, 1, 1, 3.0, 0, "A-3", "", ""),
            };
            list.Sort();
            Assert.Equal(["A-3", "A-6", "B-1"], list.Select(s => s.Name));
        }

        [Fact]
        public void CopyConstructor_ClearsIdentifier()
        {
            var original = new Segment(5, 1, 1, 1, 1, 1.0, 2.0, 0, "Lap", "gps", "map");
            var copy = new Segment(original);
            Assert.Equal(-1, copy.Identifier);
            Assert.Equal("Lap", copy.Name);
            Assert.Equal(2.0, copy.CumulativeDistance);
            Assert.True(copy.Equals(original));
        }
    }
}
