using Chronokeep.Objects;

namespace Chronokeep.Test.MemStore
{
    public class MemStoreParticipantTests : MemStoreTestBase
    {
        [Fact]
        public void AddParticipant_CachesAndPersists()
        {
            var e = AddCurrentEvent();
            var d = AddStoreDistance(e.Identifier);
            var p = AddStoreParticipant(e.Identifier, d, "1");

            Assert.True(p.EventSpecific.Identifier > 0);
            Assert.Equal("5K", p.Distance);
            Assert.Same(p, Store.GetParticipantEventSpecific(e.Identifier, p.EventSpecific.Identifier));
            Assert.Single(Db.GetParticipants(e.Identifier));
        }

        [Fact]
        public void AddParticipant_AssignsCommonAgeGroup()
        {
            var e = AddCurrentEvent();
            var d = AddStoreDistance(e.Identifier);
            var g = new AgeGroup(e.Identifier, Constants.Timing.COMMON_AGEGROUPS_DISTANCEID, 30, 39);
            Store.AddAgeGroup(g);

            var p = AddStoreParticipant(e.Identifier, d, "1", birthday: "01/02/1990");

            Assert.Equal(g.GroupId, p.EventSpecific.AgeGroupId);
            Assert.Equal("30-39", p.EventSpecific.AgeGroupName);
            Assert.Equal(g.GroupId, Db.GetParticipantBib(e.Identifier, "1")!.EventSpecific.AgeGroupId);
        }

        [Fact]
        public void AddParticipant_AssignsDistanceAgeGroup_WhenNotCommon()
        {
            var e = AddCurrentEvent();
            e.CommonAgeGroups = false;
            Store.UpdateEvent(e);
            var d = AddStoreDistance(e.Identifier);
            var g = new AgeGroup(e.Identifier, d.Identifier, 30, 39);
            Store.AddAgeGroup(g);

            var p = AddStoreParticipant(e.Identifier, d, "1", birthday: "01/02/1990");

            Assert.Equal(g.GroupId, p.EventSpecific.AgeGroupId);
        }

        [Fact]
        public void AddParticipants_CachesEveryone()
        {
            var e = AddCurrentEvent();
            var d = AddStoreDistance(e.Identifier);
            Store.AddParticipants([
                NewParticipant(e.Identifier, d, "1", first: "A"),
                NewParticipant(e.Identifier, d, "2", first: "B")]);

            Assert.Equal(2, Store.GetParticipants(e.Identifier).Count);
            Assert.Equal(2, Db.GetParticipants(e.Identifier).Count);
        }

        [Fact]
        public void Lookups_FindCachedParticipants()
        {
            var e = AddCurrentEvent();
            var fiveK = AddStoreDistance(e.Identifier, "5K");
            var tenK = AddStoreDistance(e.Identifier, "10K");
            var p = AddStoreParticipant(e.Identifier, fiveK, "42", first: "Look");
            AddStoreParticipant(e.Identifier, tenK, "43", first: "Other");
            Store.AddBibChipAssociation(e.Identifier, [new BibChipAssociation { EventId = e.Identifier, Bib = "42", Chip = "C42" }]);

            Assert.Equal("Look", Store.GetParticipantBib(e.Identifier, "42")!.FirstName);
            Assert.Equal("Look", Store.GetParticipantChip(e.Identifier, "C42")!.FirstName);
            Assert.Equal("Look", Store.GetParticipant(e.Identifier, p.Identifier)!.FirstName);
            Assert.Equal("Look", Store.GetParticipant(e.Identifier, NewParticipant(e.Identifier, fiveK, "", first: "LOOK"))!.FirstName);
            Assert.Equal(p.Identifier, Store.GetParticipantId(NewParticipant(e.Identifier, fiveK, "", first: "Look")));
            Assert.Equal("Look", Assert.Single(Store.GetParticipants(e.Identifier, fiveK.Identifier)).FirstName);
            Assert.Null(Store.GetParticipantBib(e.Identifier, "999"));
            Assert.Empty(Store.GetParticipants(e.Identifier + 100));
        }

        [Fact]
        public void UpdateParticipant_UpdatesCacheAndDatabase()
        {
            var e = AddCurrentEvent();
            var d = AddStoreDistance(e.Identifier);
            var p = AddStoreParticipant(e.Identifier, d, "1");
            p.Status = Constants.Timing.EVENTSPECIFIC_FINISHED;

            Store.UpdateParticipant(p);

            Assert.Equal(Constants.Timing.EVENTSPECIFIC_FINISHED, Store.GetParticipantBib(e.Identifier, "1")!.Status);
            Assert.Equal(Constants.Timing.EVENTSPECIFIC_FINISHED, Db.GetParticipantBib(e.Identifier, "1")!.Status);
        }

        [Fact]
        public void UpdateParticipants_UpdatesEveryone()
        {
            var e = AddCurrentEvent();
            var d = AddStoreDistance(e.Identifier);
            AddStoreParticipant(e.Identifier, d, "1", first: "A");
            AddStoreParticipant(e.Identifier, d, "2", first: "B");
            var parts = Store.GetParticipants(e.Identifier);
            parts.ForEach(p => p.Status = Constants.Timing.EVENTSPECIFIC_DNF);

            Store.UpdateParticipants(parts);

            Assert.All(Db.GetParticipants(e.Identifier), p => Assert.Equal(Constants.Timing.EVENTSPECIFIC_DNF, p.Status));
        }

        [Fact]
        public void RemoveParticipantVariants_RemoveFromCache()
        {
            var e = AddCurrentEvent();
            var d = AddStoreDistance(e.Identifier);
            var a = AddStoreParticipant(e.Identifier, d, "1", first: "A");
            var b = AddStoreParticipant(e.Identifier, d, "2", first: "B");
            var c = AddStoreParticipant(e.Identifier, d, "3", first: "C");

            Store.RemoveParticipant(a.Identifier);
            Store.RemoveParticipantEntry(b);
            Assert.Equal("3", Assert.Single(Store.GetParticipants(e.Identifier)).Bib);

            Store.RemoveParticipantEntries([c]);
            Assert.Empty(Store.GetParticipants(e.Identifier));
            Assert.Empty(Db.GetParticipants(e.Identifier));
        }

        [Fact]
        public void GetDivisions_ReturnsDistinctDivisions()
        {
            var e = AddCurrentEvent();
            var d = AddStoreDistance(e.Identifier);
            Store.AddParticipants([
                NewParticipant(e.Identifier, d, "1", first: "A", division: "Elite"),
                NewParticipant(e.Identifier, d, "2", first: "B", division: "Elite"),
                NewParticipant(e.Identifier, d, "3", first: "C", division: "Open")]);
            Assert.Equal(["Elite", "Open"], Store.GetDivisions(e.Identifier).Order());
        }

        [Fact]
        public void GetDistanceParticipantsStatus_GroupsByStatus()
        {
            var e = AddCurrentEvent();
            var d = AddStoreDistance(e.Identifier);
            AddStoreParticipant(e.Identifier, d, "1", first: "A");
            var finished = AddStoreParticipant(e.Identifier, d, "2", first: "B");
            finished.Status = Constants.Timing.EVENTSPECIFIC_FINISHED;
            Store.UpdateParticipant(finished);

            var byStatus = Store.GetDistanceParticipantsStatus(e.Identifier, d.Identifier);

            Assert.Single(byStatus[Constants.Timing.EVENTSPECIFIC_FINISHED]);
            Assert.Single(byStatus[Constants.Timing.EVENTSPECIFIC_UNKNOWN]);
        }
    }

    public class MemStoreBibChipTests : MemStoreTestBase
    {
        [Fact]
        public void AddBibChipAssociation_TrimsCachesAndLabelsReads()
        {
            var e = AddCurrentEvent();
            var d = AddStoreDistance(e.Identifier);
            AddStoreParticipant(e.Identifier, d, "5", first: "Chip", last: "Owner");
            Store.AddChipRead(NewRead(e.Identifier, "C5"));

            Store.AddBibChipAssociation(e.Identifier, [new BibChipAssociation { EventId = e.Identifier, Bib = " 5 ", Chip = " C5 " }]);

            var assoc = Assert.Single(Store.GetBibChips(e.Identifier));
            Assert.Equal("5", assoc.Bib);
            Assert.Equal("C5", assoc.Chip);
            var read = Assert.Single(Store.GetChipReads(e.Identifier));
            Assert.Equal("5", read.Bib);
            Assert.Equal("Chip Owner", read.Name);
            Assert.Single(Db.GetBibChips(e.Identifier));
        }

        [Fact]
        public void IgnoredChips_UseEventIdNegativeOne()
        {
            AddCurrentEvent();
            Store.AddBibChipAssociation(-1, [new BibChipAssociation { EventId = -1, Bib = "", Chip = "IGNORE-ME" }]);
            Assert.Equal("IGNORE-ME", Assert.Single(Store.GetBibChips(-1)).Chip);

            Store.RemoveBibChipAssociation(-1, "IGNORE-ME");
            Assert.Empty(Store.GetBibChips(-1));
        }

        [Fact]
        public void RemoveBibChipAssociation_ClearsReadLabels()
        {
            var e = AddCurrentEvent();
            var d = AddStoreDistance(e.Identifier);
            AddStoreParticipant(e.Identifier, d, "5", first: "Chip");
            Store.AddBibChipAssociation(e.Identifier, [new BibChipAssociation { EventId = e.Identifier, Bib = "5", Chip = "C5" }]);
            Store.AddChipRead(NewRead(e.Identifier, "C5"));

            Store.RemoveBibChipAssociation(e.Identifier, "C5");

            Assert.Empty(Store.GetBibChips(e.Identifier));
            var read = Assert.Single(Store.GetChipReads(e.Identifier));
            Assert.Equal("", read.Bib);
            Assert.Equal("", read.Name);
        }

        [Fact]
        public void RemoveBibChipAssociation_ByChip_DoesNotLeaveStaleBibMapping()
        {
            var e = AddCurrentEvent();
            var d = AddStoreDistance(e.Identifier);
            Store.AddBibChipAssociation(e.Identifier, [new BibChipAssociation { EventId = e.Identifier, Bib = "5", Chip = "C5" }]);
            Store.AddChipRead(NewRead(e.Identifier, "C5"));
            Store.RemoveBibChipAssociation(e.Identifier, "C5");

            // Adding the bib's runner afterwards must not label the now-unassigned chip's read.
            AddStoreParticipant(e.Identifier, d, "5", first: "Late");

            Assert.Equal("", Assert.Single(Store.GetChipReads(e.Identifier)).Name);
        }

        [Fact]
        public void RemoveOneOfTwoChips_KeepsTheOtherChipLabelled()
        {
            var e = AddCurrentEvent();
            var d = AddStoreDistance(e.Identifier);
            var a = new BibChipAssociation { EventId = e.Identifier, Bib = "5", Chip = "A" };
            var b = new BibChipAssociation { EventId = e.Identifier, Bib = "5", Chip = "B" };
            Store.AddBibChipAssociation(e.Identifier, [a, b]);
            Store.AddChipReads([NewRead(e.Identifier, "A", 1), NewRead(e.Identifier, "B", 2)]);

            Store.RemoveBibChipAssociation(a);
            // A participant change re-labels reads using the bib-to-chip map.
            AddStoreParticipant(e.Identifier, d, "5", first: "Two", last: "Chips");

            var reads = Store.GetChipReads(e.Identifier).ToDictionary(r => r.ChipNumber);
            Assert.Equal("", reads["A"].Name);
            Assert.Equal("Two Chips", reads["B"].Name);
            Assert.Equal("B", Assert.Single(Store.GetBibChips(e.Identifier)).Chip);
        }

        [Fact]
        public void RemoveBibChipAssociations_RemovesAll()
        {
            var e = AddCurrentEvent();
            var items = new List<BibChipAssociation>
            {
                new() { EventId = e.Identifier, Bib = "1", Chip = "A" },
                new() { EventId = e.Identifier, Bib = "2", Chip = "B" },
                new() { EventId = e.Identifier, Bib = "3", Chip = "C" },
            };
            Store.AddBibChipAssociation(e.Identifier, items);

            Store.RemoveBibChipAssociation(items[0]);
            Store.RemoveBibChipAssociations([items[1], items[2]]);

            Assert.Empty(Store.GetBibChips(e.Identifier));
            Assert.Empty(Db.GetBibChips(e.Identifier));
        }
    }
}
