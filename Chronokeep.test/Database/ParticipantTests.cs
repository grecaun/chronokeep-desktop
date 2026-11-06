using Chronokeep.Objects;

namespace Chronokeep.Test.Database
{
    public class ParticipantTests : DatabaseTestBase
    {
        [Fact]
        public void AddParticipant_AssignsIds_AndRoundTripsFields()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            var added = AddParticipant(e.Identifier, d, "101", division: "Elite");

            Assert.True(added.Identifier > 0);
            Assert.True(added.EventSpecific.Identifier > 0);

            var p = Db.GetParticipant(e.Identifier, added.Identifier);
            Assert.NotNull(p);
            Assert.Equal("Jane", p.FirstName);
            Assert.Equal("Runner", p.LastName);
            Assert.Equal("123 Main St", p.Street);
            Assert.Equal("Apt 1", p.Street2);
            Assert.Equal("Springfield", p.City);
            Assert.Equal("OR", p.State);
            Assert.Equal("97477", p.Zip);
            Assert.Equal("01/02/1990", p.Birthdate);
            Assert.Equal("jane@example.com", p.Email);
            Assert.Equal("555-0100", p.Phone);
            Assert.Equal("555-0101", p.Mobile);
            Assert.Equal("Parent", p.Parent);
            Assert.Equal("USA", p.Country);
            Assert.Equal("F", p.Gender);
            Assert.Equal("EC Name", p.EcName);
            Assert.Equal("555-0199", p.EcPhone);
            Assert.Equal("101", p.Bib);
            Assert.Equal(d.Name, p.Distance);
            Assert.Equal("comment", p.Comments);
            Assert.Equal("owes", p.Owes);
            Assert.Equal("other", p.Other);
            Assert.Equal("M", p.Apparel);
            Assert.Equal("Elite", p.Division);
            Assert.Equal(Constants.Timing.EVENTSPECIFIC_UNKNOWN, p.Status);
        }

        [Fact]
        public void AddParticipant_SamePersonInTwoEvents_SharesParticipantRow()
        {
            var a = AddEvent("A");
            var b = AddEvent("B");
            var da = AddDistance(a.Identifier);
            var db = AddDistance(b.Identifier);

            var first = AddParticipant(a.Identifier, da, "1");
            var second = AddParticipant(b.Identifier, db, "2");

            Assert.Equal(first.Identifier, second.Identifier);
            Assert.NotEqual(first.EventSpecific.Identifier, second.EventSpecific.Identifier);
            Assert.Equal(2, Db.GetParticipants().Count);
        }

        [Fact]
        public void AddParticipants_AddsEveryone()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            var added = Db.AddParticipants([
                NewParticipant(e.Identifier, d, "1", first: "A"),
                NewParticipant(e.Identifier, d, "2", first: "B"),
                NewParticipant(e.Identifier, d, "3", first: "C")]);

            Assert.Equal(3, added.Count);
            Assert.Equal(["1", "2", "3"], Db.GetParticipants(e.Identifier).Select(p => p.Bib).Order());
        }

        [Fact]
        public void GetParticipants_FiltersByEventAndDistance_SortedByName()
        {
            var e = AddEvent();
            var fiveK = AddDistance(e.Identifier, "5K");
            var tenK = AddDistance(e.Identifier, "10K");
            AddParticipant(e.Identifier, fiveK, "1", first: "Zed", last: "Adams");
            AddParticipant(e.Identifier, fiveK, "2", first: "Amy", last: "Baker");
            AddParticipant(e.Identifier, tenK, "3", first: "Bob", last: "Adams");

            Assert.Equal(["Bob", "Zed", "Amy"], Db.GetParticipants(e.Identifier).Select(p => p.FirstName));
            Assert.Equal(["Zed", "Amy"], Db.GetParticipants(e.Identifier, fiveK.Identifier).Select(p => p.FirstName));
        }

        [Fact]
        public void GetParticipantBib_FindsParticipant()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            AddParticipant(e.Identifier, d, "42", first: "Bib");
            Assert.Equal("Bib", Db.GetParticipantBib(e.Identifier, "42")!.FirstName);
            Assert.Null(Db.GetParticipantBib(e.Identifier, "43"));
        }

        [Fact]
        public void GetParticipantChip_UsesBibChipAssociation()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            AddParticipant(e.Identifier, d, "42", first: "Chip");
            Db.AddBibChipAssociation(e.Identifier, [new BibChipAssociation { EventId = e.Identifier, Bib = "42", Chip = "ABC123" }]);

            Assert.Equal("Chip", Db.GetParticipantChip(e.Identifier, "ABC123")!.FirstName);
            Assert.Null(Db.GetParticipantChip(e.Identifier, "NOPE"));
        }

        [Fact]
        public void GetParticipantEventSpecific_FindsByEntryId()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            var p = AddParticipant(e.Identifier, d, "7");
            Assert.Equal("7", Db.GetParticipantEventSpecific(e.Identifier, p.EventSpecific.Identifier)!.Bib);
        }

        [Fact]
        public void GetParticipant_ByUnknownDetails_MatchesOnPersonalFields()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            AddParticipant(e.Identifier, d, "7");
            var probe = NewParticipant(e.Identifier, d, "");
            Assert.Equal("7", Db.GetParticipant(e.Identifier, probe)!.Bib);
        }

        [Fact]
        public void GetParticipantId_FindsExistingPerson()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            var p = AddParticipant(e.Identifier, d, "7");
            Assert.Equal(p.Identifier, Db.GetParticipantId(NewParticipant(e.Identifier, d, "7")));
        }

        [Fact]
        public void UpdateParticipant_ChangesPersonAndEntry()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            var tenK = AddDistance(e.Identifier, "10K");
            var p = AddParticipant(e.Identifier, d, "7");

            var changed = new Participant(
                p.Identifier, "Janet", "Runner", "1 New St", "Eugene", "OR", "97401", "01/02/1990",
                new EventSpecific(p.EventSpecific.Identifier, e.Identifier, tenK.Identifier, tenK.Name, "8", 1,
                    "new comment", "", "", Constants.Timing.EVENTSPECIFIC_FINISHED, "30-39", 5, true, true, "L", "Masters", 2, 1),
                "janet@example.com", "", "", "", "USA", "", "F", "EC", "");
            Db.UpdateParticipant(changed);

            var stored = Db.GetParticipantEventSpecific(e.Identifier, p.EventSpecific.Identifier)!;
            Assert.Equal("Janet", stored.FirstName);
            Assert.Equal("Eugene", stored.City);
            Assert.Equal("8", stored.Bib);
            Assert.Equal(tenK.Identifier, stored.EventSpecific.DistanceIdentifier);
            Assert.True(stored.IsCheckedIn);
            Assert.True(stored.Anonymous);
            Assert.Equal("Masters", stored.Division);
            Assert.Equal("30-39", stored.EventSpecific.AgeGroupName);
            Assert.Equal(5, stored.EventSpecific.AgeGroupId);
            Assert.Equal(2, stored.EventSpecific.Version);
            Assert.Equal(1, stored.EventSpecific.UploadedVersion);
        }

        [Fact]
        public void UpdateParticipants_UpdatesStatusForAll()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            AddParticipant(e.Identifier, d, "1", first: "A");
            AddParticipant(e.Identifier, d, "2", first: "B");
            var parts = Db.GetParticipants(e.Identifier);
            parts.ForEach(p => p.Status = Constants.Timing.EVENTSPECIFIC_FINISHED);

            Db.UpdateParticipants(parts);

            Assert.All(Db.GetParticipants(e.Identifier), p => Assert.Equal(Constants.Timing.EVENTSPECIFIC_FINISHED, p.Status));
        }

        [Fact]
        public void RemoveParticipant_DeletesPersonAndEntries()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            var p = AddParticipant(e.Identifier, d, "1");
            Db.RemoveParticipant(p.Identifier);
            Assert.Empty(Db.GetParticipants(e.Identifier));
            Assert.Equal(0L, Count("participants", $"participant_id={p.Identifier}"));
        }

        [Fact]
        public void RemoveParticipantEntry_KeepsPersonRow()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            var p = AddParticipant(e.Identifier, d, "1");
            Db.RemoveParticipantEntry(p);
            Assert.Empty(Db.GetParticipants(e.Identifier));
            Assert.Equal(1L, Count("participants", $"participant_id={p.Identifier}"));
        }

        [Fact]
        public void RemoveParticipantEntries_RemovesAllGiven()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            var a = AddParticipant(e.Identifier, d, "1", first: "A");
            var b = AddParticipant(e.Identifier, d, "2", first: "B");
            Db.RemoveParticipantEntries([a, b]);
            Assert.Empty(Db.GetParticipants(e.Identifier));
        }

        [Fact]
        public void GetDivisions_ReturnsDivisionPerEntry()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            AddParticipant(e.Identifier, d, "1", first: "A", division: "Elite");
            AddParticipant(e.Identifier, d, "2", first: "B", division: "Open");
            Assert.Equal(["Elite", "Open"], Db.GetDivisions(e.Identifier).Order());
        }

        [Fact]
        public void GetDistanceParticipantsStatus_GroupsByStatus()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            AddParticipant(e.Identifier, d, "1", first: "A");
            AddParticipant(e.Identifier, d, "2", first: "B");
            var finished = Db.GetParticipants(e.Identifier).First();
            finished.Status = Constants.Timing.EVENTSPECIFIC_FINISHED;
            Db.UpdateParticipant(finished);

            var byStatus = Db.GetDistanceParticipantsStatus(e.Identifier, -1);

            Assert.Single(byStatus[Constants.Timing.EVENTSPECIFIC_FINISHED]);
            Assert.Single(byStatus[Constants.Timing.EVENTSPECIFIC_UNKNOWN]);
        }
    }

    public class BibChipTests : DatabaseTestBase
    {
        [Fact]
        public void AddBibChipAssociation_StoresEveryItem()
        {
            var e = AddEvent();
            Db.AddBibChipAssociation(e.Identifier, [
                new BibChipAssociation { EventId = e.Identifier, Bib = "1", Chip = "A" },
                new BibChipAssociation { EventId = e.Identifier, Bib = "2", Chip = "B" },
                new BibChipAssociation { EventId = e.Identifier, Bib = "3", Chip = "C" }]);

            var stored = Db.GetBibChips(e.Identifier).OrderBy(b => b.Bib).ToList();
            Assert.Equal(["1", "2", "3"], stored.Select(b => b.Bib));
            Assert.Equal(["A", "B", "C"], stored.Select(b => b.Chip));
        }

        [Fact]
        public void AddBibChipAssociation_SameChip_ReplacesBib()
        {
            var e = AddEvent();
            Db.AddBibChipAssociation(e.Identifier, [new BibChipAssociation { EventId = e.Identifier, Bib = "1", Chip = "A" }]);
            Db.AddBibChipAssociation(e.Identifier, [new BibChipAssociation { EventId = e.Identifier, Bib = "2", Chip = "A" }]);
            Assert.Equal("2", Assert.Single(Db.GetBibChips(e.Identifier)).Bib);
        }

        [Fact]
        public void GetBibChips_FiltersByEvent()
        {
            var a = AddEvent("A");
            var b = AddEvent("B");
            Db.AddBibChipAssociation(a.Identifier, [new BibChipAssociation { EventId = a.Identifier, Bib = "1", Chip = "A" }]);
            Db.AddBibChipAssociation(b.Identifier, [new BibChipAssociation { EventId = b.Identifier, Bib = "1", Chip = "A" }]);
            Assert.Single(Db.GetBibChips(a.Identifier));
            Assert.Equal(2, Db.GetBibChips().Count);
        }

        [Fact]
        public void RemoveBibChipAssociation_ByChip_And_ByObject()
        {
            var e = AddEvent();
            var items = new List<BibChipAssociation>
            {
                new() { EventId = e.Identifier, Bib = "1", Chip = "A" },
                new() { EventId = e.Identifier, Bib = "2", Chip = "B" },
                new() { EventId = e.Identifier, Bib = "3", Chip = "C" },
                new() { EventId = e.Identifier, Bib = "4", Chip = "D" },
            };
            Db.AddBibChipAssociation(e.Identifier, items);

            Db.RemoveBibChipAssociation(e.Identifier, "A");
            Db.RemoveBibChipAssociation(items[1]);
            Assert.Equal(["C", "D"], Db.GetBibChips(e.Identifier).Select(b => b.Chip).Order());

            Db.RemoveBibChipAssociations([items[2], items[3]]);
            Assert.Empty(Db.GetBibChips(e.Identifier));
        }
    }
}
