using Chronokeep.Objects;

namespace Chronokeep.Test.Database
{
    /// <summary>
    /// Chip read queries for a given event use that event, not whichever event is open. Saving and
    /// importing events read another event's reads, sometimes with no event open at all.
    /// </summary>
    public class ChipReadEventTests : DatabaseTestBase
    {
        [Fact]
        public void Reads_for_an_event_load_when_no_event_is_open()
        {
            Event e = AddEvent();
            Db.AddChipReads([NewRead(e.Identifier, "1001", 60)]);
            Assert.Null(Db.GetCurrentEvent());

            Assert.Equal("1001", Assert.Single(Db.GetChipReads(e.Identifier)).ChipNumber);
            Assert.Single(Db.GetUsefulChipReads(e.Identifier));
        }

        [Fact]
        public void Reads_use_their_own_events_locations_not_the_open_events()
        {
            Event open = AddEvent("Open", makeCurrent: true);
            Event other = AddEvent("Other");
            int mile = Db.AddTimingLocation(new TimingLocation(other.Identifier, "Mile 6"));
            Db.AddChipReads([NewRead(other.Identifier, "1001", 60, mile)]);

            ChipRead read = Assert.Single(Db.GetChipReads(other.Identifier));

            Assert.Equal("Mile 6", read.LocationName);
            Assert.Equal(open.Identifier, Db.GetCurrentEvent()!.Identifier);
        }

        [Fact]
        public void Reads_for_a_missing_event_are_empty()
        {
            AddEvent(makeCurrent: true);

            Assert.Empty(Db.GetChipReads(9999));
            Assert.Empty(Db.GetDnsChipReads(9999));
        }
    }
}
