using Chronokeep.Objects;

namespace Chronokeep.Test.MemStore
{
    public class MemStoreDistanceTests : MemStoreTestBase
    {
        [Fact]
        public void AddDistance_CurrentEvent_CachesAndPersists()
        {
            var e = AddCurrentEvent();
            var d = AddStoreDistance(e.Identifier);
            Assert.True(d.Identifier > 0);
            Assert.Equal("5K", Store.GetDistance(d.Identifier)!.Name);
            Assert.Equal(d.Identifier, Store.GetDistanceId(new Distance("5k", e.Identifier)));
            Assert.Single(Db.GetDistances(e.Identifier));
        }

        [Fact]
        public void AddDistance_OtherEvent_IsNotCachedButQueriesFallBackToDatabase()
        {
            AddCurrentEvent();
            var other = AddEvent("Other");
            var d = AddStoreDistance(other.Identifier);

            Assert.Null(Store.GetDistance(d.Identifier));
            Assert.Single(Store.GetDistances(other.Identifier));
        }

        [Fact]
        public void AddDistances_CachesEveryDistance()
        {
            var e = AddCurrentEvent();
            var added = Store.AddDistances([
                new Distance("5K", e.Identifier),
                new Distance("10K", e.Identifier),
                new Distance("Half", e.Identifier)]);

            Assert.Equal(3, added.Count);
            Assert.Equal(3, Store.GetDistances(e.Identifier).Count);
            Assert.All(added, d => Assert.NotNull(Store.GetDistance(d.Identifier)));
        }

        [Fact]
        public void UpdateDistance_RenamesAndUpdatesParticipants()
        {
            var e = AddCurrentEvent();
            var d = AddStoreDistance(e.Identifier);
            AddStoreParticipant(e.Identifier, d, "1");

            var changed = new Distance("5 Kilometer", e.Identifier) { Identifier = d.Identifier, Wave = 2 };
            Store.UpdateDistance(changed);

            Assert.Equal("5 Kilometer", Store.GetDistance(d.Identifier)!.Name);
            Assert.Equal(2, Store.GetDistance(d.Identifier)!.Wave);
            Assert.Equal("5 Kilometer", Store.GetParticipants(e.Identifier).Single().Distance);
            Assert.Equal("5 Kilometer", Db.GetDistance(d.Identifier)!.Name);
        }

        [Fact]
        public void RemoveDistance_ByObject_RemovesDistanceAndItsParticipants()
        {
            var e = AddCurrentEvent();
            var d = AddStoreDistance(e.Identifier);
            var keep = AddStoreDistance(e.Identifier, "10K");
            AddStoreParticipant(e.Identifier, d, "1");
            AddStoreParticipant(e.Identifier, keep, "2", first: "Keep");

            Store.RemoveDistance(d);

            Assert.Null(Store.GetDistance(d.Identifier));
            Assert.Equal("2", Assert.Single(Store.GetParticipants(e.Identifier)).Bib);
            Assert.Null(Db.GetDistance(d.Identifier));
        }

        [Fact]
        public void RemoveDistance_ById_ForgetsTheDistanceName()
        {
            var e = AddCurrentEvent();
            var d = AddStoreDistance(e.Identifier, "Doomed");
            Store.RemoveDistance(d.Identifier);

            Assert.Null(Store.GetDistance(d.Identifier));
            Assert.Equal(-1, Store.GetDistanceId(new Distance("Doomed", e.Identifier)));

            // A new distance reusing the name must be what results resolve to.
            var replacement = AddStoreDistance(e.Identifier, "Doomed");
            Assert.Equal(replacement.Identifier, Store.GetDistanceId(new Distance("Doomed", e.Identifier)));
        }

        [Fact]
        public void SetWaveTimes_UpdatesCachedDistancesInWave()
        {
            var e = AddCurrentEvent();
            Store.AddDistance(new Distance("Wave1", e.Identifier) { Wave = 1 });
            var w2 = new Distance("Wave2", e.Identifier) { Wave = 2 };
            Store.AddDistance(w2);

            Store.SetWaveTimes(e.Identifier, 2, 300, 500);

            Assert.Equal(300, Store.GetDistance(w2.Identifier)!.StartOffsetSeconds);
            Assert.Equal(500, Store.GetDistance(w2.Identifier)!.StartOffsetMilliseconds);
            Assert.All(Store.GetDistances(e.Identifier).Where(d => d.Wave == 1), d => Assert.Equal(0, d.StartOffsetSeconds));
        }
    }

    public class MemStoreTimingLocationTests : MemStoreTestBase
    {
        [Fact]
        public void GetTimingLocations_ExcludesBuiltInLocations()
        {
            var e = AddCurrentEvent();
            Assert.Empty(Store.GetTimingLocations(e.Identifier));
            Store.AddTimingLocation(new TimingLocation(e.Identifier, "Mile 1"));
            Assert.Equal("Mile 1", Assert.Single(Store.GetTimingLocations(e.Identifier)).Name);
        }

        [Fact]
        public void AddTimingLocations_CachesAll()
        {
            var e = AddCurrentEvent();
            Store.AddTimingLocations([new TimingLocation(e.Identifier, "A"), new TimingLocation(e.Identifier, "B")]);
            Assert.Equal(2, Store.GetTimingLocations(e.Identifier).Count);
        }

        [Fact]
        public void GetTimingLocationId_MatchesNameIgnoringCase()
        {
            var e = AddCurrentEvent();
            int id = Store.AddTimingLocation(new TimingLocation(e.Identifier, "Turnaround"));
            Assert.Equal(id, Store.GetTimingLocationId(new TimingLocation(e.Identifier, "TURNAROUND")));
            Assert.Equal(-1, Store.GetTimingLocationId(new TimingLocation(e.Identifier, "Missing")));
        }

        [Fact]
        public void UpdateAndRemoveTimingLocation()
        {
            var e = AddCurrentEvent();
            int id = Store.AddTimingLocation(new TimingLocation(e.Identifier, "A"));
            Store.UpdateTimingLocation(new TimingLocation(id, e.Identifier, "Renamed", 2, 30));
            Assert.Equal("Renamed", Store.GetTimingLocations(e.Identifier).Single().Name);

            Store.RemoveTimingLocation(id);
            Assert.Empty(Store.GetTimingLocations(e.Identifier));
            Assert.Empty(Db.GetTimingLocations(e.Identifier));
        }
    }

    public class MemStoreSegmentTests : MemStoreTestBase
    {
        private static Segment NewSegment(int eventId, int distanceId, int occurrence)
            => new(eventId, distanceId, Constants.Timing.LOCATION_FINISH, occurrence, 1.0, occurrence, 0, $"Lap {occurrence}", "", "");

        [Fact]
        public void AddSegment_CachesUnderNewId()
        {
            var e = AddCurrentEvent();
            var d = AddStoreDistance(e.Identifier);
            var first = NewSegment(e.Identifier, d.Identifier, 1);
            var second = NewSegment(e.Identifier, d.Identifier, 2);
            int firstId = Store.AddSegment(first);
            int secondId = Store.AddSegment(second);

            Assert.Equal(firstId, first.Identifier);
            Assert.Equal(2, Store.GetSegments(e.Identifier).Count);
            Assert.Equal(secondId, Store.GetSegmentId(NewSegment(e.Identifier, d.Identifier, 2)));
        }

        [Fact]
        public void AddSegments_CachesAll_AndCountsMax()
        {
            var e = AddCurrentEvent();
            var d1 = AddStoreDistance(e.Identifier, "5K");
            var d2 = AddStoreDistance(e.Identifier, "10K");
            Store.AddSegments([
                NewSegment(e.Identifier, d1.Identifier, 1),
                NewSegment(e.Identifier, d2.Identifier, 1),
                NewSegment(e.Identifier, d2.Identifier, 2)]);

            Assert.Equal(3, Store.GetSegments(e.Identifier).Count);
            Assert.Equal(2, Store.GetMaxSegments(e.Identifier));
        }

        [Fact]
        public void UpdateRemoveAndResetSegments()
        {
            var e = AddCurrentEvent();
            var d = AddStoreDistance(e.Identifier);
            var segs = Store.AddSegments([
                NewSegment(e.Identifier, d.Identifier, 1),
                NewSegment(e.Identifier, d.Identifier, 2),
                NewSegment(e.Identifier, d.Identifier, 3),
                NewSegment(e.Identifier, d.Identifier, 4)]);

            segs[0].Name = "Renamed";
            Store.UpdateSegment(segs[0]);
            Assert.Contains(Store.GetSegments(e.Identifier), s => s.Name == "Renamed");

            Store.RemoveSegment(segs[0]);
            Store.RemoveSegments([segs[1]]);
            Assert.Equal(2, Store.GetSegments(e.Identifier).Count);

            Store.ResetSegments(e.Identifier);
            Assert.Empty(Store.GetSegments(e.Identifier));
            Assert.Empty(Db.GetSegments(e.Identifier));
        }
    }

    public class MemStoreAgeGroupTests : MemStoreTestBase
    {
        [Fact]
        public void AddAgeGroups_CachesByDistance()
        {
            var e = AddCurrentEvent();
            var d = AddStoreDistance(e.Identifier);
            Store.AddAgeGroups([
                new AgeGroup(e.Identifier, d.Identifier, 0, 29),
                new AgeGroup(e.Identifier, d.Identifier, 30, 99)]);
            Store.AddAgeGroup(new AgeGroup(e.Identifier, Constants.Timing.COMMON_AGEGROUPS_DISTANCEID, 0, 99));

            Assert.Equal(3, Store.GetAgeGroups(e.Identifier).Count);
            Assert.Equal(2, Store.GetAgeGroups(e.Identifier, d.Identifier).Count);
        }

        [Fact]
        public void UpdateAgeGroup_UpdatesCachedGroup()
        {
            var e = AddCurrentEvent();
            var g = new AgeGroup(e.Identifier, -1, 20, 29);
            Store.AddAgeGroup(g);
            Store.UpdateAgeGroup(new AgeGroup(g.GroupId, e.Identifier, -1, 25, 34, Constants.Timing.AGEGROUPS_LASTGROUP_FALSE, "Custom"));

            var cached = Assert.Single(Store.GetAgeGroups(e.Identifier));
            Assert.Equal(25, cached.StartAge);
            Assert.Equal("Custom", cached.CustomName);
        }

        [Fact]
        public void RemoveAgeGroup_RemovesFromCache()
        {
            var e = AddCurrentEvent();
            var g = new AgeGroup(e.Identifier, -1, 20, 29);
            Store.AddAgeGroup(g);

            Store.RemoveAgeGroup(g);

            Assert.Empty(Store.GetAgeGroups(e.Identifier));
            Assert.Empty(Db.GetAgeGroups(e.Identifier));
        }

        [Fact]
        public void RemoveAgeGroups_List_RemovesFromCache()
        {
            var e = AddCurrentEvent();
            var groups = Store.AddAgeGroups([new AgeGroup(e.Identifier, -1, 0, 29), new AgeGroup(e.Identifier, -1, 30, 99)]);

            Store.RemoveAgeGroups(groups);

            Assert.Empty(Store.GetAgeGroups(e.Identifier));
        }

        [Fact]
        public void RemoveAgeGroups_ByDistance_And_Reset()
        {
            var e = AddCurrentEvent();
            var d = AddStoreDistance(e.Identifier);
            Store.AddAgeGroups([new AgeGroup(e.Identifier, d.Identifier, 0, 99), new AgeGroup(e.Identifier, -1, 0, 99)]);

            Store.RemoveAgeGroups(e.Identifier, d.Identifier);
            Assert.Single(Store.GetAgeGroups(e.Identifier));

            Store.ResetAgeGroups(e.Identifier);
            Assert.Empty(Store.GetAgeGroups(e.Identifier));
            Assert.Empty(Db.GetAgeGroups(e.Identifier));
        }
    }
}
