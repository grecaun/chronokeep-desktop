using Chronokeep.Objects;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Timing
{
    /// <summary>
    /// Tests how announcer-location reads and finish results are combined into the announcer list.
    /// </summary>
    public class AnnouncerMergeTests
    {
        private static readonly DateTime Now = DateTime.Today.AddHours(9);

        private static Participant Person(string bib) =>
            new($"Runner{bib}", "Last", "", "Eugene", "OR", "", new DateTime(1990, 6, 16).ToShortDateString(),
                new EventSpecific(1, 1, "5K", bib, 0, $"Go {bib}!", "", "", false, false, "", ""), "", "", "", "", "", "", "F", "", "");

        private static AnnouncerParticipant Seen(string bib, int secondsAgo, Participant? person = null) =>
            new(bib, person, TimingConstants.RfidDateToEpoch(Now.AddSeconds(-secondsAgo)));

        private static TimeResult Finish(string bib, int secondsAgo, int segment = TimingConstants.SEGMENT_FINISH, int status = TimingConstants.TIMERESULT_STATUS_NONE) =>
            new(1, 1, 1, TimingConstants.LOCATION_FINISH, segment, 1, 1800, 0, "", 1800, 0, Now.AddSeconds(-secondsAgo), bib, status, "");

        private static string When(int secondsAgo) => Now.AddSeconds(-secondsAgo).ToString("HH:mm:ss");

        /// <summary>Merges with a 45 second window. Bibs 1 to 3 are registered.</summary>
        private static List<AnnouncerParticipant> Merge(IEnumerable<AnnouncerParticipant> announced, IEnumerable<TimeResult> results, bool includeUnknown = false)
        {
            Dictionary<string, Participant> people = new[] { "1", "2", "3" }.ToDictionary(b => b, Person);
            return AnnouncerParticipant.Merge(announced, results, people.GetValueOrDefault, Now.AddSeconds(-45), includeUnknown);
        }

        [Fact]
        public void AnnouncerReadsOnly_AreListedNewestFirst()
        {
            List<AnnouncerParticipant> list = Merge([Seen("1", 30, Person("1")), Seen("2", 10, Person("2"))], []);

            Assert.Equal(["2", "1"], list.Select(x => x.Bib));
            Assert.Equal([When(10), When(30)], list.Select(x => x.AnnouncerWhen));
        }

        [Fact]
        public void FinishersOnly_AreListedWithParticipantDetails()
        {
            AnnouncerParticipant row = Assert.Single(Merge([], [Finish("1", 5)]));

            Assert.Equal("Runner1 Last", row.ParticipantName);
            Assert.Equal("Eugene OR", row.CityState);
            Assert.Equal("Go 1!", row.Comments);
            Assert.Equal(When(5), row.AnnouncerWhen);
        }

        [Fact]
        public void BothSources_AreListedTogether()
        {
            List<AnnouncerParticipant> list = Merge([Seen("1", 20, Person("1"))], [Finish("2", 5)]);

            Assert.Equal(["2", "1"], list.Select(x => x.Bib));
        }

        [Fact]
        public void AnnouncerReadThenFinish_ShowsTheRunnerOnceAtTheFinishTime()
        {
            List<AnnouncerParticipant> list = Merge([Seen("1", 30, Person("1")), Seen("2", 20, Person("2"))], [Finish("1", 5)]);

            Assert.Equal(["1", "2"], list.Select(x => x.Bib));
            Assert.Equal(When(5), list[0].AnnouncerWhen);
        }

        [Fact]
        public void FinishThenAnnouncerRead_ShowsTheRunnerOnceAtTheLaterTime()
        {
            AnnouncerParticipant row = Assert.Single(Merge([Seen("1", 2, Person("1"))], [Finish("1", 10)]));

            Assert.Equal(When(2), row.AnnouncerWhen);
        }

        [Fact]
        public void OldNonFinishDnfAndChipOnlyResults_AreLeftOut()
        {
            List<AnnouncerParticipant> list = Merge([Seen("1", 300, Person("1"))], [
                Finish("1", 300),
                Finish("2", 5, segment: 7),
                Finish("3", 5, status: TimingConstants.TIMERESULT_STATUS_DNF),
                Finish(TimingConstants.CHIPREAD_DUMMYBIB, 5),
            ], includeUnknown: true);

            Assert.Empty(list);
        }

        [Fact]
        public void UnknownBibs_AreHiddenUnlessAskedFor()
        {
            List<AnnouncerParticipant> announced = [Seen("98", 10), Seen("1", 20, Person("1"))];
            List<TimeResult> results = [Finish("99", 5)];

            Assert.Equal(["1"], Merge(announced, results).Select(x => x.Bib));

            List<AnnouncerParticipant> all = Merge(announced, results, includeUnknown: true);
            Assert.Equal(["99", "98", "1"], all.Select(x => x.Bib));
            Assert.Equal("", all[0].ParticipantName);
            Assert.Equal("", all[0].AgeGender);
        }

        [Fact]
        public void AnnouncerReadWithoutParticipant_IsFilledInFromRegistration()
        {
            // The worker didn't know bib 3 yet, but someone has registered with it since.
            AnnouncerParticipant row = Assert.Single(Merge([Seen("3", 10)], []));

            Assert.Equal("Runner3 Last", row.ParticipantName);
        }
    }
}
