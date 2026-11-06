using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.Test.Database;
using Chronokeep.Timing.Announcer;
using System.Collections;
using System.Reflection;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Timing
{
    /// <summary>
    /// Tests how the announcer worker turns announcer-mat reads into the announcer list.
    /// The worker keeps its list and "seen" times in static fields, so each test clears them first.
    /// </summary>
    public class AnnouncerWorkerTests : DatabaseTestBase
    {
        private readonly AnnouncerWorker worker;
        private readonly Event theEvent;
        private readonly Dictionary<string, Participant> byBib = [];
        private readonly int previousSeenWindow = Globals.SeenWindow;

        public AnnouncerWorkerTests()
        {
            Static<IList>("Participants").Clear();
            Static<IDictionary>("BibSeen").Clear();
            Globals.SeenWindow = 5;
            worker = (AnnouncerWorker)typeof(AnnouncerWorker)
                .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, [typeof(IMainWindow), typeof(IdbInterface)])!
                .Invoke([FakeMainWindow.Create(out _), Db]);
            theEvent = AddEvent(makeCurrent: true);
            var d = AddDistance(theEvent.Identifier);
            foreach (string bib in new[] { "1", "2" })
            {
                byBib[bib] = AddParticipant(theEvent.Identifier, d, bib, first: $"Runner{bib}");
                Db.AddBibChipAssociation(theEvent.Identifier, [new BibChipAssociation { EventId = theEvent.Identifier, Bib = bib, Chip = $"C{bib}" }]);
            }
        }

        public override void Dispose()
        {
            Globals.SeenWindow = previousSeenWindow;
            Static<IList>("Participants").Clear();
            Static<IDictionary>("BibSeen").Clear();
            base.Dispose();
        }

        private static T Static<T>(string name)
            => (T)typeof(AnnouncerWorker).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

        /// <summary>Saves announcer reads for the given chips, then processes them the way the worker loop does.</summary>
        private bool Process(params string[] chips)
        {
            for (int i = 0; i < chips.Length; i++)
            {
                Db.AddChipRead(new ChipRead(theEvent.Identifier, TimingConstants.LOCATION_ANNOUNCER, chips[i],
                    DateTime.Today.AddHours(8).AddSeconds(Static<IList>("Participants").Count * 10 + i)));
            }
            var reads = Db.GetAnnouncerChipReads(theEvent.Identifier);
            return (bool)typeof(AnnouncerWorker).GetMethod("ProcessReads", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(worker, [reads, byBib])!;
        }

        private List<ChipRead> Reads() => Db.GetChipReadsSafemode(theEvent.Identifier);

        [Fact]
        public void KnownRunner_IsAnnouncedAndReadMarkedUsed()
        {
            Assert.True(Process("C1"));

            var announced = Assert.Single(AnnouncerWorker.GetList());
            Assert.Equal("1", announced.Bib);
            Assert.Equal(TimingConstants.CHIPREAD_STATUS_ANNOUNCER_USED, Assert.Single(Reads()).Status);
        }

        [Fact]
        public void UnknownChip_IsSeenButNotAnnounced()
        {
            Assert.False(Process("NOBODY"));

            Assert.Empty(AnnouncerWorker.GetList());
            Assert.Equal(TimingConstants.CHIPREAD_STATUS_ANNOUNCER_SEEN, Assert.Single(Reads()).Status);
        }

        [Fact]
        public void SameRunnerWithinSeenWindow_IsAnnouncedOnce()
        {
            Process("C1");
            Assert.False(Process("C1"));

            Assert.Single(AnnouncerWorker.GetList());
            Assert.Contains(Reads(), r => r.Status == TimingConstants.CHIPREAD_STATUS_ANNOUNCER_SEEN);
        }

        [Fact]
        public void SeveralRunnersInOneBatch_AreAllAnnounced()
        {
            Assert.True(Process("C1", "C2", "NOBODY"));

            Assert.Equal(["1", "2"], AnnouncerWorker.GetList().Select(p => p.Bib).Order());
            Assert.DoesNotContain(Reads(), r => r.Status == TimingConstants.CHIPREAD_STATUS_NONE);
        }

        [Fact]
        public void SeenWindowZero_AnnouncesOnlyOnceEver()
        {
            Globals.SeenWindow = 0;
            Process("C1");
            Static<IDictionary>("BibSeen")["1"] = DateTime.Now.AddHours(-3);

            Assert.False(Process("C1"));
            Assert.Single(AnnouncerWorker.GetList());
        }

        [Fact]
        public void RunnerSeenAgainAfterWindow_IsAnnouncedAgain()
        {
            Process("C1");
            // Pretend the first sighting was 10 minutes ago (the window is 5 minutes).
            Static<IDictionary>("BibSeen")["1"] = DateTime.Now.AddMinutes(-10);

            Assert.True(Process("C1", "C2"));

            Assert.Equal(["1", "1", "2"], AnnouncerWorker.GetList().Select(p => p.Bib).Order());
            Assert.DoesNotContain(Reads(), r => r.Status == TimingConstants.CHIPREAD_STATUS_NONE);
        }
    }
}
