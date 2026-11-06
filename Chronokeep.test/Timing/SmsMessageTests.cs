using Chronokeep.Objects;
using Chronokeep.Timing;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Timing
{
    /// <summary>The text messages sent to people subscribed to a runner's results.</summary>
    public class SmsMessageTests
    {
        private static readonly Event TheEvent = new() { Name = "Spring Run", Date = new DateTime(2026, 6, 15).ToShortDateString() };

        private static TimeResult Result(int segment)
        {
            var r = new TimeResult(1, -1, 42, TimingConstants.LOCATION_FINISH, segment, 1, 1510, 250, "Bib:7",
                1500, 250, DateTime.Now, "7", TimingConstants.TIMERESULT_STATUS_NONE, "");
            r.SetParticipant(new Participant("Jane", "Runner", "", "", "", "", "",
                new EventSpecific(1, 1, "10K", "7", 0, "", "", "", false, true, "", ""), "", "", "", "", "", "", "F", "", ""));
            return r;
        }

        [Fact]
        public void Finish_SingleDistance()
        {
            Assert.Equal(
                "Jane Runner has finished the 2026 Spring Run in 0:25:00. Reply STOP to opt-out.",
                TimingWorker.BuildSmsMessage(Result(TimingConstants.SEGMENT_FINISH), TheEvent, false, ""));
        }

        [Fact]
        public void Finish_MultipleDistances_NamesTheDistance()
        {
            Assert.Equal(
                "Jane Runner has finished the 2026 Spring Run 10K in 0:25:00. More results @ https://x/. Reply STOP to opt-out.",
                TimingWorker.BuildSmsMessage(Result(TimingConstants.SEGMENT_FINISH), TheEvent, true, " More results @ https://x/."));
        }

        [Fact]
        public void Split_SaysReached()
        {
            var r = Result(12);
            r.SegmentName = "Mile 3 ";
            Assert.Equal(
                "Jane Runner has reached Mile 3 in 0:25:00. Reply STOP to opt-out.",
                TimingWorker.BuildSmsMessage(r, TheEvent, false, ""));
        }
    }
}
