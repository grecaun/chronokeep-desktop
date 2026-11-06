using Chronokeep.Objects;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Timing
{
    /// <summary>
    /// Backyard ultra: a new loop starts every hour (the "interval") and runners must finish each loop
    /// before the next one starts. Start results use occurrence hour*2, finish results hour*2+1.
    /// The start window is 60 seconds into each hour.
    /// </summary>
    public class BackyardUltraRoutineTests : RoutineTestBase
    {
        private const int Hour = 3600;

        private Distance Setup(Action<Event>? configureEvent = null, Action<Distance>? configureDistance = null)
        {
            SetupEvent(TimingConstants.EVENT_TYPE_BACKYARD_ULTRA, configureEvent);
            return AddRaceDistance("Backyard", x => { x.DistanceValue = 4.167; configureDistance?.Invoke(x); });
        }

        /// <summary>Start read in hour h's start window plus a finish read after the given loop time.</summary>
        private void Loop(string bib, int hour, int loopSeconds, int startOffset = 5)
        {
            ReadAt(Chip(bib), (hour * Hour) + startOffset);
            ReadAt(Chip(bib), (hour * Hour) + loopSeconds);
        }

        private List<TimeResult> FinishesFor(string bib)
            => [.. Results().Where(r => r.Bib == bib && r.SegmentId == TimingConstants.SEGMENT_FINISH).OrderBy(r => r.Occurrence)];

        [Fact]
        public void OneLoop_CreatesStartAndFinishForHourOne()
        {
            var d = Setup();
            AddRunner(d, "1");
            var start = ReadAt(Chip("1"), 5);
            ReadAt(Chip("1"), 2400);

            ProcessBackyard();

            Assert.Equal(TimingConstants.CHIPREAD_STATUS_STARTTIME, ReadById(start.ReadId).Status);
            var startResult = Assert.Single(Results(), r => r.SegmentId == TimingConstants.SEGMENT_START);
            Assert.Equal(0, startResult.Occurrence);
            var finish = Assert.Single(FinishesFor("1"));
            Assert.Equal(1, finish.Occurrence);
            Assert.Equal("0:40:00.000", finish.Time);
            Assert.Equal("0:40:00.000", finish.ChipTime);
            Assert.Equal(TimingConstants.EVENTSPECIFIC_STARTED, Runner("1").Status);
        }

        [Fact]
        public void SeveralLoops_TrackElapsedAndCumulativeTime()
        {
            var d = Setup();
            AddRunner(d, "1");
            Loop("1", 0, 2400);
            Loop("1", 1, 2500);
            Loop("1", 2, 2600);

            ProcessBackyard();

            var finishes = FinishesFor("1");
            Assert.Equal([1, 3, 5], finishes.Select(f => f.Occurrence));
            Assert.Equal("2:43:20.000", finishes[2].Time);       // 2h + 43:20 since the gun
            Assert.Equal("2:05:00.000", finishes[2].ChipTime);   // 40:00 + 41:40 + 43:20 on course
        }

        [Fact]
        public void LoopsAcrossSeveralPasses_MatchOnePass()
        {
            var d = Setup();
            AddRunner(d, "1");
            Loop("1", 0, 2400);
            ProcessBackyard();
            Loop("1", 1, 2500);
            ProcessBackyard();

            var finishes = FinishesFor("1");
            Assert.Equal([1, 3], finishes.Select(f => f.Occurrence));
            Assert.Equal("1:21:40.000", finishes[1].ChipTime);
        }

        [Fact]
        public void SecondFinishReadInSameHour_IsIgnored()
        {
            var d = Setup();
            AddRunner(d, "1");
            Loop("1", 0, 2400);
            var extra = ReadAt(Chip("1"), 2500);

            ProcessBackyard();

            Assert.Single(FinishesFor("1"));
            Assert.Equal(TimingConstants.CHIPREAD_STATUS_WITHINIGN, ReadById(extra.ReadId).Status);
        }

        [Fact]
        public void StartReadAtEdgeOfStartWindow_IsAStart()
        {
            var d = Setup();
            AddRunner(d, "1");
            var start = ReadAt(Chip("1"), 60); // start window is 60 seconds

            ProcessBackyard();

            Assert.Equal(TimingConstants.CHIPREAD_STATUS_STARTTIME, ReadById(start.ReadId).Status);
            Assert.Empty(FinishesFor("1"));
        }

        [Fact]
        public void StartingNextHourWithoutFinishing_IsAutoDnf()
        {
            var d = Setup();
            AddRunner(d, "1");
            Loop("1", 0, 2400);                    // finishes hour 1
            ReadAt(Chip("1"), Hour + 5);           // starts hour 2 but never finishes it
            var late = ReadAt(Chip("1"), (2 * Hour) + 5);   // shows up for hour 3

            ProcessBackyard();

            Assert.Equal(TimingConstants.CHIPREAD_STATUS_AUTO_DNF, ReadById(late.ReadId).Status);
            Assert.Equal(TimingConstants.EVENTSPECIFIC_DNF, Runner("1").Status);
            var dnf = Assert.Single(FinishesFor("1"), f => f.Status == TimingConstants.TIMERESULT_STATUS_DNF);
            Assert.Equal(3, dnf.Occurrence);       // failed the second loop
        }

        [Fact]
        public void NeverFinishingFirstLoop_IsDnfInHourOne()
        {
            var d = Setup();
            AddRunner(d, "1");
            ReadAt(Chip("1"), 5);                  // starts hour 1
            ReadAt(Chip("1"), Hour + 5);           // still at the line for hour 2 without a finish

            ProcessBackyard();

            var dnf = Assert.Single(FinishesFor("1"), f => f.Status == TimingConstants.TIMERESULT_STATUS_DNF);
            Assert.Equal(1, dnf.Occurrence);
        }

        [Fact]
        public void ManualDnfDuringALoop_MarksThatLoopDnf()
        {
            var d = Setup();
            AddRunner(d, "1");
            Loop("1", 0, 2400);
            ReadAt(Chip("1"), Hour + 5);           // starts hour 2
            ManualAt("1", Hour + 1200, TimingConstants.CHIPREAD_STATUS_DNF);

            ProcessBackyard();

            Assert.Equal(TimingConstants.EVENTSPECIFIC_DNF, Runner("1").Status);
            var dnf = Assert.Single(FinishesFor("1"), f => f.Status == TimingConstants.TIMERESULT_STATUS_DNF);
            Assert.Equal(3, dnf.Occurrence);
            Assert.Equal(Runner("1").EventSpecific.Identifier, dnf.EventSpecificId);
            Assert.Contains(FinishesFor("1"), f => f.Occurrence == 1 && f.Status != TimingConstants.TIMERESULT_STATUS_DNF);
        }

        [Fact]
        public void ManualDnfEnteredLater_MarksThatLoopDnf()
        {
            var d = Setup();
            AddRunner(d, "1");
            Loop("1", 0, 2400);
            ReadAt(Chip("1"), Hour + 5);           // starts hour 2
            ProcessBackyard();

            // Same as the manual entry window: add the DNF, reset results, reprocess.
            ManualAt("1", Hour + 1200, TimingConstants.CHIPREAD_STATUS_DNF);
            Db.ResetTimingResultsEvent(TheEvent.Identifier);
            ProcessBackyard();

            Assert.Equal(TimingConstants.EVENTSPECIFIC_DNF, Runner("1").Status);
            var dnf = Assert.Single(FinishesFor("1"), f => f.Status == TimingConstants.TIMERESULT_STATUS_DNF);
            Assert.Equal(3, dnf.Occurrence);
        }

        [Fact]
        public void ManualDnfProcessedWithoutReset_DoesNotCrash()
        {
            var d = Setup();
            AddRunner(d, "1");
            Loop("1", 0, 2400);
            ReadAt(Chip("1"), Hour + 5);           // hour 2 started, start result saved
            ProcessBackyard();
            ManualAt("1", Hour + 1200, TimingConstants.CHIPREAD_STATUS_DNF);

            // Run the routine directly, without the reset the manual entry window does.
            Chronokeep.Timing.Routines.BackyardUltraRoutine.ProcessRace(TheEvent, Db, BuildDictionary(), Window);

            var dnf = Assert.Single(FinishesFor("1"), f => f.Status == TimingConstants.TIMERESULT_STATUS_DNF);
            Assert.Equal(3, dnf.Occurrence);
        }

        [Fact]
        public void FinishingTwoLoopsButNotThird_IsDnfOnLoopThree()
        {
            var d = Setup();
            AddRunner(d, "1");
            Loop("1", 0, 2400);
            Loop("1", 1, 2400);
            ReadAt(Chip("1"), (2 * Hour) + 5);     // starts loop 3, never finishes it
            var late = ReadAt(Chip("1"), (3 * Hour) + 5);

            ProcessBackyard();

            Assert.Equal(TimingConstants.CHIPREAD_STATUS_AUTO_DNF, ReadById(late.ReadId).Status);
            var dnf = Assert.Single(FinishesFor("1"), f => f.Status == TimingConstants.TIMERESULT_STATUS_DNF);
            Assert.Equal(5, dnf.Occurrence);
        }

        [Fact]
        public void UnknownChip_FinishingAnHourLate_IsDnf()
        {
            Setup();
            ReadAt("NOBODY", 5);
            ReadAt("NOBODY", 2400);                // finishes loop 1
            ReadAt("NOBODY", (2 * Hour) + 1000);   // crosses the line in hour 3 without doing loop 2

            ProcessBackyard();

            var dnf = Assert.Single(Results(), r => r.SegmentId == TimingConstants.SEGMENT_FINISH && r.Occurrence == 3);
            Assert.Equal(TimingConstants.TIMERESULT_STATUS_DNF, dnf.Status);
        }

        [Fact]
        public void ReadsAfterDnf_AreIgnored()
        {
            var d = Setup();
            AddRunner(d, "1");
            ManualAt("1", 1200, TimingConstants.CHIPREAD_STATUS_DNF);
            var later = ReadAt(Chip("1"), Hour + 5);

            ProcessBackyard();

            Assert.Equal(TimingConstants.CHIPREAD_STATUS_OVERMAX, ReadById(later.ReadId).Status);
            Assert.Equal(TimingConstants.EVENTSPECIFIC_DNF, Runner("1").Status);
        }

        [Fact]
        public void ReadsPastTheLastInterval_AreAfterFinish()
        {
            var d = Setup(configureDistance: x => x.EndSeconds = 2 * Hour);   // a two-hour race
            AddRunner(d, "1");
            Loop("1", 0, 2400);
            Loop("1", 1, 2400);
            var tooLate = ReadAt(Chip("1"), (2 * Hour) + 5);

            ProcessBackyard();

            Assert.Equal(2, FinishesFor("1").Count);
            Assert.Equal(TimingConstants.CHIPREAD_STATUS_AFTER_FINISH, ReadById(tooLate.ReadId).Status);
        }

        [Fact]
        public void CustomInterval_UsesDistanceStartOffset()
        {
            var d = Setup(configureDistance: x => x.StartOffsetSeconds = 1800);  // 30-minute loops
            AddRunner(d, "1");
            ReadAt(Chip("1"), 5);
            ReadAt(Chip("1"), 1500);
            ReadAt(Chip("1"), 1805);
            ReadAt(Chip("1"), 3300);

            ProcessBackyard();

            Assert.Equal([1, 3], FinishesFor("1").Select(f => f.Occurrence));
        }

        [Fact]
        public void UnknownChip_StillGetsLoopResults()
        {
            Setup();
            ReadAt("NOBODY", 5);
            ReadAt("NOBODY", 2400);

            ProcessBackyard();

            var finish = Assert.Single(Results(), r => r.SegmentId == TimingConstants.SEGMENT_FINISH);
            Assert.Equal(TimeResult.ChipToIdentifier("NOBODY"), finish.UnknownId);
            Assert.Equal(TimingConstants.TIMERESULT_DUMMYPERSON, finish.EventSpecificId);
        }

        [Fact]
        public void UnknownChip_MissingAnHour_IsAutoDnf()
        {
            Setup();
            ReadAt("NOBODY", 5);
            ReadAt("NOBODY", 2400);
            ReadAt("NOBODY", Hour + 5);
            var late = ReadAt("NOBODY", (2 * Hour) + 5);

            ProcessBackyard();

            Assert.Equal(TimingConstants.CHIPREAD_STATUS_AUTO_DNF, ReadById(late.ReadId).Status);
            var dnf = Assert.Single(Results(), r => r.SegmentId == TimingConstants.SEGMENT_FINISH && r.Occurrence == 3);
            Assert.Equal(TimingConstants.TIMERESULT_STATUS_DNF, dnf.Status);
        }

        [Fact]
        public void Placement_MostLoopsThenElapsedTime()
        {
            var d = Setup();
            AddRunner(d, "1", gender: "F");
            AddRunner(d, "2", gender: "M");
            AddRunner(d, "3", gender: "F");
            foreach (int h in new[] { 0, 1, 2 }) Loop("1", h, 2400);
            foreach (int h in new[] { 0, 1, 2 }) Loop("3", h, 2500);
            Loop("2", 0, 2000);                     // only one loop, then drops
            ReadAt(Chip("2"), Hour + 5);
            ReadAt(Chip("2"), (2 * Hour) + 5);

            ProcessBackyard();

            Assert.Equal(1, FinishesFor("1").Last().Place);
            Assert.Equal(2, FinishesFor("3").Last().Place);
            Assert.Equal(3, FinishesFor("2").First().Place);
            Assert.Equal(2, FinishesFor("3").Last().GenderPlace);
            Assert.Equal(1, FinishesFor("2").First().GenderPlace);
            // Every result for a runner carries their current place.
            Assert.All(FinishesFor("1"), f => Assert.Equal(1, f.Place));
        }

        [Theory]
        [InlineData(RankingType.Clock, "1")]
        [InlineData(RankingType.Chip, "2")]
        public void Ranking_UsesElapsedOrCumulativeTime(RankingType ranking, string winner)
        {
            var d = Setup(e => e.RankedBy = ranking);
            AddRunner(d, "1");
            AddRunner(d, "2");
            Loop("1", 0, 2400);
            Loop("1", 1, 2400);                     // elapsed 1:40:00, on course 1:20:00
            Loop("2", 0, 2000);
            Loop("2", 1, 2500, startOffset: 50);    // elapsed 1:41:40, on course 1:15:00

            ProcessBackyard();

            Assert.Equal(1, FinishesFor(winner).Last().Place);
        }
    }
}
