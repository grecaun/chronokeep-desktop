using Chronokeep.Objects;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Logic
{
    public class TimeResultLogicTests
    {
        private static TimeResult Make(int segment = TimingConstants.SEGMENT_FINISH, int occurrence = 1, long seconds = 1800,
            int status = TimingConstants.TIMERESULT_STATUS_NONE, string bib = "1", int location = TimingConstants.LOCATION_FINISH,
            long chipSeconds = -1)
            => new(1, TimingConstants.TIMERESULT_DUMMYREAD, 10, location, segment, occurrence, seconds, 0, "",
                chipSeconds < 0 ? seconds : chipSeconds, 0, new DateTime(2026, 6, 15, 8, 0, 0).AddSeconds(seconds), bib, status, "");

        private static Event NewEvent(int type) => new() { Identifier = 1, Name = "E", Date = new DateTime(2026, 6, 15).ToShortDateString(), EventType = type };

        private static Dictionary<int, TimingLocation> Locations() => new()
        {
            [TimingConstants.LOCATION_FINISH] = new TimingLocation(TimingConstants.LOCATION_FINISH, 1, "Start/Finish", 1, 0),
            [5] = new TimingLocation(5, 1, "Mile 5", 1, 0),
        };

        private static Participant Runner(string distance = "5K")
            => new("Jane", "Runner", "", "", "", "", new DateTime(1990, 1, 1).ToShortDateString(),
                new EventSpecific(1, 1, distance, "1", 0, "", "", "", false, false, "", ""),
                "", "", "", "", "", "", "F", "", "");

        [Fact]
        public void Constructor_FormatsTimes()
        {
            var r = Make(seconds: 3725, chipSeconds: 3600);
            Assert.Equal("1:02:05.000", r.Time);
            Assert.Equal("1:00:00.000", r.ChipTime);
            Assert.Equal("1:00:00", r.ChipTimeNoMilliseconds);
        }

        [Theory]
        [InlineData(TimingConstants.TIMERESULT_STATUS_DNF, "DNF")]
        [InlineData(TimingConstants.TIMERESULT_STATUS_DNS, "DNS")]
        public void Constructor_DnfAndDnsReplaceTimes(int status, string expected)
        {
            var r = Make(status: status);
            Assert.Equal(expected, r.Time);
            Assert.Equal(expected, r.ChipTime);
            Assert.Equal(status == TimingConstants.TIMERESULT_STATUS_DNF, r.IsDnf());
        }

        [Fact]
        public void Identifiers_ArePrefixed()
        {
            Assert.Equal("Bib:12", TimeResult.BibToIdentifier("12"));
            Assert.Equal("Chip:ABC", TimeResult.ChipToIdentifier("ABC"));
        }

        [Fact]
        public void SetFinalValues_DistanceEvent_NamesFinishAndLocation()
        {
            var r = Make();
            r.SetFinalValues(Locations(), [], [], NewEvent(TimingConstants.EVENT_TYPE_DISTANCE));
            Assert.Equal("Finish", r.SegmentName);
            Assert.Equal("Start/Finish", r.LocationName);
        }

        [Fact]
        public void SetFinalValues_UnknownLocation_IsLabelledUnknown()
        {
            var r = Make(location: 99, segment: TimingConstants.SEGMENT_NONE);
            r.SetFinalValues(Locations(), [], [], NewEvent(TimingConstants.EVENT_TYPE_DISTANCE));
            Assert.Equal("Unknown", r.LocationName);
            Assert.Equal("", r.SegmentName);
        }

        [Fact]
        public void SetFinalValues_DistanceEvent_UsesSegmentName()
        {
            var r = Make(segment: 7, location: 5);
            var segs = new Dictionary<int, Segment> { [7] = new(7, 1, 1, 5, 1, 5, 5, Constants.Distances.MILES, "Mile 5", "", "") };
            r.SetFinalValues(Locations(), segs, [], NewEvent(TimingConstants.EVENT_TYPE_DISTANCE));
            Assert.Equal("Mile 5", r.SegmentName);
        }

        [Fact]
        public void SetFinalValues_TimeEvent_LabelsLapWithCumulativeDistance()
        {
            var r = Make(occurrence: 2);
            r.SetParticipant(Runner("Loop"));
            var dists = new Dictionary<string, Distance> { ["Loop"] = new("Loop", 1) { DistanceValue = 3.1, DistanceUnit = Constants.Distances.MILES } };
            r.SetFinalValues(Locations(), [], dists, NewEvent(TimingConstants.EVENT_TYPE_TIME));
            Assert.Equal("6.2 Miles - Lap 2", r.SegmentName);
        }

        [Fact]
        public void SetFinalValues_TimeEvent_WithoutDistance_IsJustLapNumber()
        {
            var r = Make(occurrence: 3);
            r.SetFinalValues(Locations(), [], [], NewEvent(TimingConstants.EVENT_TYPE_TIME));
            Assert.Equal("Lap 3", r.SegmentName);
        }

        [Theory]
        [InlineData(TimingConstants.SEGMENT_FINISH, 1, "Hour 1")]
        [InlineData(TimingConstants.SEGMENT_FINISH, 3, "Hour 2")]
        [InlineData(TimingConstants.SEGMENT_START, 2, "Start 2")]
        public void SetFinalValues_BackyardUltra_LabelsHours(int segment, int occurrence, string expected)
        {
            var r = Make(segment: segment, occurrence: occurrence);
            r.SetFinalValues(Locations(), [], [], NewEvent(TimingConstants.EVENT_TYPE_BACKYARD_ULTRA));
            Assert.Equal(expected, r.SegmentName);
        }

        [Fact]
        public void SetFinalValues_BackyardUltra_WithDistance_ShowsCumulativeDistance()
        {
            var r = Make(occurrence: 3);
            r.SetParticipant(Runner("Backyard"));
            var dists = new Dictionary<string, Distance> { ["Backyard"] = new("Backyard", 1) { DistanceValue = 4.167, DistanceUnit = Constants.Distances.MILES } };
            r.SetFinalValues(Locations(), [], dists, NewEvent(TimingConstants.EVENT_TYPE_BACKYARD_ULTRA));
            Assert.Equal("8.33 Miles - Hour 2", r.SegmentName);
        }

        [Fact]
        public void SetParticipant_And_SetBlankParticipant()
        {
            var r = Make();
            r.SetParticipant(Runner());
            Assert.Equal("Jane Runner", r.ParticipantName);
            Assert.Equal("5K", r.DistanceName);

            r.SetLinkedDistanceName("Main");
            Assert.Equal("Main", r.DistanceName);

            r.SetBlankParticipant();
            Assert.Equal("", r.ParticipantName);
            Assert.Equal("", r.DistanceName);
        }

        [Theory]
        [InlineData(TimingConstants.DISTANCE_TYPE_EARLY, "3e")]
        [InlineData(TimingConstants.DISTANCE_TYPE_UNOFFICIAL, "3u")]
        [InlineData(TimingConstants.DISTANCE_TYPE_DROP, "3d")]
        [InlineData(TimingConstants.DISTANCE_TYPE_LATE, "3l")]
        [InlineData(TimingConstants.DISTANCE_TYPE_NORMAL, "3")]
        public void PrettyPlaceStr_MarksDistanceType(int type, string expected)
        {
            var r = Make();
            r.SetResultType(type);
            r.Place = 3;
            Assert.Equal(expected, r.PrettyPlaceStr);
        }

        [Fact]
        public void PrettyPlaceStr_NoPlace_IsEmpty()
        {
            var r = Make();
            r.Place = -1;
            Assert.Equal("", r.PrettyPlaceStr);
        }

        [Fact]
        public void PlaceStrings_HiddenWhenEventHidesPlacements()
        {
            var r = Make();
            r.Place = 2;
            var e = NewEvent(TimingConstants.EVENT_TYPE_DISTANCE);
            e.DisplayPlacements = false;
            r.SetFinalValues(Locations(), [], [], e);
            Assert.Equal("", r.PlaceStr);

            e.DisplayPlacements = true;
            Assert.Equal("2", r.PlaceStr);
        }

        [Fact]
        public void Uploaded_StoresOnlyTrueOrFalse()
        {
            var r = Make();
            Assert.False(r.IsUploaded());
            r.Uploaded = 5;
            Assert.Equal(TimingConstants.TIMERESULT_UPLOADED_TRUE, r.Uploaded);
            Assert.True(r.IsUploaded());
        }

        [Fact]
        public void CompareByDistancePlace_PutsHigherOccurrenceFirstAndDnfLast()
        {
            var dnf = Make(status: TimingConstants.TIMERESULT_STATUS_DNF, bib: "dnf");
            var second = Make(bib: "second"); second.Place = 2;
            var first = Make(bib: "first"); first.Place = 1;
            var extraLap = Make(occurrence: 2, bib: "lap2"); extraLap.Place = 5;
            var list = new List<TimeResult> { dnf, second, first, extraLap };

            list.Sort(TimeResult.CompareByDistancePlace);

            Assert.Equal(["lap2", "first", "second", "dnf"], list.Select(r => r.Bib));
        }

        [Fact]
        public void CompareByBib_UsesNumericOrderWhenPossible()
        {
            var list = new List<TimeResult> { Make(bib: "10"), Make(bib: "9"), Make(bib: "A1") };
            list.Sort(TimeResult.CompareByBib);
            Assert.Equal(["9", "10", "A1"], list.Select(r => r.Bib));
        }

        [Fact]
        public void CompareByGunAndNetTime()
        {
            var fastGun = Make(seconds: 100, chipSeconds: 90, bib: "a");
            var fastChip = Make(seconds: 110, chipSeconds: 80, bib: "b");
            Assert.True(TimeResult.CompareByGunTime(fastGun, fastChip) < 0);
            Assert.True(TimeResult.CompareByNetTime(fastChip, fastGun) < 0);
        }

        [Fact]
        public void Predicates_ClassifyResults()
        {
            var start = Make(segment: TimingConstants.SEGMENT_START);
            var finish = Make();
            Assert.False(TimeResult.IsNotStart(start));
            Assert.True(TimeResult.IsNotStart(finish));
            Assert.False(TimeResult.IsNotFinish(finish));
            Assert.True(TimeResult.IsKnown(finish));
            Assert.True(TimeResult.StartTimes(start));
            Assert.False(TimeResult.StartTimes(finish));
        }

        [Theory]
        [InlineData("Man", "M")]
        [InlineData("Male", "M")]
        [InlineData("Woman", "W")]
        [InlineData("Female", "F")]
        [InlineData("Non-Binary", "X")]
        [InlineData("Not Specified", "")]
        [InlineData("ns", "")]
        [InlineData("Two Spirit", "Tw")]
        public void PrettyGender_Abbreviates(string gender, string expected)
        {
            var r = Make();
            r.SetParticipant(new Participant("A", "B", "", "", "", "", "",
                new EventSpecific(1, 1, "5K", "1", 0, "", "", "", false, false, "", ""), "", "", "", "", "", "", gender, "", ""));
            Assert.Equal(expected, r.PrettyGender);
        }

        [Theory]
        [InlineData("0-19", "Under 20")]
        [InlineData("20-29", "20-29")]
        [InlineData("Over 80", "Over 80")]
        public void AgeGroupName_IsPrettified(string raw, string expected)
        {
            var r = Make();
            r.AgeGroupName = raw;
            Assert.Equal(expected, r.AgeGroupName);
        }
    }
}
