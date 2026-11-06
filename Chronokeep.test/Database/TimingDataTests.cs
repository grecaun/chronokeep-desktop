using Chronokeep.Database.SQLite;
using Chronokeep.Objects;

namespace Chronokeep.Test.Database
{
    public class ChipReadTests : DatabaseTestBase
    {
        [Fact]
        public void AddChipRead_ReturnsId_AndReadsBack()
        {
            var e = AddEvent(makeCurrent: true);
            int id = Db.AddChipRead(NewRead(e.Identifier, "1001", 65));

            var read = Assert.Single(Db.GetChipReads(e.Identifier));
            Assert.True(id > 0);
            Assert.Equal(id, read.ReadId);
            Assert.Equal("1001", read.ChipNumber);
            Assert.Equal(Constants.Timing.LOCATION_FINISH, read.LocationId);
            Assert.Equal(Constants.Timing.CHIPREAD_STATUS_NONE, read.Status);
            Assert.Equal("Start/Finish", read.LocationName);
        }

        [Fact]
        public void AddChipRead_Duplicate_IsIgnored()
        {
            var e = AddEvent(makeCurrent: true);
            Db.AddChipRead(NewRead(e.Identifier, "1001", 10));
            Db.AddChipRead(NewRead(e.Identifier, "1001", 10));
            Assert.Single(Db.GetChipReads(e.Identifier));
        }

        [Fact]
        public void AddChipReads_AssignsIdsToEachRead()
        {
            var e = AddEvent(makeCurrent: true);
            var reads = Db.AddChipReads([NewRead(e.Identifier, "1", 1), NewRead(e.Identifier, "2", 2), NewRead(e.Identifier, "3", 3)]);
            Assert.Equal(3, reads.Select(r => r.ReadId).Distinct().Count());
            Assert.Equal(3, Db.GetChipReads(e.Identifier).Count);
        }

        [Fact]
        public void GetChipReads_JoinsBibAndParticipantName()
        {
            var e = AddEvent(makeCurrent: true);
            var d = AddDistance(e.Identifier);
            AddParticipant(e.Identifier, d, "55", first: "Read", last: "Er");
            Db.AddBibChipAssociation(e.Identifier, [new BibChipAssociation { EventId = e.Identifier, Bib = "55", Chip = "C55" }]);
            Db.AddChipRead(NewRead(e.Identifier, "C55"));

            var read = Assert.Single(Db.GetChipReads(e.Identifier));
            Assert.Equal("55", read.Bib);
            Assert.Contains("Read", read.Name);
        }

        [Fact]
        public void GetChipReads_FiltersByEvent()
        {
            var a = AddEvent("A", makeCurrent: true);
            var b = AddEvent("B");
            Db.AddChipRead(NewRead(a.Identifier, "1"));
            Db.AddChipRead(NewRead(b.Identifier, "2"));
            Assert.Single(Db.GetChipReads(a.Identifier));
            Assert.Equal(2, Db.GetChipReads().Count);
            Assert.Single(Db.GetChipReadsSafemode(b.Identifier));
        }

        [Fact]
        public void UpdateChipRead_ChangesStatusAndTime()
        {
            var e = AddEvent(makeCurrent: true);
            Db.AddChipRead(NewRead(e.Identifier, "1"));
            var read = Db.GetChipReads(e.Identifier).Single();
            read.Status = Constants.Timing.CHIPREAD_STATUS_USED;
            read.TimeSeconds += 10;
            read.TimeMilliseconds = 123;
            Db.UpdateChipRead(read);

            var stored = Db.GetChipReads(e.Identifier).Single();
            Assert.Equal(Constants.Timing.CHIPREAD_STATUS_USED, stored.Status);
            Assert.Equal(read.TimeSeconds, stored.TimeSeconds);
            Assert.Equal(123, stored.TimeMilliseconds);
        }

        [Fact]
        public void SetChipReadStatuses_UpdatesEveryRead()
        {
            var e = AddEvent(makeCurrent: true);
            Db.AddChipReads([NewRead(e.Identifier, "1", 1), NewRead(e.Identifier, "2", 2)]);
            var reads = Db.GetChipReads(e.Identifier);
            reads.ForEach(r => r.Status = Constants.Timing.CHIPREAD_STATUS_IGNORE);
            Db.SetChipReadStatuses(reads);
            Assert.All(Db.GetChipReads(e.Identifier), r => Assert.Equal(Constants.Timing.CHIPREAD_STATUS_IGNORE, r.Status));
        }

        [Fact]
        public void DeleteChipReads_RemovesOnlyGivenReads()
        {
            var e = AddEvent(makeCurrent: true);
            Db.AddChipReads([NewRead(e.Identifier, "1", 1), NewRead(e.Identifier, "2", 2), NewRead(e.Identifier, "3", 3)]);
            var reads = Db.GetChipReads(e.Identifier).OrderBy(r => r.ChipNumber).ToList();
            Db.DeleteChipReads([reads[0], reads[1]]);
            Assert.Equal("3", Assert.Single(Db.GetChipReads(e.Identifier)).ChipNumber);
        }

        [Fact]
        public void StatusFilteredQueries_ReturnMatchingReads()
        {
            var e = AddEvent(makeCurrent: true);
            Db.AddChipReads([
                NewRead(e.Identifier, "none", 1),
                NewRead(e.Identifier, "ignored", 2),
                NewRead(e.Identifier, "dns", 3),
                NewRead(e.Identifier, "announcer", 4, Constants.Timing.LOCATION_ANNOUNCER),
                NewRead(e.Identifier, "announcer-used", 5, Constants.Timing.LOCATION_ANNOUNCER)]);
            var byChip = Db.GetChipReads(e.Identifier).ToDictionary(r => r.ChipNumber);
            byChip["ignored"].Status = Constants.Timing.CHIPREAD_STATUS_IGNORE;
            byChip["dns"].Status = Constants.Timing.CHIPREAD_STATUS_DNS;
            byChip["announcer-used"].Status = Constants.Timing.CHIPREAD_STATUS_ANNOUNCER_USED;
            Db.SetChipReadStatuses([byChip["ignored"], byChip["dns"], byChip["announcer-used"]]);

            Assert.Equal(["dns", "none"], Db.GetUsefulChipReads(e.Identifier).Select(r => r.ChipNumber).Order());
            Assert.Equal("announcer", Assert.Single(Db.GetAnnouncerChipReads(e.Identifier)).ChipNumber);
            Assert.Equal("announcer-used", Assert.Single(Db.GetAnnouncerUsedChipReads(e.Identifier)).ChipNumber);
            Assert.Equal("dns", Assert.Single(Db.GetDnsChipReads(e.Identifier)).ChipNumber);
        }

        [Fact]
        public void UnprocessedReadsExist_TracksStatusNone()
        {
            var e = AddEvent(makeCurrent: true);
            Assert.False(Db.UnprocessedReadsExist(e.Identifier));
            Db.AddChipRead(NewRead(e.Identifier, "1"));
            Assert.True(Db.UnprocessedReadsExist(e.Identifier));

            var read = Db.GetChipReads(e.Identifier).Single();
            read.Status = Constants.Timing.CHIPREAD_STATUS_USED;
            Db.SetChipReadStatus(read);
            Assert.False(Db.UnprocessedReadsExist(e.Identifier));
        }
    }

    public class TimeResultTests : DatabaseTestBase
    {
        private Event theEvent = null!;
        private Participant runner = null!;

        private void Arrange()
        {
            theEvent = AddEvent(makeCurrent: true);
            var d = AddDistance(theEvent.Identifier);
            runner = AddParticipant(theEvent.Identifier, d, "9", first: "Timed");
            // TimeResult construction relies on the cached current event.
            Results.GetStaticVariables(Db);
        }

        private TimeResult NewResult(int segment, int occurrence, long seconds, int status = Constants.Timing.TIMERESULT_STATUS_NONE, int? eventSpecificId = null)
            => new(theEvent.Identifier, Constants.Timing.TIMERESULT_DUMMYREAD, eventSpecificId ?? runner.EventSpecific.Identifier,
                Constants.Timing.LOCATION_FINISH, segment, occurrence, seconds, 0, "", seconds, 0,
                DateTime.Now, runner.Bib, status, "");

        [Fact]
        public void AddTimingResult_ThenGetTimingResults_RoundTrips()
        {
            Arrange();
            Db.AddTimingResult(NewResult(Constants.Timing.SEGMENT_FINISH, 1, 1800));

            var result = Assert.Single(Db.GetTimingResults(theEvent.Identifier));
            Assert.Equal(runner.EventSpecific.Identifier, result.EventSpecificId);
            Assert.Equal(Constants.Timing.SEGMENT_FINISH, result.SegmentId);
            Assert.Equal(Constants.Timing.ToTime(1800, 0), result.Time);
            Assert.Equal("9", result.Bib);
            Assert.Equal("Timed", result.First);
        }

        [Fact]
        public void AddTimingResult_SameKey_ReplacesRow()
        {
            Arrange();
            Db.AddTimingResult(NewResult(Constants.Timing.SEGMENT_FINISH, 1, 1800));
            Db.AddTimingResult(NewResult(Constants.Timing.SEGMENT_FINISH, 1, 1900));
            Assert.Equal(Constants.Timing.ToTime(1900, 0), Assert.Single(Db.GetTimingResults(theEvent.Identifier)).Time);
        }

        [Fact]
        public void SegmentQueries_FilterBySegment()
        {
            Arrange();
            Db.AddTimingResults([
                NewResult(Constants.Timing.SEGMENT_START, 0, 0),
                NewResult(Constants.Timing.SEGMENT_FINISH, 1, 1800),
                NewResult(42, 2, 900)]);

            Assert.Equal(3, Db.GetTimingResults(theEvent.Identifier).Count);
            Assert.Single(Db.GetStartTimes(theEvent.Identifier));
            Assert.Single(Db.GetFinishTimes(theEvent.Identifier));
            Assert.Equal(42, Assert.Single(Db.GetSegmentTimes(theEvent.Identifier, 42)).SegmentId);
        }

        [Fact]
        public void RemoveTimingResult_DeletesMatchingRow()
        {
            Arrange();
            var result = NewResult(Constants.Timing.SEGMENT_FINISH, 1, 1800);
            Db.AddTimingResult(result);
            Db.RemoveTimingResult(result);
            Assert.Empty(Db.GetTimingResults(theEvent.Identifier));
        }

        [Fact]
        public void Uploaded_TracksNonUploadedResults()
        {
            Arrange();
            Db.AddTimingResult(NewResult(Constants.Timing.SEGMENT_FINISH, 1, 1800));
            var pending = Db.GetNonUploadedResults(theEvent.Identifier);
            Assert.Single(pending);

            pending.ForEach(r => r.Uploaded = Constants.Timing.TIMERESULT_UPLOADED_TRUE);
            Db.SetUploadedTimingResults(pending);

            Assert.Empty(Db.GetNonUploadedResults(theEvent.Identifier));
        }

        [Fact]
        public void UnprocessedResultsExist_IgnoresStartAndProcessed()
        {
            Arrange();
            Db.AddTimingResult(NewResult(Constants.Timing.SEGMENT_START, 0, 0));
            Assert.False(Db.UnprocessedResultsExist(theEvent.Identifier));

            Db.AddTimingResult(NewResult(Constants.Timing.SEGMENT_FINISH, 1, 1800, Constants.Timing.TIMERESULT_STATUS_PROCESSED));
            Assert.False(Db.UnprocessedResultsExist(theEvent.Identifier));

            Db.AddTimingResult(NewResult(Constants.Timing.SEGMENT_FINISH, 2, 3600));
            Assert.True(Db.UnprocessedResultsExist(theEvent.Identifier));
        }

        [Fact]
        public void ResetTimingResultsPlacements_MarksResultsUnprocessed()
        {
            Arrange();
            Db.AddTimingResult(NewResult(Constants.Timing.SEGMENT_FINISH, 1, 1800, Constants.Timing.TIMERESULT_STATUS_PROCESSED));
            Db.ResetTimingResultsPlacements(theEvent.Identifier);
            Assert.True(Db.UnprocessedResultsExist(theEvent.Identifier));
        }

        [Fact]
        public void ResetTimingResultsEvent_ClearsResults_ResetsReadsAndStatuses()
        {
            Arrange();
            Db.AddTimingResult(NewResult(Constants.Timing.SEGMENT_FINISH, 1, 1800));
            Db.AddChipReads([NewRead(theEvent.Identifier, "used", 1), NewRead(theEvent.Identifier, "ignored", 2)]);
            var reads = Db.GetChipReads(theEvent.Identifier).ToDictionary(r => r.ChipNumber);
            reads["used"].Status = Constants.Timing.CHIPREAD_STATUS_USED;
            reads["ignored"].Status = Constants.Timing.CHIPREAD_STATUS_IGNORE;
            Db.SetChipReadStatuses([.. reads.Values]);
            var p = Db.GetParticipants(theEvent.Identifier).Single();
            p.Status = Constants.Timing.EVENTSPECIFIC_FINISHED;
            Db.UpdateParticipant(p);

            Db.ResetTimingResultsEvent(theEvent.Identifier);

            Assert.Empty(Db.GetTimingResults(theEvent.Identifier));
            var after = Db.GetChipReads(theEvent.Identifier).ToDictionary(r => r.ChipNumber);
            Assert.Equal(Constants.Timing.CHIPREAD_STATUS_NONE, after["used"].Status);
            Assert.Equal(Constants.Timing.CHIPREAD_STATUS_IGNORE, after["ignored"].Status);
            Assert.Equal(Constants.Timing.EVENTSPECIFIC_UNKNOWN, Db.GetParticipants(theEvent.Identifier).Single().Status);
        }
    }

    public class DistanceStatsTests : DatabaseTestBase
    {
        [Fact]
        public void GetDistanceStats_CountsDnsAndUnknownTogether()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier, "5K");
            AddParticipant(e.Identifier, d, "1", first: "A");
            AddParticipant(e.Identifier, d, "2", first: "B");
            var dns = Db.GetParticipantBib(e.Identifier, "1")!;
            dns.Status = Constants.Timing.EVENTSPECIFIC_DNS;
            Db.UpdateParticipant(dns);

            var stat = Assert.Single(Db.GetDistanceStats(e.Identifier));
            Assert.Equal("5K", stat.DistanceName);
            Assert.Equal(2, stat.Dns);
        }

        [Fact]
        public void GetDistanceStats_Condense_RollsLinkedDistanceIntoParent()
        {
            var e = AddEvent();
            var main = AddDistance(e.Identifier, "Marathon");
            var linked = new Distance("Marathon Early Start", e.Identifier) { LinkedDistance = main.Identifier };
            linked.Identifier = Db.AddDistance(linked);
            var other = AddDistance(e.Identifier, "5K");
            AddParticipant(e.Identifier, main, "1", first: "A");
            AddParticipant(e.Identifier, linked, "2", first: "B");
            AddParticipant(e.Identifier, other, "3", first: "C");

            var condensed = Db.GetDistanceStats(e.Identifier, condense: true).ToDictionary(s => s.DistanceName);
            Assert.Equal(["5K", "All", "Marathon"], condensed.Keys.Order());
            Assert.Equal(2, condensed["Marathon"].Dns);
            Assert.Equal(1, condensed["5K"].Dns);
            Assert.Equal(3, condensed["All"].Dns);

            var expanded = Db.GetDistanceStats(e.Identifier, condense: false).ToDictionary(s => s.DistanceName);
            Assert.Equal(["5K", "All", "Marathon", "Marathon Early Start"], expanded.Keys.Order());
            Assert.Equal(1, expanded["Marathon Early Start"].Dns);
        }

        [Fact]
        public void GetDistanceStats_CountsStatusesPerDistance()
        {
            var e = AddEvent();
            var fiveK = AddDistance(e.Identifier, "5K");
            var tenK = AddDistance(e.Identifier, "10K");
            AddParticipant(e.Identifier, fiveK, "1", first: "A");
            AddParticipant(e.Identifier, fiveK, "2", first: "B");
            AddParticipant(e.Identifier, tenK, "3", first: "C");
            var finisher = Db.GetParticipantBib(e.Identifier, "1")!;
            finisher.Status = Constants.Timing.EVENTSPECIFIC_FINISHED;
            Db.UpdateParticipant(finisher);

            var stats = Db.GetDistanceStats(e.Identifier).ToDictionary(s => s.DistanceName);

            Assert.Equal(1, stats["5K"].Finished);
            Assert.Equal(1, stats["5K"].Dns);
            Assert.Equal(1, stats["10K"].Dns);
            Assert.Equal(1, stats["All"].Finished);
            Assert.Equal(2, stats["All"].Dns);
        }
    }
}
