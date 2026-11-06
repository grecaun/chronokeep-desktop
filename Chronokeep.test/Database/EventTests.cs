using Chronokeep.Objects;

namespace Chronokeep.Test.Database
{
    public class EventTests : DatabaseTestBase
    {
        [Fact]
        public void AddEvent_ReturnsNewIdentifier()
        {
            var e = AddEvent();
            Assert.True(e.Identifier > 0);
        }

        [Fact]
        public void AddEvent_ThenGetEvent_RoundTripsFields()
        {
            Event e = new()
            {
                Name = "Round Trip",
                Date = DateString(2026, 7, 4),
                YearCode = "26",
                RankedBy = RankingType.Chip,
                CommonAgeGroups = false,
                CommonStartFinish = false,
                DistanceSpecificSegments = true,
                StartSeconds = 3600,
                StartMilliseconds = 250,
                FinishMaxOccurrences = 3,
                FinishIgnoreWithin = 30,
                StartWindow = 45,
                StartMaxOccurrences = 2,
                EventType = Constants.Timing.EVENT_TYPE_TIME,
                DisplayPlacements = false,
                UploadSpecific = true,
                UseMaleFemale = true,
            };
            int id = Db.AddEvent(e);

            var stored = Db.GetEvent(id);
            Assert.NotNull(stored);
            Assert.Equal("Round Trip", stored.Name);
            Assert.Equal(e.Date, stored.Date);
            Assert.Equal("26", stored.YearCode);
            Assert.Equal(RankingType.Chip, stored.RankedBy);
            Assert.False(stored.CommonAgeGroups);
            Assert.False(stored.CommonStartFinish);
            Assert.True(stored.DistanceSpecificSegments);
            Assert.Equal(3600, stored.StartSeconds);
            Assert.Equal(250, stored.StartMilliseconds);
            Assert.Equal(3, stored.FinishMaxOccurrences);
            Assert.Equal(30, stored.FinishIgnoreWithin);
            Assert.Equal(45, stored.StartWindow);
            Assert.Equal(2, stored.StartMaxOccurrences);
            Assert.Equal(Constants.Timing.EVENT_TYPE_TIME, stored.EventType);
            Assert.False(stored.DisplayPlacements);
            Assert.True(stored.UploadSpecific);
            Assert.True(stored.UseMaleFemale);
        }

        [Theory]
        [InlineData(RankingType.Clock)]
        [InlineData(RankingType.Chip)]
        [InlineData(RankingType.Mixed)]
        public void AddEvent_PreservesRankingType(RankingType ranking)
        {
            Event e = new() { Name = $"Ranked {ranking}", Date = DateString(), RankedBy = ranking };
            int id = Db.AddEvent(e);
            Assert.Equal(ranking, Db.GetEvent(id)!.RankedBy);
        }

        [Fact]
        public void AddEvent_DuplicateNameAndDate_IsIgnored()
        {
            AddEvent("Same");
            Db.AddEvent(new Event { Name = "Same", Date = DateString() });
            Assert.Single(Db.GetEvents());
        }

        [Fact]
        public void GetEvent_NegativeOrMissingId_ReturnsNull()
        {
            Assert.Null(Db.GetEvent(-1));
            Assert.Null(Db.GetEvent(12345));
        }

        [Fact]
        public void GetEventId_FindsByNameAndDate()
        {
            var e = AddEvent("Lookup");
            Assert.Equal(e.Identifier, Db.GetEventId(new Event { Name = "Lookup", Date = e.Date }));
            Assert.Equal(-1, Db.GetEventId(new Event { Name = "Nope", Date = e.Date }));
        }

        [Fact]
        public void GetEvents_ReturnsAllEvents()
        {
            AddEvent("One");
            AddEvent("Two");
            Assert.Equal(["One", "Two"], Db.GetEvents().Select(e => e.Name).Order());
        }

        [Fact]
        public void UpdateEvent_ChangesFields()
        {
            var e = AddEvent();
            e.Name = "Renamed";
            e.RankedBy = RankingType.Mixed;
            e.ApiEventId = "abc";
            e.ApiId = 7;
            Db.UpdateEvent(e);

            var stored = Db.GetEvent(e.Identifier)!;
            Assert.Equal("Renamed", stored.Name);
            Assert.Equal(RankingType.Mixed, stored.RankedBy);
            Assert.Equal("abc", stored.ApiEventId);
            Assert.Equal(7, stored.ApiId);
        }

        [Fact]
        public void SetStartOptions_OnlyUpdatesStartFields()
        {
            var e = AddEvent();
            e.StartWindow = 99;
            e.StartMaxOccurrences = 4;
            e.FinishMaxOccurrences = 8;
            Db.SetStartOptions(e);

            var stored = Db.GetEvent(e.Identifier)!;
            Assert.Equal(99, stored.StartWindow);
            Assert.Equal(4, stored.StartMaxOccurrences);
            Assert.Equal(1, stored.FinishMaxOccurrences);
        }

        [Fact]
        public void SetFinishOptions_OnlyUpdatesFinishFields()
        {
            var e = AddEvent();
            e.FinishMaxOccurrences = 5;
            e.FinishIgnoreWithin = 120;
            e.StartWindow = 77;
            Db.SetFinishOptions(e);

            var stored = Db.GetEvent(e.Identifier)!;
            Assert.Equal(5, stored.FinishMaxOccurrences);
            Assert.Equal(120, stored.FinishIgnoreWithin);
            Assert.NotEqual(77, stored.StartWindow);
        }

        [Fact]
        public void SetStartFinishOptions_UpdatesBoth()
        {
            var e = AddEvent();
            e.StartWindow = 11;
            e.StartMaxOccurrences = 2;
            e.FinishMaxOccurrences = 3;
            e.FinishIgnoreWithin = 44;
            Db.SetStartFinishOptions(e);

            var stored = Db.GetEvent(e.Identifier)!;
            Assert.Equal(11, stored.StartWindow);
            Assert.Equal(2, stored.StartMaxOccurrences);
            Assert.Equal(3, stored.FinishMaxOccurrences);
            Assert.Equal(44, stored.FinishIgnoreWithin);
        }

        [Fact]
        public void CurrentEvent_DefaultsToNull_ThenFollowsSetCurrentEvent()
        {
            Assert.Null(Db.GetCurrentEvent());
            var e = AddEvent();
            Db.SetCurrentEvent(e.Identifier);
            Assert.Equal(e.Identifier, Db.GetCurrentEvent()!.Identifier);
        }

        [Fact]
        public void RemoveEvent_CascadesToEventData()
        {
            var e = AddEvent();
            var other = AddEvent("Other");
            var d = AddDistance(e.Identifier);
            var otherDistance = AddDistance(other.Identifier);
            AddParticipant(e.Identifier, d, "1");
            AddParticipant(other.Identifier, otherDistance, "1");
            Db.AddTimingLocation(new TimingLocation(e.Identifier, "Mile 1"));
            Db.AddAgeGroup(new AgeGroup(e.Identifier, d.Identifier, 0, 99));
            Db.AddChipRead(NewRead(e.Identifier, "100"));
            Db.AddBibChipAssociation(e.Identifier, [new BibChipAssociation { EventId = e.Identifier, Bib = "1", Chip = "100" }]);

            Db.RemoveEvent(e.Identifier);

            Assert.Null(Db.GetEvent(e.Identifier));
            Assert.Equal(0L, Count("distances", $"event_id={e.Identifier}"));
            Assert.Equal(0L, Count("eventspecific", $"event_id={e.Identifier}"));
            Assert.Equal(0L, Count("timing_locations", $"event_id={e.Identifier}"));
            Assert.Equal(0L, Count("age_groups", $"event_id={e.Identifier}"));
            Assert.Equal(0L, Count("chipreads", $"event_id={e.Identifier}"));
            Assert.Equal(0L, Count("bib_chip_assoc", $"event_id={e.Identifier}"));
            // The other event is untouched.
            Assert.NotNull(Db.GetEvent(other.Identifier));
            Assert.Single(Db.GetParticipants(other.Identifier));
        }

        [Fact]
        public void RemoveEvent_ByObject_RemovesEvent()
        {
            var e = AddEvent();
            Db.RemoveEvent(e);
            Assert.Empty(Db.GetEvents());
        }
    }
}
