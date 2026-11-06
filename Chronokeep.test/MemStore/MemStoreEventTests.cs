using Chronokeep.Objects;

namespace Chronokeep.Test.MemStore
{
    public class MemStoreEventTests : MemStoreTestBase
    {
        [Fact]
        public void AddEvent_CachesAndPersists()
        {
            Event e = new() { Name = "Race", Date = DateString() };
            int id = Store.AddEvent(e);
            Assert.True(id > 0);
            Assert.Equal(id, e.Identifier);
            Assert.Equal("Race", Store.GetEvent(id)!.Name);
            Assert.NotNull(Db.GetEvent(id));
        }

        [Fact]
        public void GetEventId_MatchesNameAndDateIgnoringCase()
        {
            Event e = new() { Name = "Race", Date = DateString() };
            Store.AddEvent(e);
            Assert.Equal(e.Identifier, Store.GetEventId(new Event { Name = "RACE", Date = e.Date }));
            Assert.Equal(-1, Store.GetEventId(new Event { Name = "Other", Date = e.Date }));
        }

        [Fact]
        public void SetCurrentEvent_LoadsThatEvent()
        {
            var a = AddEvent("A");
            var b = AddEvent("B");
            AddDistance(a.Identifier, "A-5K");
            AddDistance(b.Identifier, "B-5K");
            Reload();

            Store.SetCurrentEvent(b.Identifier);

            Assert.Equal(b.Identifier, Store.GetCurrentEvent()!.Identifier);
            Assert.Equal("B-5K", Assert.Single(Store.GetDistances(b.Identifier)).Name);
            Assert.Equal(b.Identifier.ToString(), Db.GetAppSetting(Constants.Settings.CURRENT_EVENT)!.Value);
        }

        [Fact]
        public void SetCurrentEvent_SwitchingBackReloadsTimingResults()
        {
            var a = AddCurrentEvent("A");
            var d = AddStoreDistance(a.Identifier);
            var p = AddStoreParticipant(a.Identifier, d, "1");
            Store.AddTimingResult(new TimeResult(a.Identifier, Constants.Timing.TIMERESULT_DUMMYREAD, p.EventSpecific.Identifier,
                Constants.Timing.LOCATION_FINISH, Constants.Timing.SEGMENT_FINISH, 1, 1800, 0, "", 1800, 0, DateTime.Now, "1",
                Constants.Timing.TIMERESULT_STATUS_NONE, ""));
            var b = AddCurrentEvent("B");

            Store.SetCurrentEvent(a.Identifier);

            Assert.Single(Store.GetTimingResults(a.Identifier));
        }

        [Fact]
        public void UpdateEvent_UpdatesCurrentAndListedEvent()
        {
            var e = AddCurrentEvent();
            var changed = Store.GetEvent(e.Identifier)!;
            changed.Name = "Renamed";
            Store.UpdateEvent(changed);

            Assert.Equal("Renamed", Store.GetCurrentEvent()!.Name);
            Assert.Equal("Renamed", Store.GetEvents().Single().Name);
            Assert.Equal("Renamed", Db.GetEvent(e.Identifier)!.Name);
        }

        [Fact]
        public void SetStartOptions_UpdatesCurrentEvent()
        {
            var e = AddCurrentEvent();
            e.StartWindow = 90;
            e.StartMaxOccurrences = 3;
            Store.SetStartOptions(e);

            var current = Store.GetCurrentEvent()!;
            Assert.Equal(90, current.StartWindow);
            Assert.Equal(3, current.StartMaxOccurrences);
        }

        [Fact]
        public void SetFinishOptions_UpdatesCurrentEvent()
        {
            var e = AddCurrentEvent();
            e.FinishMaxOccurrences = 4;
            e.FinishIgnoreWithin = 30;
            Store.SetFinishOptions(e);

            var current = Store.GetCurrentEvent()!;
            Assert.Equal(4, current.FinishMaxOccurrences);
            Assert.Equal(30, current.FinishIgnoreWithin);
            Assert.Equal(4, Db.GetEvent(e.Identifier)!.FinishMaxOccurrences);
        }

        [Fact]
        public void SetStartFinishOptions_UpdatesCurrentEvent()
        {
            var e = AddCurrentEvent();
            e.StartWindow = 90;
            e.StartMaxOccurrences = 3;
            e.FinishMaxOccurrences = 4;
            e.FinishIgnoreWithin = 30;
            Store.SetStartFinishOptions(e);

            var current = Store.GetCurrentEvent()!;
            Assert.Equal(90, current.StartWindow);
            Assert.Equal(3, current.StartMaxOccurrences);
            Assert.Equal(4, current.FinishMaxOccurrences);
            Assert.Equal(30, current.FinishIgnoreWithin);
        }

        [Fact]
        public void RemoveEvent_Current_ClearsLoadedData()
        {
            var e = AddCurrentEvent();
            AddStoreDistance(e.Identifier);

            Store.RemoveEvent(e.Identifier);

            Assert.Empty(Store.GetEvents());
            Assert.Null(Store.GetCurrentEvent());
            Assert.Null(Db.GetEvent(e.Identifier));
        }

        [Fact]
        public void RemoveEvent_Other_KeepsCurrentEvent()
        {
            var other = new Event { Name = "Other", Date = DateString() };
            Store.AddEvent(other);
            var e = AddCurrentEvent();

            Store.RemoveEvent(other);

            Assert.Equal(e.Identifier, Store.GetCurrentEvent()!.Identifier);
            Assert.Single(Store.GetEvents());
        }

        [Fact]
        public void UpdateStart_SetsChipReadStartFromEventStart()
        {
            var e = AddCurrentEvent();
            e.StartSeconds = 3600;
            Store.UpdateEvent(e);
            Store.AddChipRead(NewRead(e.Identifier, "1"));

            Store.UpdateStart();

            var expected = DateTime.Parse(e.Date).AddSeconds(3600);
            Assert.Equal(expected, Assert.Single(Store.GetChipReads(e.Identifier)).Start);
        }
    }
}
