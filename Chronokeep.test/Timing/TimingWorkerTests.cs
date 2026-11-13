using Chronokeep.Database;
using Chronokeep.Objects;
using Chronokeep.Timing;
using System.Reflection;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Timing
{
    /// <summary>
    /// The timing worker's loop: woken by Notify, it scores new reads with the routine for the race
    /// type, places the results, tells the window, and keeps going until Shutdown.
    /// </summary>
    public class TimingWorkerTests : RoutineTestBase
    {
        private Thread? thread;

        public TimingWorkerTests() => ResetStatics();

        public override void Dispose()
        {
            TimingWorker.Shutdown();
            TimingWorker.Notify();
            thread?.Join(TimeSpan.FromSeconds(5));
            ResetStatics();
            base.Dispose();
        }

        /// <summary>The worker keeps its state in statics; start each test from a fresh, idle worker.</summary>
        private static void ResetStatics()
        {
            Type type = typeof(TimingWorker);
            type.GetField("worker", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, null);
            type.GetField("quittingTime", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, false);
            type.GetField("resetDictionariesBool", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, true);
            type.GetField("newResults", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, false);
            Semaphore semaphore = (Semaphore)type.GetField("Semaphore", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            while (semaphore.WaitOne(0)) { }
        }

        private void StartWorker(IdbInterface? database = null)
        {
            TimingWorker worker = TimingWorker.NewWorker(Window, database ?? Db);
            thread = new Thread(worker.Run) { IsBackground = true };
            thread.Start();
        }

        private static bool WaitFor(Func<bool> condition, int timeoutMs = 8000)
        {
            DateTime end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < end)
            {
                if (condition()) return true;
                Thread.Sleep(25);
            }
            return condition();
        }

        private int CallCount(string name) => FakeWindow.Calls.Count(c => c == name);

        [Fact]
        public void Distance_reads_are_scored_and_placed_when_woken()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1");
            AddRunner(d, "2");
            ReadAt(Chip("1"), 1500);
            ReadAt(Chip("2"), 1400);
            StartWorker();

            TimingWorker.Notify();

            Assert.True(WaitFor(() => Results().Count(r => r.Place > 0) == 2), "The reads were never scored and placed.");
            Assert.Equal(1, FinishFor("2").Place);
            Assert.True(WaitFor(() => CallCount("UpdateTiming") > 0 && CallCount("NetworkUpdateResults") > 0));
            Assert.True(TimingWorker.NewResultsExist());
            Assert.False(TimingWorker.NewResultsExist());
        }

        [Fact]
        public void Time_based_reads_become_laps()
        {
            SetupEvent(TimingConstants.EVENT_TYPE_TIME, e => e.FinishMaxOccurrences = 10);
            var d = AddRaceDistance(configure: x => x.EndSeconds = 3600);
            AddRunner(d, "1");
            ReadAt(Chip("1"), 600);
            ReadAt(Chip("1"), 1200);
            StartWorker();

            TimingWorker.Notify();

            Assert.True(WaitFor(() => LapsFor("1").Count == 2 && LapsFor("1").All(l => l.Place > 0)), "The laps were never scored.");
        }

        [Fact]
        public void Nothing_happens_without_a_current_event()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1");
            ReadAt(Chip("1"), 1500);
            int eventId = TheEvent.Identifier;
            Db.SetCurrentEvent(-1);
            StartWorker();

            TimingWorker.Notify();
            Thread.Sleep(300);
            Assert.Empty(Db.GetTimingResults(eventId));

            Db.SetCurrentEvent(eventId);
            TimingWorker.Notify();
            Assert.True(WaitFor(() => Db.GetTimingResults(eventId).Count == 1), "Scoring didn't start once there was an event.");
        }

        [Fact]
        public void Runners_added_later_are_known_after_the_dictionaries_reset()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1");
            ReadAt(Chip("1"), 1500);
            StartWorker();
            TimingWorker.Notify();
            Assert.True(WaitFor(() => Results().Count == 1));

            AddRunner(d, "2");
            ReadAt(Chip("2"), 1600);
            TimingWorker.ResetDictionaries();
            TimingWorker.Notify();

            Assert.True(WaitFor(() => Results().Any(r => r.Bib == "2" && r.Place == 2)), "The new runner wasn't scored as a known runner.");
        }

        [Fact]
        public void Shutdown_ends_the_loop()
        {
            SetupEvent();
            StartWorker();

            TimingWorker.Shutdown();
            TimingWorker.Notify();

            Assert.True(thread!.Join(TimeSpan.FromSeconds(5)), "The worker didn't stop.");
        }

        [Fact]
        public void A_pass_that_fails_doesnt_stop_scoring()
        {
            SetupEvent();
            var d = AddRaceDistance();
            AddRunner(d, "1");
            ReadAt(Chip("1"), 1500);
            IdbInterface failingOnce = FailOnceDatabase.Wrap(Db, nameof(IdbInterface.UnprocessedReadsExist));
            StartWorker(failingOnce);

            TimingWorker.Notify();
            Thread.Sleep(300);
            TimingWorker.Notify();

            Assert.True(WaitFor(() => Results().Count == 1), "Scoring stopped for good after one failed pass.");
            Assert.True(thread!.IsAlive);
        }
    }

    /// <summary>A database that throws the first time one method is called, then works normally.</summary>
    public class FailOnceDatabase : DispatchProxy
    {
        private IdbInterface inner = null!;
        private string failing = "";
        private bool failed;

        internal static IdbInterface Wrap(IdbInterface inner, string method)
        {
            IdbInterface proxy = Create<IdbInterface, FailOnceDatabase>();
            FailOnceDatabase self = (FailOnceDatabase)(object)proxy;
            self.inner = inner;
            self.failing = method;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (!failed && targetMethod!.Name == failing)
            {
                failed = true;
                throw new InvalidOperationException("Database is busy.");
            }
            try
            {
                return targetMethod!.Invoke(inner, args);
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                throw e.InnerException;
            }
        }
    }
}
