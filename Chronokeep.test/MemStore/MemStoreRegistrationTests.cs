using Chronokeep.Objects;
using static Chronokeep.Constants.ApiConstants;

namespace Chronokeep.Test.MemStore
{
    /// <summary>The in-memory store's side of the Runsignup database changes.</summary>
    public class MemStoreRegistrationTests : MemStoreTestBase
    {
        [Fact]
        public void Distance_map_passes_through_to_the_database()
        {
            Event e = AddCurrentEvent();
            Distance d = AddStoreDistance(e.Identifier);

            Store.SetRegistrationDistances(e.Identifier, [new(e.Identifier, "700001", "5K", d.Identifier)]);

            Assert.Equal(d.Identifier, Assert.Single(Store.GetRegistrationDistances(e.Identifier)).DistanceId);
            Assert.Single(Db.GetRegistrationDistances(e.Identifier));
        }

        [Fact]
        public void Registration_link_is_cached_with_the_current_event()
        {
            Event e = AddCurrentEvent();
            int api = Store.AddApi(new ApiObject(0, CHRONOKEEP_RESULTS_SELF, "secret", "Runsignup", "key", ""));
            e.RegistrationApiId = api;
            e.RegistrationRaceId = "123456";
            Store.UpdateEvent(e);

            Assert.True(Store.GetCurrentEvent()!.IsRegistrationLinked);
            Assert.Equal("123456", Db.GetEvent(e.Identifier)!.RegistrationRaceId);
        }

        [Fact]
        public void Removing_an_api_clears_both_links_on_the_cached_event()
        {
            Event e = AddCurrentEvent();
            int api = Store.AddApi(new ApiObject(0, CHRONOKEEP_RESULTS_SELF, "secret", "Runsignup", "key", ""));
            e.ApiId = api;
            e.ApiEventId = "slug,2026";
            e.RegistrationApiId = api;
            e.RegistrationRaceId = "123456";
            Store.UpdateEvent(e);

            Store.RemoveApi(api);

            Event cached = Store.GetCurrentEvent()!;
            Assert.Equal(NULL_ID, cached.ApiId);
            Assert.Equal(NULL_EVENT_ID, cached.ApiEventId);
            Assert.False(cached.IsRegistrationLinked);
        }

        [Fact]
        public void Distance_stats_leave_out_removed_participants()
        {
            Event e = AddCurrentEvent();
            Distance d = AddStoreDistance(e.Identifier);
            AddStoreParticipant(e.Identifier, d, "1", first: "Active");
            Participant removed = AddStoreParticipant(e.Identifier, d, "2", first: "Removed");
            removed.EventSpecific.Removed = "removed_cleared";
            Store.UpdateParticipant(removed);

            DistanceStat stat = Assert.Single(Store.GetDistanceStats(e.Identifier));

            Assert.Equal(1, stat.Total);
        }
    }
}
