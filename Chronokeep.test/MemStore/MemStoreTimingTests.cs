using Chronokeep.Objects;
using Chronokeep.Objects.ChronoKeepAPI;
using Chronokeep.Objects.ChronokeepRemote;

namespace Chronokeep.Test.MemStore
{
    public class MemStoreChipReadTests : MemStoreTestBase
    {
        [Fact]
        public void AddChipRead_CachesWithLocationAndName()
        {
            var e = AddCurrentEvent();
            var d = AddStoreDistance(e.Identifier);
            AddStoreParticipant(e.Identifier, d, "7", first: "Fast", last: "Runner");
            Store.AddBibChipAssociation(e.Identifier, [new BibChipAssociation { EventId = e.Identifier, Bib = "7", Chip = "C7" }]);

            int id = Store.AddChipRead(NewRead(e.Identifier, "C7"));

            var read = Assert.Single(Store.GetChipReads(e.Identifier));
            Assert.Equal(id, read.ReadId);
            Assert.Equal("7", read.Bib);
            Assert.Equal("Fast Runner", read.Name);
            Assert.Equal("Start/Finish", read.LocationName);
            Assert.Single(Db.GetChipReads(e.Identifier));
        }

        [Fact]
        public void AddChipReads_CachesEachRead()
        {
            var e = AddCurrentEvent();
            Store.AddChipReads([NewRead(e.Identifier, "1", 1), NewRead(e.Identifier, "2", 2)]);
            Assert.Equal(2, Store.GetChipReads(e.Identifier).Count);
        }

        [Fact]
        public void UpdateAndStatusChanges_ApplyToCachedRead()
        {
            var e = AddCurrentEvent();
            Store.AddChipReads([NewRead(e.Identifier, "1", 1), NewRead(e.Identifier, "2", 2)]);
            var reads = Store.GetChipReads(e.Identifier).OrderBy(r => r.ChipNumber).ToList();

            var update = reads[0];
            update.Status = Constants.Timing.CHIPREAD_STATUS_USED;
            update.TimeMilliseconds = 321;
            Store.UpdateChipRead(update);
            reads[1].Status = Constants.Timing.CHIPREAD_STATUS_IGNORE;
            Store.SetChipReadStatus(reads[1]);

            var byChip = Store.GetChipReads(e.Identifier).ToDictionary(r => r.ChipNumber);
            Assert.Equal(Constants.Timing.CHIPREAD_STATUS_USED, byChip["1"].Status);
            Assert.Equal(321, byChip["1"].TimeMilliseconds);
            Assert.Equal(Constants.Timing.CHIPREAD_STATUS_IGNORE, byChip["2"].Status);
            Assert.Equal(Constants.Timing.CHIPREAD_STATUS_IGNORE,
                Db.GetChipReads(e.Identifier).Single(r => r.ChipNumber == "2").Status);
        }

        [Fact]
        public void StatusFilteredQueries_ReturnMatchingReads()
        {
            var e = AddCurrentEvent();
            Store.AddChipReads([
                NewRead(e.Identifier, "none", 1),
                NewRead(e.Identifier, "ignored", 2),
                NewRead(e.Identifier, "dns", 3),
                NewRead(e.Identifier, "announcer", 4, Constants.Timing.LOCATION_ANNOUNCER),
                NewRead(e.Identifier, "announcer-used", 5, Constants.Timing.LOCATION_ANNOUNCER)]);
            var byChip = Store.GetChipReads(e.Identifier).ToDictionary(r => r.ChipNumber);
            byChip["ignored"].Status = Constants.Timing.CHIPREAD_STATUS_IGNORE;
            byChip["dns"].Status = Constants.Timing.CHIPREAD_STATUS_DNS;
            byChip["announcer-used"].Status = Constants.Timing.CHIPREAD_STATUS_ANNOUNCER_USED;
            Store.SetChipReadStatuses([byChip["ignored"], byChip["dns"], byChip["announcer-used"]]);

            Assert.Equal(["dns", "none"], Store.GetUsefulChipReads(e.Identifier).Select(r => r.ChipNumber).Order());
            Assert.Equal("announcer", Assert.Single(Store.GetAnnouncerChipReads(e.Identifier)).ChipNumber);
            Assert.Equal("announcer-used", Assert.Single(Store.GetAnnouncerUsedChipReads(e.Identifier)).ChipNumber);
            Assert.Equal("dns", Assert.Single(Store.GetDnsChipReads(e.Identifier)).ChipNumber);
        }

        [Fact]
        public void DeleteChipReads_RemovesFromCache()
        {
            var e = AddCurrentEvent();
            Store.AddChipReads([NewRead(e.Identifier, "1", 1), NewRead(e.Identifier, "2", 2)]);
            var doomed = Store.GetChipReads(e.Identifier).Single(r => r.ChipNumber == "1");
            Store.DeleteChipReads([doomed]);
            Assert.Equal("2", Assert.Single(Store.GetChipReads(e.Identifier)).ChipNumber);
            Assert.Single(Db.GetChipReads(e.Identifier));
        }

        [Fact]
        public void UnprocessedReadsExist_TracksStatusNone()
        {
            var e = AddCurrentEvent();
            Assert.False(Store.UnprocessedReadsExist(e.Identifier));
            Store.AddChipRead(NewRead(e.Identifier, "1"));
            Assert.True(Store.UnprocessedReadsExist(e.Identifier));
            var read = Store.GetChipReads(e.Identifier).Single();
            read.Status = Constants.Timing.CHIPREAD_STATUS_USED;
            Store.SetChipReadStatus(read);
            Assert.False(Store.UnprocessedReadsExist(e.Identifier));
        }
    }

    public class MemStoreTimeResultTests : MemStoreTestBase
    {
        private Event theEvent = null!;
        private Participant runner = null!;

        private void Arrange()
        {
            theEvent = AddCurrentEvent();
            var d = AddStoreDistance(theEvent.Identifier);
            runner = AddStoreParticipant(theEvent.Identifier, d, "9", first: "Timed");
        }

        private TimeResult NewResult(int segment, int occurrence, long seconds, int status = Constants.Timing.TIMERESULT_STATUS_NONE)
            => new(theEvent.Identifier, Constants.Timing.TIMERESULT_DUMMYREAD, runner.EventSpecific.Identifier,
                Constants.Timing.LOCATION_FINISH, segment, occurrence, seconds, 0, "", seconds, 0,
                DateTime.Now, runner.Bib, status, "");

        [Fact]
        public void AddTimingResult_CachesWithParticipantDetails()
        {
            Arrange();
            Store.AddTimingResult(NewResult(Constants.Timing.SEGMENT_FINISH, 1, 1800));

            var result = Assert.Single(Store.GetTimingResults(theEvent.Identifier));
            Assert.Equal("Timed", result.First);
            Assert.Equal("9", result.Bib);
            Assert.Equal("5K", result.DistanceName);
            Assert.Equal(1L, Count("time_results", $"event_id={theEvent.Identifier}"));
        }

        [Fact]
        public void AddTimingResult_SameKey_ReplacesCachedResult()
        {
            Arrange();
            Store.AddTimingResult(NewResult(Constants.Timing.SEGMENT_FINISH, 1, 1800));
            Store.AddTimingResult(NewResult(Constants.Timing.SEGMENT_FINISH, 1, 1900));
            Assert.Equal(Constants.Timing.ToTime(1900, 0), Assert.Single(Store.GetTimingResults(theEvent.Identifier)).Time);
        }

        [Fact]
        public void SegmentQueries_FilterBySegment()
        {
            Arrange();
            Store.AddTimingResults([
                NewResult(Constants.Timing.SEGMENT_START, 0, 0),
                NewResult(Constants.Timing.SEGMENT_FINISH, 1, 1800),
                NewResult(42, 2, 900)]);

            Assert.Single(Store.GetStartTimes(theEvent.Identifier));
            Assert.Single(Store.GetFinishTimes(theEvent.Identifier));
            Assert.Single(Store.GetSegmentTimes(theEvent.Identifier, 42));
        }

        [Fact]
        public void GetLastSeenResults_ReturnsLatestPerParticipant()
        {
            Arrange();
            Store.AddTimingResults([NewResult(42, 2, 900), NewResult(Constants.Timing.SEGMENT_FINISH, 1, 1800)]);
            Assert.Equal(1800, Assert.Single(Store.GetLastSeenResults(theEvent.Identifier)).Seconds);
        }

        [Fact]
        public void RemoveTimingResult_RemovesFromCache()
        {
            Arrange();
            var result = NewResult(Constants.Timing.SEGMENT_FINISH, 1, 1800);
            Store.AddTimingResult(result);
            Store.RemoveTimingResult(result);
            Assert.Empty(Store.GetTimingResults(theEvent.Identifier));
            Assert.Empty(Db.GetTimingResults(theEvent.Identifier));
        }

        [Fact]
        public void SetUploadedTimingResults_RemovesFromNonUploaded()
        {
            Arrange();
            Store.AddTimingResult(NewResult(Constants.Timing.SEGMENT_FINISH, 1, 1800));
            var pending = Store.GetNonUploadedResults(theEvent.Identifier);
            Assert.Single(pending);

            pending.ForEach(r => r.Uploaded = Constants.Timing.TIMERESULT_UPLOADED_TRUE);
            Store.SetUploadedTimingResults(pending);

            Assert.Empty(Store.GetNonUploadedResults(theEvent.Identifier));
            Assert.Empty(Db.GetNonUploadedResults(theEvent.Identifier));
        }

        [Fact]
        public void UnprocessedResultsExist_MatchesDatabaseRules()
        {
            Arrange();
            Store.AddTimingResult(NewResult(Constants.Timing.SEGMENT_START, 0, 0));
            Assert.Equal(Db.UnprocessedResultsExist(theEvent.Identifier), Store.UnprocessedResultsExist(theEvent.Identifier));

            Store.AddTimingResult(NewResult(Constants.Timing.SEGMENT_FINISH, 1, 1800));
            Assert.True(Store.UnprocessedResultsExist(theEvent.Identifier));
            Assert.True(Db.UnprocessedResultsExist(theEvent.Identifier));
        }

        [Fact]
        public void ResetTimingResultsPlacements_MarksResultsUnprocessed()
        {
            Arrange();
            Store.AddTimingResult(NewResult(Constants.Timing.SEGMENT_FINISH, 1, 1800, Constants.Timing.TIMERESULT_STATUS_PROCESSED));
            Assert.False(Store.UnprocessedResultsExist(theEvent.Identifier));

            Store.ResetTimingResultsPlacements(theEvent.Identifier);

            Assert.True(Store.UnprocessedResultsExist(theEvent.Identifier));
            Assert.Equal(-1, Store.GetTimingResults(theEvent.Identifier).Single().Place);
        }

        [Fact]
        public void ResetTimingResultsEvent_ClearsResultsAndResetsStatuses()
        {
            Arrange();
            Store.AddTimingResult(NewResult(Constants.Timing.SEGMENT_FINISH, 1, 1800));
            Store.AddChipReads([NewRead(theEvent.Identifier, "used", 1), NewRead(theEvent.Identifier, "ignored", 2)]);
            var reads = Store.GetChipReads(theEvent.Identifier).ToDictionary(r => r.ChipNumber);
            reads["used"].Status = Constants.Timing.CHIPREAD_STATUS_USED;
            reads["ignored"].Status = Constants.Timing.CHIPREAD_STATUS_IGNORE;
            Store.SetChipReadStatuses([.. reads.Values]);
            runner.Status = Constants.Timing.EVENTSPECIFIC_FINISHED;
            Store.UpdateParticipant(runner);

            Store.ResetTimingResultsEvent(theEvent.Identifier);

            Assert.Empty(Store.GetTimingResults(theEvent.Identifier));
            var after = Store.GetChipReads(theEvent.Identifier).ToDictionary(r => r.ChipNumber);
            Assert.Equal(Constants.Timing.CHIPREAD_STATUS_NONE, after["used"].Status);
            Assert.Equal(Constants.Timing.CHIPREAD_STATUS_IGNORE, after["ignored"].Status);
            Assert.Equal(Constants.Timing.EVENTSPECIFIC_UNKNOWN, Store.GetParticipants(theEvent.Identifier).Single().Status);
        }
    }

    public class MemStoreDistanceStatsTests : MemStoreTestBase
    {
        [Fact]
        public void GetDistanceStats_CountsPerDistanceWithAllRow()
        {
            var e = AddCurrentEvent();
            var fiveK = AddStoreDistance(e.Identifier, "5K");
            var tenK = AddStoreDistance(e.Identifier, "10K");
            var a = AddStoreParticipant(e.Identifier, fiveK, "1", first: "A");
            AddStoreParticipant(e.Identifier, fiveK, "2", first: "B");
            var c = AddStoreParticipant(e.Identifier, tenK, "3", first: "C");
            a.Status = Constants.Timing.EVENTSPECIFIC_FINISHED;
            c.Status = Constants.Timing.EVENTSPECIFIC_STARTED;
            Store.UpdateParticipants([a, c]);

            var stats = Store.GetDistanceStats(e.Identifier);

            Assert.Equal("All", stats[0].DistanceName);
            var byName = stats.ToDictionary(s => s.DistanceName);
            Assert.Equal(1, byName["5K"].Finished);
            Assert.Equal(1, byName["5K"].Dns);
            Assert.Equal(1, byName["10K"].Active);
            Assert.Equal(1, byName["All"].Finished);
            Assert.Equal(1, byName["All"].Active);
            Assert.Equal(1, byName["All"].Dns);
        }

        [Fact]
        public void GetDistanceStats_Condense_RollsUpLinkedDistances()
        {
            var e = AddCurrentEvent();
            var main = AddStoreDistance(e.Identifier, "Marathon");
            var early = AddStoreDistance(e.Identifier, "Marathon Early", main.Identifier);
            AddStoreParticipant(e.Identifier, main, "1", first: "A");
            AddStoreParticipant(e.Identifier, early, "2", first: "B");

            var condensed = Store.GetDistanceStats(e.Identifier, condense: true);
            Assert.Equal(2, Assert.Single(condensed).Dns);

            var expanded = Store.GetDistanceStats(e.Identifier, condense: false);
            Assert.Equal(3, expanded.Count);
        }

        [Fact]
        public void GetDistanceStats_MatchesDatabaseImplementation()
        {
            var e = AddCurrentEvent();
            var main = AddStoreDistance(e.Identifier, "Marathon");
            var early = AddStoreDistance(e.Identifier, "Marathon Early", main.Identifier);
            var fiveK = AddStoreDistance(e.Identifier, "5K");
            var a = AddStoreParticipant(e.Identifier, main, "1", first: "A");
            AddStoreParticipant(e.Identifier, early, "2", first: "B");
            AddStoreParticipant(e.Identifier, fiveK, "3", first: "C");
            a.Status = Constants.Timing.EVENTSPECIFIC_FINISHED;
            Store.UpdateParticipant(a);

            foreach (bool condense in new[] { true, false })
            {
                var fromStore = Store.GetDistanceStats(e.Identifier, condense)
                    .Select(s => (s.DistanceName, s.Active, s.Finished, s.Dns, s.Dnf)).OrderBy(s => s.DistanceName);
                var fromDb = Db.GetDistanceStats(e.Identifier, condense)
                    .Select(s => (s.DistanceName, s.Active, s.Finished, s.Dns, s.Dnf)).OrderBy(s => s.DistanceName);
                Assert.Equal(fromDb, fromStore);
            }
        }
    }

    public class MemStoreAlarmTests : MemStoreTestBase
    {
        [Fact]
        public void SaveAlarm_CachesAndPersists()
        {
            var e = AddCurrentEvent();
            int id = Store.SaveAlarm(e.Identifier, new Alarm(-1, "1", "", true, 2));
            Assert.Equal(id, Assert.Single(Store.GetAlarms(e.Identifier)).Identifier);
            Assert.Single(Db.GetAlarms(e.Identifier));
        }

        [Fact]
        public void SaveAlarm_SameBibTwice_KeepsOneAlarm()
        {
            var e = AddCurrentEvent();
            Store.SaveAlarm(e.Identifier, new Alarm(-1, "1", "", true, 1));
            Store.SaveAlarm(e.Identifier, new Alarm(-1, "1", "", true, 2));

            var alarm = Assert.Single(Store.GetAlarms(e.Identifier));
            Assert.Equal(2, alarm.AlarmSound);
            Assert.Single(Db.GetAlarms(e.Identifier));
        }

        [Fact]
        public void SaveAlarms_ResavingSameList_DoesNotDuplicate()
        {
            // Alarm.SaveAlarms re-saves the whole alarm list each time.
            var e = AddCurrentEvent();
            Store.SaveAlarms(e.Identifier, [new Alarm(-1, "1", "", true, 1), new Alarm(-1, "2", "", true, 1)]);
            Store.SaveAlarms(e.Identifier, Store.GetAlarms(e.Identifier));
            Store.SaveAlarms(e.Identifier, Store.GetAlarms(e.Identifier));

            Assert.Equal(["1", "2"], Store.GetAlarms(e.Identifier).Select(a => a.Bib).Order());
            Assert.Equal(2, Db.GetAlarms(e.Identifier).Count);
        }

        [Fact]
        public void SaveAlarms_DeleteAlarm_DeleteAlarms()
        {
            var e = AddCurrentEvent();
            var saved = Store.SaveAlarms(e.Identifier, [new Alarm(-1, "1", "", true, 1), new Alarm(-1, "2", "", true, 1), new Alarm(-1, "3", "", true, 1)]);
            Assert.Equal(3, Store.GetAlarms(e.Identifier).Count);

            Store.DeleteAlarm(saved[0]);
            Assert.Equal(2, Store.GetAlarms(e.Identifier).Count);

            Store.DeleteAlarms(e.Identifier);
            Assert.Empty(Store.GetAlarms(e.Identifier));
            Assert.Empty(Db.GetAlarms(e.Identifier));
        }
    }

    public class MemStoreRemoteReaderTests : MemStoreTestBase
    {
        [Fact]
        public void AddRemoteReaders_ReplacesSameApiAndName()
        {
            var e = AddCurrentEvent();
            Store.AddRemoteReaders(e.Identifier, [new RemoteReader("Reader", 1, Constants.Timing.LOCATION_FINISH, e.Identifier)]);
            Store.AddRemoteReaders(e.Identifier, [new RemoteReader("READER", 1, Constants.Timing.LOCATION_START, e.Identifier)]);

            var reader = Assert.Single(Store.GetRemoteReaders(e.Identifier));
            Assert.Equal(Constants.Timing.LOCATION_START, reader.LocationId);
        }

        [Fact]
        public void DeleteRemoteReader_And_DeleteRemoteReaders()
        {
            var e = AddCurrentEvent();
            var readers = new List<RemoteReader>
            {
                new("A", 1, Constants.Timing.LOCATION_FINISH, e.Identifier),
                new("B", 1, Constants.Timing.LOCATION_FINISH, e.Identifier),
                new("C", 1, Constants.Timing.LOCATION_FINISH, e.Identifier),
            };
            Store.AddRemoteReaders(e.Identifier, readers);

            Store.DeleteRemoteReader(e.Identifier, readers[0]);
            Assert.Equal(2, Store.GetRemoteReaders(e.Identifier).Count);

            Store.DeleteRemoteReaders(e.Identifier, [readers[1], readers[2]]);
            Assert.Empty(Store.GetRemoteReaders(e.Identifier));
            Assert.Empty(Db.GetRemoteReaders(e.Identifier));
        }

        [Fact]
        public void GetRemoteReaders_OtherEvent_ReadsFromDatabase()
        {
            AddCurrentEvent();
            var other = AddEvent("Other");
            Db.AddRemoteReaders(other.Identifier, [new RemoteReader("X", 1, Constants.Timing.LOCATION_FINISH, other.Identifier)]);
            Assert.Single(Store.GetRemoteReaders(other.Identifier));
        }
    }

    public class MemStoreAlertTests : MemStoreTestBase
    {
        [Fact]
        public void SmsAndEmailAlerts_AreCachedForCurrentEventOnly()
        {
            var e = AddCurrentEvent();
            Store.AddSmsAlert(e.Identifier, 10, Constants.Timing.SEGMENT_FINISH);
            Store.AddEmailAlert(e.Identifier, 10);

            Assert.Equal([(10, Constants.Timing.SEGMENT_FINISH)], Store.GetSmsAlerts(e.Identifier));
            Assert.Equal([10], Store.GetEmailAlerts(e.Identifier));
            Assert.Empty(Store.GetSmsAlerts(e.Identifier + 1));
            Assert.Single(Db.GetSmsAlerts(e.Identifier));
        }

        [Fact]
        public void SmsSubscriptions_AddGetDelete()
        {
            var e = AddCurrentEvent();
            Store.AddSmsSubscriptions(e.Identifier, [new ApiSmsSubscription { Bib = "1", Phone = "555" }]);
            Assert.Single(Store.GetSmsSubscriptions(e.Identifier));

            Store.DeleteSmsSubscriptions(e.Identifier);
            Assert.Empty(Store.GetSmsSubscriptions(e.Identifier));
            Assert.Empty(Db.GetSmsSubscriptions(e.Identifier));
        }

        [Fact]
        public void SetCurrentEvent_LoadsAlertsFromDatabase()
        {
            var e = AddEvent();
            Db.AddSmsAlert(e.Identifier, 5, 1);
            Db.AddEmailAlert(e.Identifier, 6);
            Db.AddSmsSubscriptions(e.Identifier, [new ApiSmsSubscription { Bib = "1", Phone = "555" }]);

            Store.SetCurrentEvent(e.Identifier);

            Assert.Single(Store.GetSmsAlerts(e.Identifier));
            Assert.Single(Store.GetEmailAlerts(e.Identifier));
            Assert.Single(Store.GetSmsSubscriptions(e.Identifier));
        }
    }
}
