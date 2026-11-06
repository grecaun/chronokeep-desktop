using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Timing
{
    /// <summary>A one-hour timed race: count laps at the finish line until the clock runs out.</summary>
    public class TimeRoutineTests : RoutineTestBase
    {
        private const int OneHour = 3600;

        private Chronokeep.Objects.Distance Setup()
        {
            SetupEvent(TimingConstants.EVENT_TYPE_TIME);
            return AddRaceDistance("1 Hour", x => { x.EndSeconds = OneHour; x.DistanceValue = 1.0; });
        }

        [Fact]
        public void EachFinishRead_IsAnotherLap()
        {
            var d = Setup();
            AddRunner(d, "1");
            ReadAt(Chip("1"), 600);
            ReadAt(Chip("1"), 1200);
            ReadAt(Chip("1"), 1800);

            ProcessTime();

            var laps = LapsFor("1");
            Assert.Equal([1, 2, 3], laps.Select(l => l.Occurrence));
            Assert.Equal(["0:10:00.000", "0:20:00.000", "0:30:00.000"], laps.Select(l => l.Time));
        }

        [Fact]
        public void LapTimes_AreTheTimeBetweenLaps()
        {
            var d = Setup();
            AddRunner(d, "1");
            ReadAt(Chip("1"), 600);
            ReadAt(Chip("1"), 1260);
            ReadAt(Chip("1"), 1800);

            ProcessTime();

            Assert.Equal(["0:10:00.000", "0:11:00.000", "0:09:00.000"], LapsFor("1").Select(l => l.LapTime));
        }

        [Fact]
        public void ReadAfterTimeLimit_IsOverMax()
        {
            var d = Setup();
            AddRunner(d, "1");
            ReadAt(Chip("1"), 600);
            var late = ReadAt(Chip("1"), OneHour + 30);

            ProcessTime();

            Assert.Single(LapsFor("1"));
            Assert.Equal(TimingConstants.CHIPREAD_STATUS_OVERMAX, ReadById(late.ReadId).Status);
        }

        [Fact]
        public void ReadWithinIgnorePeriod_IsNotALap()
        {
            var d = Setup();
            AddRunner(d, "1");
            ReadAt(Chip("1"), 600);
            var bounce = ReadAt(Chip("1"), 650);

            ProcessTime();

            Assert.Single(LapsFor("1"));
            Assert.Equal(TimingConstants.CHIPREAD_STATUS_WITHINIGN, ReadById(bounce.ReadId).Status);
        }

        [Fact]
        public void ManualDnf_StopsCountingLaps()
        {
            var d = Setup();
            AddRunner(d, "1");
            ReadAt(Chip("1"), 600);
            ManualAt("1", 900, TimingConstants.CHIPREAD_STATUS_DNF);
            var after = ReadAt(Chip("1"), 1200);

            ProcessTime();

            Assert.Single(LapsFor("1"));
            Assert.Equal(TimingConstants.CHIPREAD_STATUS_OVERMAX, ReadById(after.ReadId).Status);
        }

        [Fact]
        public void Placement_MostLapsWinsThenFastestTime()
        {
            var d = Setup();
            AddRunner(d, "1");
            AddRunner(d, "2");
            AddRunner(d, "3");
            foreach (int t in new[] { 600, 1200, 1800 }) ReadAt(Chip("1"), t);          // 3 laps, last at 30:00
            foreach (int t in new[] { 500, 1000, 1500, 2000 }) ReadAt(Chip("2"), t);    // 4 laps
            foreach (int t in new[] { 550, 1100, 1700 }) ReadAt(Chip("3"), t);          // 3 laps, last at 28:20

            ProcessTime();

            Assert.Equal(1, LapsFor("2").Last().Place);
            Assert.Equal(2, LapsFor("3").Last().Place);
            Assert.Equal(3, LapsFor("1").Last().Place);
        }

        [Fact]
        public void LapsAcrossSeveralPasses_KeepCounting()
        {
            var d = Setup();
            AddRunner(d, "1");
            ReadAt(Chip("1"), 600);
            ProcessTime();
            ReadAt(Chip("1"), 1200);
            ProcessTime();

            var laps = LapsFor("1");
            Assert.Equal([1, 2], laps.Select(l => l.Occurrence));
            Assert.Equal("0:10:00.000", laps[1].LapTime);
        }
    }
}
