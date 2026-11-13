using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Helpers;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.Util;

namespace Chronokeep.Test.UI
{
    /// <summary>Only one copy of Chronokeep runs; a second one says so instead of crashing.</summary>
    public class SingleInstanceTests : UiTestBase
    {
        private static string UniqueName() => $"chronokeep-test-{Guid.NewGuid():N}";

        /// <summary>Holds the lock on its own thread, like another running copy, until told to let go.</summary>
        private static (Thread Holder, ManualResetEventSlim LetGo) AnotherCopy(string name)
        {
            ManualResetEventSlim held = new(), letGo = new();
            Thread holder = new(() =>
            {
                using Mutex mutex = new(true, name, out _);
                held.Set();
                letGo.Wait();
                mutex.ReleaseMutex();
            }) { IsBackground = true };
            holder.Start();
            held.Wait();
            return (holder, letGo);
        }

        [AvaloniaFact]
        public void The_only_copy_gets_the_claim_and_keeps_it()
        {
            string name = UniqueName();

            Assert.True(SingleInstance.Claim(name));
            Assert.True(SingleInstance.Claim(name));
        }

        [AvaloniaFact]
        public void A_second_copy_is_refused_while_the_first_runs()
        {
            string name = UniqueName();
            (Thread holder, ManualResetEventSlim letGo) = AnotherCopy(name);

            Assert.False(SingleInstance.Claim(name));

            letGo.Set();
            holder.Join();
            Assert.True(SingleInstance.Claim(name));
        }

        [AvaloniaFact]
        public void A_second_copy_opens_only_a_message_and_closes_when_it_is_dismissed()
        {
            int shutdowns = 0;

            Window first = App.FirstWindow(safeMode: false, alreadyRunning: true, () => shutdowns++);

            DialogBox message = Assert.IsType<DialogBox>(first);
            Show(message);
            Assert.True(message.IsVisible);
            Assert.Equal("Chronokeep is already running.", DialogText(message));
            Click(Find<Button>(message, "RightButton"));
            Pump();
            Assert.Equal(1, shutdowns);
            Assert.False(message.IsVisible);
        }
    }
}
