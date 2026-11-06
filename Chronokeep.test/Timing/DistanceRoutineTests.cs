using Chronokeep.Objects;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Timing
{
    public class DistanceRoutineReadTests : RoutineTestBase
    {
        [Fact]
        public void FinishWithoutStartRead_UsesGunTimeForBoth()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1");
            var finish = ReadAt(Chip("1"), 1500);

            ProcessDistance();

            var result = FinishFor("1");
            Assert.Equal("0:25:00.000", result.Time);
            Assert.Equal("0:25:00.000", result.ChipTime);
            Assert.Equal(TimingConstants.CHIPREAD_STATUS_USED, ReadById(finish.ReadId).Status);
            Assert.Equal(TimingConstants.EVENTSPECIFIC_FINISHED, Runner("1").Status);
        }

        [Fact]
        public void StartReadInWindow_CreatesStartResultAndChipTime()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1");
            var start = ReadAt(Chip("1"), 10);
            ReadAt(Chip("1"), 1500);

            ProcessDistance();

            var startResult = Assert.Single(Results(), r => r.SegmentId == TimingConstants.SEGMENT_START);
            Assert.Equal("0:00:10.000", startResult.Time);
            Assert.Equal(TimingConstants.CHIPREAD_STATUS_STARTTIME, ReadById(start.ReadId).Status);
            var finish = FinishFor("1");
            Assert.Equal("0:25:00.000", finish.Time);
            Assert.Equal("0:24:50.000", finish.ChipTime);
        }

        [Fact]
        public void StartOnly_MarksRunnerStarted()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1");
            ReadAt(Chip("1"), 10);

            ProcessDistance();

            Assert.Equal(TimingConstants.EVENTSPECIFIC_STARTED, Runner("1").Status);
        }

        [Fact]
        public void LastReadInStartWindow_IsTheStartTime()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1");
            var early = ReadAt(Chip("1"), 5);
            ReadAt(Chip("1"), 30);
            ReadAt(Chip("1"), 1500);

            ProcessDistance();

            Assert.Equal(TimingConstants.CHIPREAD_STATUS_UNUSEDSTART, ReadById(early.ReadId).Status);
            Assert.Equal("0:24:30.000", FinishFor("1").ChipTime);
        }

        [Fact]
        public void ReadBeforeGun_IsPrestart()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1");
            var read = ReadAt(Chip("1"), -30);

            ProcessDistance();

            Assert.Equal(TimingConstants.CHIPREAD_STATUS_PRESTART, ReadById(read.ReadId).Status);
            Assert.Empty(Results());
        }

        [Fact]
        public void ReadAfterStartWindowButWithinIgnorePeriod_IsIgnored()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1");
            var read = ReadAt(Chip("1"), 90); // window is 60s, ignore-within is 120s

            ProcessDistance();

            Assert.Equal(TimingConstants.CHIPREAD_STATUS_WITHINIGN, ReadById(read.ReadId).Status);
            Assert.Empty(Results());
        }

        [Fact]
        public void ExtraFinishRead_IsOverMax()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1");
            ReadAt(Chip("1"), 1500);
            var extra = ReadAt(Chip("1"), 1700);

            ProcessDistance();

            Assert.Equal("0:25:00.000", FinishFor("1").Time);
            Assert.Equal(TimingConstants.CHIPREAD_STATUS_OVERMAX, ReadById(extra.ReadId).Status);
        }

        [Fact]
        public void ReadsArrivingAcrossPasses_GiveSameResultAsOnePass()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1");
            ReadAt(Chip("1"), 10);
            ProcessDistance();
            ReadAt(Chip("1"), 1500);
            ProcessDistance();
            var extra = ReadAt(Chip("1"), 1600);
            ProcessDistance();

            Assert.Equal("0:24:50.000", FinishFor("1").ChipTime);
            Assert.Equal(TimingConstants.CHIPREAD_STATUS_OVERMAX, ReadById(extra.ReadId).Status);
        }

        [Fact]
        public void SeparateStartAndFinish_UsesStartLineRead()
        {
            SetupEvent(configure: e => e.CommonStartFinish = false);
            var d = AddRaceDistance();
            AddRunner(d, "1");
            ReadAt(Chip("1"), 10, TimingConstants.LOCATION_START);
            var tooSoon = ReadAt(Chip("1"), 100); // within 120s of the start read
            ReadAt(Chip("1"), 1500);

            ProcessDistance();

            Assert.Equal(TimingConstants.CHIPREAD_STATUS_WITHINIGN, ReadById(tooSoon.ReadId).Status);
            var finish = FinishFor("1");
            Assert.Equal("0:25:00.000", finish.Time);
            Assert.Equal("0:24:50.000", finish.ChipTime);
        }

        [Fact]
        public void WaveStartOffset_ShiftsGunTime()
        {
            SetupEvent();
            var d = AddRaceDistance(configure: x => { x.Wave = 2; x.StartOffsetSeconds = 600; });
            AddRunner(d, "1");
            var beforeWave = ReadAt(Chip("1"), 300);
            ReadAt(Chip("1"), 2100);

            ProcessDistance();

            Assert.Equal(TimingConstants.CHIPREAD_STATUS_PRESTART, ReadById(beforeWave.ReadId).Status);
            Assert.Equal("0:25:00.000", FinishFor("1").Time);
        }

        [Fact]
        public void UnknownChip_CreatesUnknownResult()
        {
            SetupEvent();
            AddRaceDistance();
            ReadAt("NOBODY", 1500);

            ProcessDistance();

            var result = Assert.Single(Results());
            Assert.Equal(TimeResult.ChipToIdentifier("NOBODY"), result.UnknownId);
            Assert.Equal(TimingConstants.TIMERESULT_DUMMYPERSON, result.EventSpecificId);
        }

        [Fact]
        public void IntermediateLocation_MatchesSegment()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1");
            int mile = Db.AddTimingLocation(new TimingLocation(TheEvent.Identifier, "Mile 1"));
            int segId = Db.AddSegment(new Segment(TheEvent.Identifier, TimingConstants.COMMON_SEGMENTS_DISTANCEID, mile, 1, 1.0, 1.0,
                Constants.Distances.MILES, "Mile 1", "", ""));
            ReadAt(Chip("1"), 480, mile);
            ReadAt(Chip("1"), 1500);

            ProcessDistance();

            var split = Assert.Single(Results(), r => r.LocationId == mile);
            Assert.Equal(segId, split.SegmentId);
            Assert.Equal("0:08:00.000", split.Time);
        }

        [Fact]
        public void ManualDnf_MarksRunnerAndIgnoresLaterReads()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1");
            ManualAt("1", 1000, TimingConstants.CHIPREAD_STATUS_DNF);
            var late = ReadAt(Chip("1"), 1500);

            ProcessDistance();

            var finish = FinishFor("1");
            Assert.Equal(TimingConstants.TIMERESULT_STATUS_DNF, finish.Status);
            Assert.Equal("DNF", finish.Time);
            Assert.Equal(TimingConstants.EVENTSPECIFIC_DNF, Runner("1").Status);
            Assert.Equal(TimingConstants.CHIPREAD_STATUS_OVERMAX, ReadById(late.ReadId).Status);
        }

        [Fact]
        public void DnsRead_MarksRunnerDnsAndLaterReadsAfterDns()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1");
            var dns = ReadAt(Chip("1"), -600);
            dns.Status = TimingConstants.CHIPREAD_STATUS_DNS;
            Db.SetChipReadStatus(dns);
            var late = ReadAt(Chip("1"), 1500);

            ProcessDistance();

            Assert.Equal(TimingConstants.EVENTSPECIFIC_DNS, Runner("1").Status);
            Assert.Equal(TimingConstants.TIMERESULT_STATUS_DNS, FinishFor("1").Status);
            Assert.Equal(TimingConstants.CHIPREAD_STATUS_AFTER_DNS, ReadById(late.ReadId).Status);
        }
    }

    public class TimingDictionaryTests : RoutineTestBase
    {
        [Fact]
        public void Recalculate_BuildsStartTimesFromEventAndWaves()
        {
            SetupEvent(configure: e => e.StartMilliseconds = 500);
            var d = AddRaceDistance(configure: x => { x.StartOffsetSeconds = 600; x.StartOffsetMilliseconds = 700; });

            var dictionary = BuildDictionary();

            long gun = TimingConstants.RfidDateToEpoch(Gun);
            Assert.Equal((gun, 500), dictionary.DistanceStartDict[0]);
            Assert.Equal((gun + 601, 200), dictionary.DistanceStartDict[d.Identifier]);
        }

        [Fact]
        public void Recalculate_MapsLinkedDistancesToTheirMainDistance()
        {
            SetupEvent();
            var main = AddRaceDistance("Half");
            var early = AddRaceDistance("Half Early", x => { x.LinkedDistance = main.Identifier; x.Ranking = 1; });

            var dictionary = BuildDictionary();

            Assert.Equal(main.Identifier, dictionary.LinkedDistanceIdentifierDictionary[early.Identifier]);
            Assert.Equal((main.Identifier, 1), (dictionary.LinkedDistanceDictionary["Half Early"].Item1.Identifier, dictionary.LinkedDistanceDictionary["Half Early"].Item2));
            Assert.Equal(main.Identifier, Assert.Single(dictionary.MainDistances).Identifier);
        }

        [Fact]
        public void Recalculate_AfterChipIsRemoved_ForgetsTheChip()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1");
            var dns = ReadAt(Chip("1"), -600);
            dns.Status = TimingConstants.CHIPREAD_STATUS_DNS;
            Db.SetChipReadStatus(dns);
            BuildDictionary();

            // The DNS chip turns out to be the wrong one and is removed from bib 1.
            Db.RemoveBibChipAssociation(TheEvent.Identifier, Chip("1"));
            var dictionary = BuildDictionary();

            Assert.False(dictionary.ChipToBibDictionary.ContainsKey(Chip("1")));
            Assert.DoesNotContain("1", dictionary.DnsBibs);
            Assert.Empty(dictionary.BibToChipDictionary);
        }
    }

    public class DistanceRoutinePlacementTests : RoutineTestBase
    {
        [Fact]
        public void Places_OverallGenderAndDivision()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1", gender: "F", division: "Elite");
            AddRunner(d, "2", gender: "M", division: "Elite");
            AddRunner(d, "3", gender: "F", division: "Open");
            ReadAt(Chip("1"), 1500);
            ReadAt(Chip("2"), 1400);
            ReadAt(Chip("3"), 1600);

            ProcessDistance();

            Assert.Equal((2, 1, 2), Places("1"));
            Assert.Equal((1, 1, 1), Places("2"));
            Assert.Equal((3, 2, 1), Places("3"));
            Assert.All(Results(), r => Assert.Equal(TimingConstants.TIMERESULT_STATUS_PROCESSED, r.Status));
        }

        private (int Place, int Gender, int Division) Places(string bib)
        {
            var r = FinishFor(bib);
            return (r.Place, r.GenderPlace, r.DivisionPlace);
        }

        [Fact]
        public void DnfRunner_IsNotPlaced()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1");
            AddRunner(d, "2");
            ReadAt(Chip("1"), 1500);
            ManualAt("2", 1000, TimingConstants.CHIPREAD_STATUS_DNF);

            ProcessDistance();

            Assert.Equal(1, FinishFor("1").Place);
            Assert.Equal(TimingConstants.TIMERESULT_DUMMYPLACE, FinishFor("2").Place);
        }

        [Fact]
        public void EachDistance_IsPlacedSeparately()
        {
            SetupEvent();
            var fiveK = AddRaceDistance("5K");
            var tenK = AddRaceDistance("10K");
            AddRunner(fiveK, "1");
            AddRunner(tenK, "2");
            ReadAt(Chip("1"), 1500);
            ReadAt(Chip("2"), 3000);

            ProcessDistance();

            Assert.Equal(1, FinishFor("1").Place);
            Assert.Equal(1, FinishFor("2").Place);
        }

        [Theory]
        [InlineData(RankingType.Clock, "1", "2")]
        [InlineData(RankingType.Chip, "2", "1")]
        public void Ranking_UsesClockOrChipTime(RankingType ranking, string firstBib, string secondBib)
        {
            SetupEvent(configure: e => e.RankedBy = ranking);
            var d = AddRaceDistance();
            AddRunner(d, "1");
            AddRunner(d, "2");
            ReadAt(Chip("1"), 1500);              // clock 25:00, chip 25:00
            ReadAt(Chip("2"), 50);                // starts 50s late
            ReadAt(Chip("2"), 1520);              // clock 25:20, chip 24:30

            ProcessDistance();

            Assert.Equal(1, FinishFor(firstBib).Place);
            Assert.Equal(2, FinishFor(secondBib).Place);
        }

        [Fact]
        public void MixedRanking_KeepsClockPodiumThenUsesChipTime()
        {
            SetupEvent(configure: e => e.RankedBy = RankingType.Mixed);
            var d = AddRaceDistance();
            foreach (string bib in new[] { "1", "2", "3", "4", "5" })
            {
                AddRunner(d, bib);
            }
            ReadAt(Chip("1"), 1000);
            ReadAt(Chip("2"), 1100);
            ReadAt(Chip("3"), 1200);
            ReadAt(Chip("4"), 1300);              // chip 21:40
            ReadAt(Chip("5"), 59);
            ReadAt(Chip("5"), 1340);              // clock 22:20, chip 21:21

            ProcessDistance();

            Assert.Equal([1, 2, 3, 5, 4], new[] { "1", "2", "3", "4", "5" }.Select(b => FinishFor(b).Place));
        }

        [Fact]
        public void LinkedDistance_RanksAfterMainDistanceByRankingOrder()
        {
            SetupEvent();
            var main = AddRaceDistance("Half");
            var early = AddRaceDistance("Half Early", x => { x.LinkedDistance = main.Identifier; x.Ranking = 1; });
            AddRunner(main, "1");
            AddRunner(early, "2");
            ReadAt(Chip("1"), 1200);
            ReadAt(Chip("2"), 1000);              // faster, but early starters rank after the main field

            ProcessDistance();

            Assert.Equal(1, FinishFor("1").Place);
            Assert.Equal(2, FinishFor("2").Place);
        }

        [Fact]
        public void RerunningPlacements_IsStable()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1");
            AddRunner(d, "2");
            ReadAt(Chip("1"), 1500);
            ReadAt(Chip("2"), 1400);
            ProcessDistance();

            Db.ResetTimingResultsPlacements(TheEvent.Identifier);
            ProcessDistance();

            Assert.Equal(2, FinishFor("1").Place);
            Assert.Equal(1, FinishFor("2").Place);
        }

        [Fact]
        public void PlaceChange_MarksDisplacedResultForReupload()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1");
            AddRunner(d, "2");
            ReadAt(Chip("1"), 1500);
            ProcessDistance();
            var uploaded = Results();
            uploaded.ForEach(r => r.Uploaded = TimingConstants.TIMERESULT_UPLOADED_TRUE);
            Db.SetUploadedTimingResults(uploaded);

            ReadAt(Chip("2"), 1400);              // a faster finisher arrives later
            ProcessDistance();

            Assert.Equal(2, FinishFor("1").Place);
            Assert.False(FinishFor("1").IsUploaded());
        }
    }
}
