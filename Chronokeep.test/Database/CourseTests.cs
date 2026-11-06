using Chronokeep.Objects;

namespace Chronokeep.Test.Database
{
    public class DistanceTests : DatabaseTestBase
    {
        [Fact]
        public void AddDistance_ThenGetDistance_RoundTripsFields()
        {
            var e = AddEvent();
            Distance d = new(0, "Half", e.Identifier, 13.1, 1, -2, 2, -1, 30, 3, 120, 500, 7200,
                Constants.Timing.DISTANCE_NO_LINKED_ID, 0, 4, true, true, "USATF-123");
            int id = Db.AddDistance(d);

            var stored = Db.GetDistance(id);
            Assert.NotNull(stored);
            Assert.Equal("Half", stored.Name);
            Assert.Equal(e.Identifier, stored.EventIdentifier);
            Assert.Equal(13.1, stored.DistanceValue, 3);
            Assert.Equal(1, stored.DistanceUnit);
            Assert.Equal(-2, stored.FinishLocation);
            Assert.Equal(2, stored.FinishOccurrence);
            Assert.Equal(-1, stored.StartLocation);
            Assert.Equal(30, stored.StartWithin);
            Assert.Equal(3, stored.Wave);
            Assert.Equal(120, stored.StartOffsetSeconds);
            Assert.Equal(500, stored.StartOffsetMilliseconds);
            Assert.Equal(7200, stored.EndSeconds);
            Assert.Equal(4, stored.Ranking);
            Assert.True(stored.SmsEnabled);
            Assert.True(stored.Upload);
            Assert.Equal("USATF-123", stored.Certification);
        }

        [Fact]
        public void AddDistance_DuplicateNameInEvent_ReturnsExistingId()
        {
            var e = AddEvent();
            var first = AddDistance(e.Identifier, "5K");
            int second = Db.AddDistance(new Distance("5K", e.Identifier));
            Assert.Equal(first.Identifier, second);
            Assert.Single(Db.GetDistances(e.Identifier));
        }

        [Fact]
        public void AddDistances_AssignsIdentifiers()
        {
            var e = AddEvent();
            var added = Db.AddDistances([new Distance("5K", e.Identifier), new Distance("10K", e.Identifier)]);
            Assert.Equal(2, added.Count);
            Assert.All(added, d => Assert.True(d.Identifier > 0));
            Assert.Equal(2, Db.GetDistances(e.Identifier).Count);
        }

        [Fact]
        public void GetDistances_FiltersByEvent()
        {
            var a = AddEvent("A");
            var b = AddEvent("B");
            AddDistance(a.Identifier, "5K");
            AddDistance(b.Identifier, "10K");
            Assert.Equal("5K", Assert.Single(Db.GetDistances(a.Identifier)).Name);
            Assert.Empty(Db.GetDistances(0));
            Assert.Equal(2, Db.GetDistances().Count);
        }

        [Fact]
        public void GetDistanceId_FindsByNameAndEvent()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier, "Marathon");
            Assert.Equal(d.Identifier, Db.GetDistanceId(new Distance("Marathon", e.Identifier)));
            Assert.Equal(-1, Db.GetDistanceId(new Distance("Missing", e.Identifier)));
        }

        [Fact]
        public void GetDistance_Missing_ReturnsNull()
        {
            Assert.Null(Db.GetDistance(999));
        }

        [Fact]
        public void UpdateDistance_ChangesFields()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            d.Name = "5 Kilometer";
            d.Wave = 2;
            d.SmsEnabled = true;
            Db.UpdateDistance(d);

            var stored = Db.GetDistance(d.Identifier)!;
            Assert.Equal("5 Kilometer", stored.Name);
            Assert.Equal(2, stored.Wave);
            Assert.True(stored.SmsEnabled);
        }

        [Fact]
        public void RemoveDistance_AlsoRemovesSegmentsEntriesAndAgeGroups()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            var keep = AddDistance(e.Identifier, "10K");
            AddParticipant(e.Identifier, d, "1");
            AddParticipant(e.Identifier, keep, "2", first: "Keep");
            Db.AddSegment(new Segment(e.Identifier, d.Identifier, Constants.Timing.LOCATION_FINISH, 1, 1, 1, 0, "Lap", "", ""));
            Db.AddAgeGroup(new AgeGroup(e.Identifier, d.Identifier, 0, 99));

            Db.RemoveDistance(d);

            Assert.Null(Db.GetDistance(d.Identifier));
            Assert.Equal(0L, Count("segments", $"distance_id={d.Identifier}"));
            Assert.Equal(0L, Count("age_groups", $"distance_id={d.Identifier}"));
            Assert.Equal("2", Assert.Single(Db.GetParticipants(e.Identifier)).Bib);
        }

        [Fact]
        public void SetWaveTimes_OnlyUpdatesMatchingWave()
        {
            var e = AddEvent();
            Db.AddDistance(new Distance("Wave1", e.Identifier) { Wave = 1 });
            Db.AddDistance(new Distance("Wave2", e.Identifier) { Wave = 2 });

            Db.SetWaveTimes(e.Identifier, 2, 300, 750);

            var distances = Db.GetDistances(e.Identifier).ToDictionary(d => d.Name);
            Assert.Equal(0, distances["Wave1"].StartOffsetSeconds);
            Assert.Equal(300, distances["Wave2"].StartOffsetSeconds);
            Assert.Equal(750, distances["Wave2"].StartOffsetMilliseconds);
        }
    }

    public class TimingLocationTests : DatabaseTestBase
    {
        [Fact]
        public void AddTimingLocation_ThenGet_RoundTrips()
        {
            var e = AddEvent();
            int id = Db.AddTimingLocation(new TimingLocation(-1, e.Identifier, "Turnaround", 2, 60));

            var loc = Assert.Single(Db.GetTimingLocations(e.Identifier));
            Assert.Equal(id, loc.Identifier);
            Assert.Equal("Turnaround", loc.Name);
            Assert.Equal(2, loc.MaxOccurrences);
            Assert.Equal(60, loc.IgnoreWithin);
        }

        [Fact]
        public void AddTimingLocations_AddsAll()
        {
            var e = AddEvent();
            Db.AddTimingLocations([new TimingLocation(e.Identifier, "A"), new TimingLocation(e.Identifier, "B")]);
            Assert.Equal(2, Db.GetTimingLocations(e.Identifier).Count);
        }

        [Fact]
        public void AddTimingLocation_DuplicateName_IsIgnored()
        {
            var e = AddEvent();
            Db.AddTimingLocation(new TimingLocation(e.Identifier, "A"));
            Db.AddTimingLocation(new TimingLocation(e.Identifier, "A"));
            Assert.Single(Db.GetTimingLocations(e.Identifier));
        }

        [Fact]
        public void UpdateTimingLocation_ChangesFields()
        {
            var e = AddEvent();
            int id = Db.AddTimingLocation(new TimingLocation(e.Identifier, "A"));
            Db.UpdateTimingLocation(new TimingLocation(id, e.Identifier, "Renamed", 3, 10));

            var loc = Assert.Single(Db.GetTimingLocations(e.Identifier));
            Assert.Equal("Renamed", loc.Name);
            Assert.Equal(3, loc.MaxOccurrences);
            Assert.Equal(10, loc.IgnoreWithin);
        }

        [Fact]
        public void RemoveTimingLocation_DeletesIt()
        {
            var e = AddEvent();
            int id = Db.AddTimingLocation(new TimingLocation(e.Identifier, "A"));
            int keep = Db.AddTimingLocation(new TimingLocation(e.Identifier, "B"));
            Db.RemoveTimingLocation(id);
            Assert.Equal(keep, Assert.Single(Db.GetTimingLocations(e.Identifier)).Identifier);
        }

        [Fact]
        public void GetTimingLocationId_FindsByEventAndName()
        {
            var e = AddEvent();
            var other = AddEvent("Other");
            Db.AddTimingLocation(new TimingLocation(other.Identifier, "A"));
            int id = Db.AddTimingLocation(new TimingLocation(e.Identifier, "A"));
            Assert.Equal(id, Db.GetTimingLocationId(new TimingLocation(e.Identifier, "A")));
            Assert.Equal(-1, Db.GetTimingLocationId(new TimingLocation(e.Identifier, "Missing")));
        }
    }

    public class SegmentTests : DatabaseTestBase
    {
        private static Segment NewSegment(int eventId, int distanceId, int occurrence, double cumulative = 1.0, string name = "Lap")
            => new(eventId, distanceId, Constants.Timing.LOCATION_FINISH, occurrence, 1.0, cumulative, 0, name, "45.0,-122.0", "https://map");

        [Fact]
        public void AddSegment_ThenGet_RoundTrips()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            int id = Db.AddSegment(NewSegment(e.Identifier, d.Identifier, 1, 2.5, "Mile 2.5"));

            var seg = Assert.Single(Db.GetSegments(e.Identifier));
            Assert.Equal(id, seg.Identifier);
            Assert.Equal(d.Identifier, seg.DistanceId);
            Assert.Equal(Constants.Timing.LOCATION_FINISH, seg.LocationId);
            Assert.Equal(1, seg.Occurrence);
            Assert.Equal(2.5, seg.CumulativeDistance, 3);
            Assert.Equal("Mile 2.5", seg.Name);
            Assert.Equal("45.0,-122.0", seg.Gps);
            Assert.Equal("https://map", seg.MapLink);
        }

        [Fact]
        public void AddSegments_AssignsIdentifiers()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            var added = Db.AddSegments([NewSegment(e.Identifier, d.Identifier, 1), NewSegment(e.Identifier, d.Identifier, 2)]);
            Assert.All(added, s => Assert.True(s.Identifier > 0));
            Assert.Equal(2, Db.GetSegments(e.Identifier).Count);
        }

        [Fact]
        public void UpdateSegment_ChangesFields()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            var seg = NewSegment(e.Identifier, d.Identifier, 1);
            seg.Identifier = Db.AddSegment(seg);
            seg.Name = "Renamed";
            seg.CumulativeDistance = 9.5;
            Db.UpdateSegment(seg);

            var stored = Assert.Single(Db.GetSegments(e.Identifier));
            Assert.Equal("Renamed", stored.Name);
            Assert.Equal(9.5, stored.CumulativeDistance, 3);
        }

        [Fact]
        public void UpdateSegments_ChangesAll()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            var segs = Db.AddSegments([NewSegment(e.Identifier, d.Identifier, 1), NewSegment(e.Identifier, d.Identifier, 2)]);
            segs.ForEach(s => s.Name = "Changed");
            Db.UpdateSegments(segs);
            Assert.All(Db.GetSegments(e.Identifier), s => Assert.Equal("Changed", s.Name));
        }

        [Fact]
        public void RemoveSegment_And_RemoveSegments_DeleteRows()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            var segs = Db.AddSegments([
                NewSegment(e.Identifier, d.Identifier, 1),
                NewSegment(e.Identifier, d.Identifier, 2),
                NewSegment(e.Identifier, d.Identifier, 3)]);

            Db.RemoveSegment(segs[0]);
            Assert.Equal(2, Db.GetSegments(e.Identifier).Count);

            Db.RemoveSegments([segs[1], segs[2]]);
            Assert.Empty(Db.GetSegments(e.Identifier));
        }

        [Fact]
        public void ResetSegments_OnlyClearsThatEvent()
        {
            var a = AddEvent("A");
            var b = AddEvent("B");
            var da = AddDistance(a.Identifier);
            var db = AddDistance(b.Identifier);
            Db.AddSegment(NewSegment(a.Identifier, da.Identifier, 1));
            Db.AddSegment(NewSegment(b.Identifier, db.Identifier, 1));

            Db.ResetSegments(a.Identifier);

            Assert.Empty(Db.GetSegments(a.Identifier));
            Assert.Single(Db.GetSegments(b.Identifier));
        }

        [Fact]
        public void GetMaxSegments_ReturnsLargestPerDistanceCount()
        {
            var e = AddEvent();
            var d1 = AddDistance(e.Identifier, "5K");
            var d2 = AddDistance(e.Identifier, "10K");
            Assert.Equal(0, Db.GetMaxSegments(e.Identifier));

            Db.AddSegments([
                NewSegment(e.Identifier, d1.Identifier, 1),
                NewSegment(e.Identifier, d2.Identifier, 1),
                NewSegment(e.Identifier, d2.Identifier, 2),
                NewSegment(e.Identifier, d2.Identifier, 3)]);

            Assert.Equal(3, Db.GetMaxSegments(e.Identifier));
        }

        [Fact]
        public void GetSegmentId_FindsByEventDistanceLocationOccurrence()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            Db.AddSegment(NewSegment(e.Identifier, d.Identifier, 1));
            int id = Db.AddSegment(NewSegment(e.Identifier, d.Identifier, 2));
            Assert.Equal(id, Db.GetSegmentId(NewSegment(e.Identifier, d.Identifier, 2)));
            Assert.Equal(-1, Db.GetSegmentId(NewSegment(e.Identifier, d.Identifier, 3)));
        }
    }
}
