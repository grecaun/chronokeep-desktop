using Chronokeep.Database;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.Test.Database;
using Chronokeep.Timing;
using System.Reflection;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Timing
{
    /// <summary>
    /// Records NotifyAlarm calls and ignores everything else the main window would do.
    /// </summary>
    public class FakeMainWindow : DispatchProxy
    {
        public List<(string Bib, string Chip)> Alarms { get; } = [];

        /// <summary>What InDidNotStartMode() returns.</summary>
        public bool DnsMode { get; set; }

        /// <summary>The name of every method called on the window, in order.</summary>
        public List<string> Calls { get; } = [];

        internal static IMainWindow Create(out FakeMainWindow fake)
        {
            IMainWindow window = Create<IMainWindow, FakeMainWindow>();
            fake = (FakeMainWindow)(object)window;
            return window;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            lock (Calls)
            {
                Calls.Add(targetMethod?.Name ?? "");
            }
            if (targetMethod?.Name == nameof(IMainWindow.NotifyAlarm))
            {
                Alarms.Add(((string)args![0]!, (string)args[1]!));
            }
            if (targetMethod?.Name == nameof(IMainWindow.InDidNotStartMode))
            {
                return DnsMode;
            }
            Type? ret = targetMethod?.ReturnType;
            return ret == null || ret == typeof(void) || !ret.IsValueType ? null : Activator.CreateInstance(ret);
        }
    }

    /// <summary>
    /// Sets up an event in a temp database and runs the timing routines the same way TimingWorker does.
    /// Race clock: the gun goes off at 8:00:00 on the event date.
    /// </summary>
    public abstract class RoutineTestBase : DatabaseTestBase
    {
        protected static readonly DateTime Gun = new(2026, 6, 15, 8, 0, 0);

        internal Event TheEvent { get; private set; } = null!;
        internal IMainWindow Window { get; }
        internal FakeMainWindow FakeWindow { get; }

        protected RoutineTestBase()
        {
            Window = FakeMainWindow.Create(out FakeMainWindow fake);
            FakeWindow = fake;
        }

        internal Event SetupEvent(int type = TimingConstants.EVENT_TYPE_DISTANCE, Action<Event>? configure = null)
        {
            Event e = new()
            {
                Name = "Routine Race",
                Date = Gun.Date.ToShortDateString(),
                EventType = type,
                StartSeconds = (long)Gun.TimeOfDay.TotalSeconds,
                StartMilliseconds = 0,
                StartWindow = 60,
                FinishIgnoreWithin = 120,
                FinishMaxOccurrences = 1,
            };
            configure?.Invoke(e);
            e.Identifier = Db.AddEvent(e);
            Db.UpdateEvent(e);
            Db.SetCurrentEvent(e.Identifier);
            TheEvent = Db.GetEvent(e.Identifier)!;
            return TheEvent;
        }

        internal Distance AddRaceDistance(string name = "5K", Action<Distance>? configure = null)
        {
            Distance d = new(name, TheEvent.Identifier) { DistanceValue = 3.1 };
            configure?.Invoke(d);
            d.Identifier = Db.AddDistance(d);
            Db.UpdateDistance(d);
            return Db.GetDistance(d.Identifier)!;
        }

        /// <summary>Adds a runner and links chip "C{bib}" to their bib.</summary>
        internal Participant AddRunner(Distance d, string bib, string first = "Runner", string gender = "F",
            string birthday = "01/02/1990", string division = "")
        {
            Participant p = AddParticipant(TheEvent.Identifier, d, bib, first, $"Bib{bib}", birthday, gender, division);
            Db.AddBibChipAssociation(TheEvent.Identifier, [new BibChipAssociation { EventId = TheEvent.Identifier, Bib = bib, Chip = Chip(bib) }]);
            return p;
        }

        internal static string Chip(string bib) => $"C{bib}";

        /// <summary>A chip read at the given number of seconds after the gun.</summary>
        internal ChipRead ReadAt(string chip, double secondsAfterGun, int location = TimingConstants.LOCATION_FINISH)
        {
            ChipRead read = new(TheEvent.Identifier, location, chip, Gun.AddSeconds(secondsAfterGun));
            read.ReadId = Db.AddChipRead(read);
            return read;
        }

        /// <summary>A manually entered read for a bib (used for DNF entries).</summary>
        internal ChipRead ManualAt(string bib, double secondsAfterGun, int status, int location = TimingConstants.LOCATION_FINISH)
        {
            ChipRead read = new(TheEvent.Identifier, location, bib, Gun.AddSeconds(secondsAfterGun), status);
            read.ReadId = Db.AddChipRead(read);
            return read;
        }

        /// <summary>Builds the dictionary exactly as TimingWorker does before each processing pass.</summary>
        internal TimingDictionary BuildDictionary()
        {
            Type workerType = typeof(TimingWorker);
            object worker = workerType.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance,
                [typeof(IMainWindow), typeof(IdbInterface)])!.Invoke([Window, Db]);
            TimingDictionary dictionary = (TimingDictionary)workerType
                .GetField("Dictionary", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            workerType.GetMethod("RecalculateDictionaries", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(worker, [TheEvent]);
            return dictionary;
        }

        /// <summary>Runs one TimingWorker pass: process reads, then placements.</summary>
        internal void ProcessDistance()
        {
            TimingDictionary dictionary = BuildDictionary();
            if (Db.UnprocessedReadsExist(TheEvent.Identifier))
            {
                Chronokeep.Timing.Routines.DistanceRoutine.ProcessRace(TheEvent, Db, dictionary, Window);
            }
            if (Db.UnprocessedResultsExist(TheEvent.Identifier))
            {
                Chronokeep.Timing.Routines.DistanceRoutine.ProcessPlacements(TheEvent, Db, dictionary);
            }
        }

        /// <summary>Runs one TimingWorker pass for a time-based event.</summary>
        internal void ProcessTime()
        {
            TimingDictionary dictionary = BuildDictionary();
            if (Db.UnprocessedReadsExist(TheEvent.Identifier))
            {
                Chronokeep.Timing.Routines.TimeRoutine.ProcessRace(TheEvent, Db, dictionary, Window);
            }
            if (Db.UnprocessedResultsExist(TheEvent.Identifier))
            {
                Chronokeep.Timing.Routines.TimeRoutine.ProcessLapTimes(TheEvent, Db);
                Chronokeep.Timing.Routines.TimeRoutine.ProcessPlacements(TheEvent, Db, dictionary);
            }
        }

        /// <summary>Runs one TimingWorker pass for a backyard ultra.</summary>
        internal void ProcessBackyard()
        {
            TimingDictionary dictionary = BuildDictionary();
            if (Db.UnprocessedReadsExist(TheEvent.Identifier))
            {
                Chronokeep.Timing.Routines.BackyardUltraRoutine.ProcessRace(TheEvent, Db, dictionary, Window);
            }
            if (Db.UnprocessedResultsExist(TheEvent.Identifier))
            {
                Chronokeep.Timing.Routines.BackyardUltraRoutine.ProcessPlacements(TheEvent, Db, dictionary);
            }
        }

        internal List<TimeResult> Results() => Db.GetTimingResults(TheEvent.Identifier);

        internal List<TimeResult> LapsFor(string bib)
            => [.. Results().Where(r => r.Bib == bib && r.SegmentId == TimingConstants.SEGMENT_FINISH).OrderBy(r => r.Occurrence)];

        internal TimeResult FinishFor(string bib)
            => Assert.Single(Results(), r => r.Bib == bib && r.SegmentId == TimingConstants.SEGMENT_FINISH);

        internal ChipRead ReadById(int readId) => Assert.Single(Db.GetChipReads(TheEvent.Identifier), r => r.ReadId == readId);

        internal Participant Runner(string bib) => Db.GetParticipantBib(TheEvent.Identifier, bib)!;
    }
}
