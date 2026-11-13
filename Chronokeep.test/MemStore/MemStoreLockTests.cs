using Chronokeep.Objects;
using System.Reflection;
using MemStoreType = Chronokeep.MemStore.MemStore;

namespace Chronokeep.Test.MemStore
{
    /// <summary>
    /// The memory store guards its cache with one lock. Timing, uploads and the window all use it at
    /// once, so reads and writes have to cope with it being busy for a moment.
    /// </summary>
    public class MemStoreLockTests : MemStoreTestBase
    {
        /// <summary>Holds the memory store's lock on another thread for a while, like a long timing pass.</summary>
        private static Task HoldLock(int ms, ManualResetEventSlim held)
        {
            Lock storeLock = (Lock)typeof(MemStoreType).GetField("memStoreLock", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            return Task.Factory.StartNew(() =>
            {
                storeLock.Enter();
                try
                {
                    held.Set();
                    Thread.Sleep(ms);
                }
                finally
                {
                    storeLock.Exit();
                }
            }, TaskCreationOptions.LongRunning);
        }

        [Fact]
        public void A_read_waits_out_a_short_busy_spell()
        {
            Event e = AddCurrentEvent();
            AddStoreDistance(e.Identifier, "5K");
            using ManualResetEventSlim held = new();
            Task holder = HoldLock(500, held);
            held.Wait();

            List<Distance> distances = Store.GetDistances(e.Identifier);

            Assert.Equal("5K", Assert.Single(distances).Name);
            holder.Wait();
        }

        [Fact]
        public void A_write_during_a_short_busy_spell_reaches_the_cache()
        {
            Event e = AddCurrentEvent();
            using ManualResetEventSlim held = new();
            Task holder = HoldLock(500, held);
            held.Wait();

            AddStoreDistance(e.Identifier, "10K");
            holder.Wait();

            Assert.Equal("10K", Assert.Single(Store.GetDistances(e.Identifier)).Name);
        }

        [Fact]
        public void A_write_during_a_long_busy_spell_still_reaches_the_cache()
        {
            Event e = AddCurrentEvent();
            using ManualResetEventSlim held = new();
            Task holder = HoldLock(6000, held);
            held.Wait();

            AddStoreDistance(e.Identifier, "10K");
            holder.Wait();

            // The database has it either way; the cache must too, or reads miss it until a reload.
            Assert.Equal("10K", Assert.Single(Db.GetDistances(e.Identifier)).Name);
            Assert.Equal("10K", Assert.Single(Store.GetDistances(e.Identifier)).Name);
        }
    }
}
