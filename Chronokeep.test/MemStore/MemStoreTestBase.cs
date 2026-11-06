using Chronokeep.Database;
using Chronokeep.Objects;
using Chronokeep.Test.Database;
using System.Reflection;
using MemStoreType = Chronokeep.MemStore.MemStore;

namespace Chronokeep.Test.MemStore
{
    /// <summary>
    /// Wraps a fresh temp-file SQLite database in a fresh MemStore.
    /// MemStore is a singleton whose cache lives in static fields, so each test
    /// builds its own instance through the private constructor and clears the statics first.
    /// </summary>
    public abstract class MemStoreTestBase : DatabaseTestBase
    {
        internal MemStoreType Store { get; private set; }

        protected MemStoreTestBase()
        {
            Store = CreateStore(Db);
        }

        private static MemStoreType CreateStore(IdbInterface database)
        {
            ConstructorInfo ctor = typeof(MemStoreType).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance, [typeof(IdbInterface)])!;
            MemStoreType store = (MemStoreType)ctor.Invoke([database]);
            MemStoreType.ResetVariables();
            store.Initialize();
            return store;
        }

        /// <summary>Throws away the cache and reloads everything from the database.</summary>
        internal void Reload()
        {
            Store = CreateStore(Db);
        }

        /// <summary>Adds an event through the MemStore and makes it the current event.</summary>
        internal Event AddCurrentEvent(string name = "Test Race")
        {
            Event e = new() { Name = name, Date = DateString(), YearCode = "2026" };
            Store.AddEvent(e);
            Store.SetCurrentEvent(e.Identifier);
            return e;
        }

        internal Distance AddStoreDistance(int eventId, string name = "5K", int linkedId = Constants.Timing.DISTANCE_NO_LINKED_ID)
        {
            Distance d = new(name, eventId) { DistanceValue = 3.1, LinkedDistance = linkedId };
            Store.AddDistance(d);
            return d;
        }

        internal Participant AddStoreParticipant(int eventId, Distance distance, string bib,
            string first = "Jane", string last = "Runner", string birthday = "01/02/1990")
        {
            Participant? p = Store.AddParticipant(NewParticipant(eventId, distance, bib, first, last, birthday));
            Assert.NotNull(p);
            return p;
        }
    }
}
