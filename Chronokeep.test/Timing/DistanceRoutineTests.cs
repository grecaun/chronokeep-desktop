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
    

        private void MarkRemoved(string bib)
        {
            Participant p = Runner(bib);
            p.EventSpecific.Removed = "Transferred";
            Db.UpdateParticipant(p);
        }

        private void SetAgeGroup(string bib, int ageGroupId)
        {
            Participant p = Runner(bib);
            p.EventSpecific.AgeGroupId = ageGroupId;
            Db.UpdateParticipant(p);
        }

        [Fact]
        public void Tied_times_still_get_one_place_each()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1");
            AddRunner(d, "2");
            AddRunner(d, "3");
            ReadAt(Chip("1"), 1500);
            ReadAt(Chip("2"), 1500);
            ReadAt(Chip("3"), 1600);

            ProcessDistance();

            Assert.Equal([1, 2], new[] { FinishFor("1").Place, FinishFor("2").Place }.Order());
            Assert.Equal(3, FinishFor("3").Place);
        }

        [Fact]
        public void Age_group_places_count_within_age_group_and_gender()
        {
            SetupEvent();
            var d = AddRaceDistance();
            int young = Db.AddAgeGroup(new AgeGroup(TheEvent.Identifier, d.Identifier, 0, 39));
            int older = Db.AddAgeGroup(new AgeGroup(TheEvent.Identifier, d.Identifier, 40, 99));
            AddRunner(d, "1", gender: "F");
            AddRunner(d, "2", gender: "F");
            AddRunner(d, "3", gender: "F");
            AddRunner(d, "4", gender: "M");
            SetAgeGroup("1", young);
            SetAgeGroup("2", older);
            SetAgeGroup("3", young);
            SetAgeGroup("4", young);
            ReadAt(Chip("1"), 1500);
            ReadAt(Chip("2"), 1400);
            ReadAt(Chip("3"), 1600);
            ReadAt(Chip("4"), 1300);

            ProcessDistance();

            Assert.Equal(1, FinishFor("1").AgePlace);
            Assert.Equal(1, FinishFor("2").AgePlace);
            Assert.Equal(2, FinishFor("3").AgePlace);
            Assert.Equal(1, FinishFor("4").AgePlace);
        }

        [Fact]
        public void Runner_without_an_age_group_has_no_age_place()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1");
            ReadAt(Chip("1"), 1500);

            ProcessDistance();

            Assert.Equal(1, FinishFor("1").Place);
            Assert.Equal(TimingConstants.TIMERESULT_DUMMYPLACE, FinishFor("1").AgePlace);
        }

        [Fact]
        public void Runners_without_a_gender_are_ranked_together()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1", gender: "");
            AddRunner(d, "2", gender: "F");
            AddRunner(d, "3", gender: "");
            ReadAt(Chip("1"), 1500);
            ReadAt(Chip("2"), 1400);
            ReadAt(Chip("3"), 1600);

            ProcessDistance();

            Assert.Equal(1, FinishFor("1").GenderPlace);
            Assert.Equal(2, FinishFor("3").GenderPlace);
            Assert.Equal(1, FinishFor("2").GenderPlace);
        }

        [Fact]
        public void Division_names_are_matched_ignoring_case()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1", division: "Elite");
            AddRunner(d, "2", division: "elite");
            ReadAt(Chip("1"), 1500);
            ReadAt(Chip("2"), 1400);

            ProcessDistance();

            Assert.Equal(2, FinishFor("1").DivisionPlace);
            Assert.Equal(1, FinishFor("2").DivisionPlace);
        }

        [Fact]
        public void A_placed_runner_who_becomes_dnf_loses_their_division_place()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1", division: "Elite");
            AddRunner(d, "2", division: "Elite");
            ReadAt(Chip("1"), 1500);
            ReadAt(Chip("2"), 1600);
            ProcessDistance();
            TimeResult placed = FinishFor("1");
            Assert.Equal(1, placed.DivisionPlace);

            // A result already placed is marked DNF and placements run again.
            placed.Status = TimingConstants.TIMERESULT_STATUS_DNF;
            Db.AddTimingResults([placed]);
            Chronokeep.Timing.Routines.DistanceRoutine.ProcessPlacements(TheEvent, Db, BuildDictionary());

            TimeResult dnf = FinishFor("1");
            Assert.Equal((TimingConstants.TIMERESULT_DUMMYPLACE, TimingConstants.TIMERESULT_DUMMYPLACE, TimingConstants.TIMERESULT_DUMMYPLACE, TimingConstants.TIMERESULT_DUMMYPLACE), (dnf.Place, dnf.GenderPlace, dnf.AgePlace, dnf.DivisionPlace));
            Assert.Equal((1, 1), (FinishFor("2").Place, FinishFor("2").DivisionPlace));
        }

        [Fact]
        public void Removed_runners_keep_their_result_but_take_no_place()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1", gender: "F", division: "Elite");
            AddRunner(d, "2", gender: "F", division: "Elite");
            ReadAt(Chip("1"), 1400);
            ReadAt(Chip("2"), 1500);
            MarkRemoved("1");

            ProcessDistance();

            TimeResult removed = FinishFor("1");
            Assert.Equal(1400, removed.Seconds);
            Assert.Equal((TimingConstants.TIMERESULT_DUMMYPLACE, TimingConstants.TIMERESULT_DUMMYPLACE, TimingConstants.TIMERESULT_DUMMYPLACE, TimingConstants.TIMERESULT_DUMMYPLACE), (removed.Place, removed.GenderPlace, removed.AgePlace, removed.DivisionPlace));
            Assert.Equal((1, 1, 1), Places("2"));
        }
    }
}

namespace Chronokeep.Test.Timing
{
    /// <summary>Courses beyond one lap and one finish: laps, repeated mats, late starts and start-line repeats.</summary>
    public class DistanceRoutineCourseTests : RoutineTestBase
    {
        private List<TimeResult> FinishLine(string bib) =>
            [.. Results().Where(r => r.Bib == bib && r.LocationId == TimingConstants.LOCATION_FINISH).OrderBy(r => r.Occurrence)];

        [Fact]
        public void Multi_lap_distance_finishes_on_its_last_lap()
        {
            SetupEvent(configure: e => e.FinishMaxOccurrences = 3);
            var d = AddRaceDistance("10K", x => x.FinishOccurrence = 3);
            AddRunner(d, "1");
            ReadAt(Chip("1"), 600);
            ReadAt(Chip("1"), 1200);
            ReadAt(Chip("1"), 1800);
            var extra = ReadAt(Chip("1"), 2400);

            ProcessDistance();

            List<TimeResult> laps = FinishLine("1");
            Assert.Equal([1, 2, 3], laps.Select(l => l.Occurrence));
            Assert.Equal(TimingConstants.SEGMENT_FINISH, laps[2].SegmentId);
            Assert.All(laps.Take(2), l => Assert.NotEqual(TimingConstants.SEGMENT_FINISH, l.SegmentId));
            Assert.Equal("0:30:00.000", FinishFor("1").Time);
            Assert.Equal(TimingConstants.CHIPREAD_STATUS_OVERMAX, ReadById(extra.ReadId).Status);
        }

        [Fact]
        public void Each_distance_finishes_on_its_own_lap_count()
        {
            SetupEvent(configure: e => e.FinishMaxOccurrences = 2);
            var fiveK = AddRaceDistance("5K", x => x.FinishOccurrence = 1);
            var tenK = AddRaceDistance("10K", x => x.FinishOccurrence = 2);
            AddRunner(fiveK, "1");
            AddRunner(tenK, "2");
            ReadAt(Chip("1"), 1500);
            var fiveKExtra = ReadAt(Chip("1"), 3000);
            ReadAt(Chip("2"), 1400);
            ReadAt(Chip("2"), 2900);

            ProcessDistance();

            Assert.Equal("0:25:00.000", FinishFor("1").Time);
            Assert.Equal(TimingConstants.CHIPREAD_STATUS_OVERMAX, ReadById(fiveKExtra.ReadId).Status);
            Assert.Equal("0:48:20.000", FinishFor("2").Time);
            Assert.Equal(2, FinishFor("2").Occurrence);
        }

        [Fact]
        public void A_mat_crossed_twice_gives_each_crossing_its_own_segment()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1");
            int loop = Db.AddTimingLocation(new TimingLocation(TheEvent.Identifier, "Loop") { MaxOccurrences = 2, IgnoreWithin = 60 });
            int first = Db.AddSegment(new Segment(TheEvent.Identifier, TimingConstants.COMMON_SEGMENTS_DISTANCEID, loop, 1, 1.0, 1.0,
                Constants.Distances.MILES, "Loop 1", "", ""));
            int second = Db.AddSegment(new Segment(TheEvent.Identifier, TimingConstants.COMMON_SEGMENTS_DISTANCEID, loop, 2, 2.0, 1.0,
                Constants.Distances.MILES, "Loop 2", "", ""));
            ReadAt(Chip("1"), 480, loop);
            var tooSoon = ReadAt(Chip("1"), 500, loop);
            ReadAt(Chip("1"), 960, loop);
            var third = ReadAt(Chip("1"), 1300, loop);
            ReadAt(Chip("1"), 1500);

            ProcessDistance();

            List<TimeResult> splits = [.. Results().Where(r => r.LocationId == loop).OrderBy(r => r.Occurrence)];
            Assert.Equal([first, second], splits.Select(s => s.SegmentId));
            Assert.Equal(["0:08:00.000", "0:16:00.000"], splits.Select(s => s.Time));
            Assert.Equal(TimingConstants.CHIPREAD_STATUS_WITHINIGN, ReadById(tooSoon.ReadId).Status);
            Assert.Equal(TimingConstants.CHIPREAD_STATUS_OVERMAX, ReadById(third.ReadId).Status);
        }

        [Fact]
        public void Late_start_distance_times_from_the_linked_distances_gun()
        {
            SetupEvent();
            var main = AddRaceDistance("Half");
            var late = AddRaceDistance("Half Late", x =>
            {
                x.LinkedDistance = main.Identifier;
                x.Type = TimingConstants.DISTANCE_TYPE_LATE;
                x.Wave = 2;
                x.StartOffsetSeconds = 600;
            });
            AddRunner(late, "1");
            ReadAt(Chip("1"), 2400);

            ProcessDistance();

            Assert.Equal("0:40:00.000", FinishFor("1").Time);
        }

        [Theory]
        [InlineData(1, TimingConstants.CHIPREAD_STATUS_OVERMAX)]
        [InlineData(2, TimingConstants.CHIPREAD_STATUS_USED)]
        public void Start_line_repeats_count_toward_its_max(int startMax, int repeatStatus)
        {
            SetupEvent(configure: e => { e.CommonStartFinish = false; e.StartMaxOccurrences = startMax; });
            var d = AddRaceDistance();
            AddRunner(d, "1");
            ReadAt(Chip("1"), 10, TimingConstants.LOCATION_START);
            var repeat = ReadAt(Chip("1"), 900, TimingConstants.LOCATION_START);
            ReadAt(Chip("1"), 1500);

            ProcessDistance();

            Assert.Equal(repeatStatus, ReadById(repeat.ReadId).Status);
            Assert.Equal("0:25:00.000", FinishFor("1").Time);
        }
    }
}
