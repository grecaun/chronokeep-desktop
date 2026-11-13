using Chronokeep.Objects;
using static Chronokeep.Constants.ApiConstants;

namespace Chronokeep.Test.Database
{
    /// <summary>
    /// The database side of the Runsignup integration: the event's registration link, the
    /// Runsignup event → distance map, external ids, and participants marked removed.
    /// </summary>
    public class RegistrationTests : DatabaseTestBase
    {
        private int AddRegistrationApi(string token = "rsu-key")
            => Db.AddApi(new ApiObject(0, CHRONOKEEP_RESULTS_SELF, "rsu-secret", "Runsignup", token, ""));

        private Event LinkToRunsignup(Event e, int apiId, string raceId = "123456", string comments = "q:51234,team_name")
        {
            e.RegistrationApiId = apiId;
            e.RegistrationRaceId = raceId;
            e.RegistrationCommentFields = comments;
            Db.UpdateEvent(e);
            return Db.GetEvent(e.Identifier)!;
        }

        [Fact]
        public void Event_registration_link_round_trips_and_leaves_the_results_link_alone()
        {
            Event e = AddEvent();
            e.ApiId = AddRegistrationApi("results-token");
            e.ApiEventId = "spring-fling,2026";
            Db.UpdateEvent(e);
            int rsu = AddRegistrationApi();

            Event linked = LinkToRunsignup(Db.GetEvent(e.Identifier)!, rsu);

            Assert.Equal(rsu, linked.RegistrationApiId);
            Assert.Equal("123456", linked.RegistrationRaceId);
            Assert.Equal("q:51234,team_name", linked.RegistrationCommentFields);
            Assert.True(linked.IsRegistrationLinked);
            Assert.Equal("spring-fling,2026", linked.ApiEventId);
            Assert.Equal(rsu, Assert.Single(Db.GetEvents()).RegistrationApiId);
        }

        [Fact]
        public void New_events_are_not_registration_linked()
        {
            Event e = Db.GetEvent(AddEvent().Identifier)!;

            Assert.Equal(NULL_ID, e.RegistrationApiId);
            Assert.Equal("", e.RegistrationRaceId);
            Assert.Equal("", e.RegistrationCommentFields);
            Assert.False(e.IsRegistrationLinked);
        }

        [Fact]
        public void External_id_and_removed_reason_round_trip_on_add_and_update()
        {
            Event e = AddEvent();
            Distance d = AddDistance(e.Identifier);
            Participant fresh = NewParticipant(e.Identifier, d, "1");
            fresh.EventSpecific.ExternalId = "rsu-9001";
            Participant added = Db.AddParticipant(fresh)!;

            Participant read = Assert.Single(Db.GetParticipants(e.Identifier));
            Assert.Equal("rsu-9001", read.EventSpecific.ExternalId);
            Assert.False(read.EventSpecific.IsRemoved);

            read.EventSpecific.Removed = "removed_transferred";
            Db.UpdateParticipant(read);

            Participant updated = Db.GetParticipantEventSpecific(e.Identifier, added.EventSpecific.Identifier)!;
            Assert.Equal("removed_transferred", updated.EventSpecific.Removed);
            Assert.True(updated.EventSpecific.IsRemoved);
            Assert.Equal("rsu-9001", updated.EventSpecific.ExternalId);
        }

        [Fact]
        public void Distance_map_is_empty_until_set_and_set_replaces_the_whole_map()
        {
            Event e = AddEvent();
            Distance fiveK = AddDistance(e.Identifier, "5K");
            Distance tenK = AddDistance(e.Identifier, "10K");
            Assert.Empty(Db.GetRegistrationDistances(e.Identifier));

            Db.SetRegistrationDistances(e.Identifier, [
                new(e.Identifier, "700001", "5K Run/Walk", fiveK.Identifier),
                new(e.Identifier, "700002", "10K", tenK.Identifier),
                new(e.Identifier, "700003", "Kids Mile", RegistrationDistance.SkipDistance),
            ]);
            List<RegistrationDistance> first = Db.GetRegistrationDistances(e.Identifier);
            Assert.Equal(3, first.Count);
            Assert.Equal(fiveK.Identifier, first.Single(r => r.RsuEventId == "700001").DistanceId);
            Assert.True(first.Single(r => r.RsuEventId == "700003").IsSkipped);

            Db.SetRegistrationDistances(e.Identifier, [new(e.Identifier, "700002", "10K", fiveK.Identifier)]);
            RegistrationDistance only = Assert.Single(Db.GetRegistrationDistances(e.Identifier));
            Assert.Equal(("700002", fiveK.Identifier), (only.RsuEventId, only.DistanceId));
        }

        [Fact]
        public void Distance_maps_are_kept_per_event()
        {
            Event one = AddEvent("One");
            Event two = AddEvent("Two");
            Db.SetRegistrationDistances(one.Identifier, [new(one.Identifier, "1", "5K", RegistrationDistance.SkipDistance)]);
            Db.SetRegistrationDistances(two.Identifier, [new(two.Identifier, "2", "10K", RegistrationDistance.SkipDistance)]);

            Assert.Equal("1", Assert.Single(Db.GetRegistrationDistances(one.Identifier)).RsuEventId);
            Assert.Equal("2", Assert.Single(Db.GetRegistrationDistances(two.Identifier)).RsuEventId);
        }

        [Fact]
        public void Removing_a_distance_unmaps_its_runsignup_events_instead_of_dropping_them()
        {
            Event e = AddEvent();
            Distance d = AddDistance(e.Identifier);
            Db.SetRegistrationDistances(e.Identifier, [new(e.Identifier, "700001", "5K", d.Identifier)]);

            Db.RemoveDistance(d);

            Assert.True(Assert.Single(Db.GetRegistrationDistances(e.Identifier)).IsSkipped);
        }

        [Fact]
        public void Removing_an_event_removes_its_distance_map()
        {
            Event e = AddEvent();
            Db.SetRegistrationDistances(e.Identifier, [new(e.Identifier, "700001", "5K", RegistrationDistance.SkipDistance)]);

            Db.RemoveEvent(e.Identifier);

            Assert.Equal(0, Count("registration_distances"));
        }

        [Fact]
        public void Removing_the_registration_api_unlinks_its_events_and_drops_their_maps()
        {
            int rsu = AddRegistrationApi();
            int other = AddRegistrationApi("other-key");
            Event linked = LinkToRunsignup(AddEvent("Linked"), rsu);
            Event elsewhere = LinkToRunsignup(AddEvent("Elsewhere"), other, "999");
            Db.SetRegistrationDistances(linked.Identifier, [new(linked.Identifier, "1", "5K", RegistrationDistance.SkipDistance)]);
            Db.SetRegistrationDistances(elsewhere.Identifier, [new(elsewhere.Identifier, "2", "5K", RegistrationDistance.SkipDistance)]);

            Db.RemoveApi(rsu);

            Event unlinked = Db.GetEvent(linked.Identifier)!;
            Assert.Equal(NULL_ID, unlinked.RegistrationApiId);
            Assert.Equal("", unlinked.RegistrationRaceId);
            Assert.Equal("", unlinked.RegistrationCommentFields);
            Assert.Empty(Db.GetRegistrationDistances(linked.Identifier));
            Assert.True(Db.GetEvent(elsewhere.Identifier)!.IsRegistrationLinked);
            Assert.Single(Db.GetRegistrationDistances(elsewhere.Identifier));
        }

        private (Event Event, Participant Active, Participant Removed) SeedWithOneRemoved()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance d = AddDistance(e.Identifier);
            Participant active = AddParticipant(e.Identifier, d, "1", first: "Active");
            Participant removed = AddParticipant(e.Identifier, d, "2", first: "Removed");
            removed.EventSpecific.Removed = "removed_removed";
            Db.UpdateParticipant(removed);
            return (e, active, removed);
        }

        [Fact]
        public void Distance_stats_leave_out_removed_participants()
        {
            (Event e, _, _) = SeedWithOneRemoved();

            DistanceStat stat = Assert.Single(Db.GetDistanceStats(e.Identifier));

            Assert.Equal(1, stat.Total);
        }

        private static TimeResult Finish(Event e, Participant p) =>
            new(e.Identifier, Constants.Timing.TIMERESULT_DUMMYREAD, p.EventSpecific.Identifier,
                Constants.Timing.LOCATION_FINISH, Constants.Timing.SEGMENT_FINISH, 1, 1800, 0, "", 1800, 0,
                DateTime.Now, p.Bib, Constants.Timing.TIMERESULT_STATUS_NONE, "");

        [Fact]
        public void Removed_participants_results_are_kept_and_flagged_but_never_offered_for_upload()
        {
            (Event e, Participant active, Participant removed) = SeedWithOneRemoved();
            Db.AddTimingResults([Finish(e, active), Finish(e, removed)]);

            List<TimeResult> all = Db.GetTimingResults(e.Identifier);
            Assert.Equal(2, all.Count);
            Assert.True(all.Single(r => r.Bib == "2").Removed);
            Assert.False(all.Single(r => r.Bib == "1").Removed);

            Assert.Equal("1", Assert.Single(Db.GetNonUploadedResults(e.Identifier)).Bib);
        }

        [Fact]
        public void Reinstating_a_removed_participant_makes_their_results_uploadable_again()
        {
            (Event e, _, Participant removed) = SeedWithOneRemoved();
            Db.AddTimingResults([Finish(e, removed)]);
            Assert.Empty(Db.GetNonUploadedResults(e.Identifier));

            removed.EventSpecific.Removed = "";
            Db.UpdateParticipant(removed);

            Assert.Equal("2", Assert.Single(Db.GetNonUploadedResults(e.Identifier)).Bib);
        }
    }
}
