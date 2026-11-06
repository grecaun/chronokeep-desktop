using Chronokeep.Objects;
using Chronokeep.Objects.ChronoKeepAPI;
using Chronokeep.Objects.ChronokeepRemote;

namespace Chronokeep.Test.Database
{
    public class AgeGroupTests : DatabaseTestBase
    {
        [Fact]
        public void AddAgeGroup_ThenGet_RoundTrips()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            int id = Db.AddAgeGroup(new AgeGroup(e.Identifier, d.Identifier, 20, 29, "Twenties"));

            var group = Assert.Single(Db.GetAgeGroups(e.Identifier));
            Assert.Equal(id, group.GroupId);
            Assert.Equal(20, group.StartAge);
            Assert.Equal(29, group.EndAge);
            Assert.Equal("Twenties", group.CustomName);
            Assert.False(group.LastGroup);
        }

        [Fact]
        public void AddAgeGroups_AddsAll_AndFiltersByDistance()
        {
            var e = AddEvent();
            var d1 = AddDistance(e.Identifier, "5K");
            var d2 = AddDistance(e.Identifier, "10K");
            Db.AddAgeGroups([
                new AgeGroup(e.Identifier, d1.Identifier, 0, 19),
                new AgeGroup(e.Identifier, d1.Identifier, 20, 99),
                new AgeGroup(e.Identifier, d2.Identifier, 0, 99)]);

            Assert.Equal(3, Db.GetAgeGroups(e.Identifier).Count);
            Assert.Equal(2, Db.GetAgeGroups(e.Identifier, d1.Identifier).Count);
        }

        [Fact]
        public void UpdateAgeGroup_ChangesFields()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            int id = Db.AddAgeGroup(new AgeGroup(e.Identifier, d.Identifier, 20, 29));
            Db.UpdateAgeGroup(new AgeGroup(id, e.Identifier, d.Identifier, 25, 34, Constants.Timing.AGEGROUPS_LASTGROUP_FALSE, "Custom"));

            var group = Assert.Single(Db.GetAgeGroups(e.Identifier));
            Assert.Equal(25, group.StartAge);
            Assert.Equal(34, group.EndAge);
            Assert.Equal("Custom", group.CustomName);
        }

        [Fact]
        public void RemoveAgeGroupVariants_DeleteRows()
        {
            var e = AddEvent();
            var d1 = AddDistance(e.Identifier, "5K");
            var d2 = AddDistance(e.Identifier, "10K");
            var groups = Db.AddAgeGroups([
                new AgeGroup(e.Identifier, d1.Identifier, 0, 19),
                new AgeGroup(e.Identifier, d1.Identifier, 20, 29),
                new AgeGroup(e.Identifier, d1.Identifier, 30, 39),
                new AgeGroup(e.Identifier, d2.Identifier, 0, 99),
                new AgeGroup(e.Identifier, d2.Identifier, 0, 49)]);

            Db.RemoveAgeGroup(groups[0]);
            Assert.Equal(4, Db.GetAgeGroups(e.Identifier).Count);

            Db.RemoveAgeGroups([groups[1], groups[2]]);
            Assert.Empty(Db.GetAgeGroups(e.Identifier, d1.Identifier));

            Db.RemoveAgeGroups(e.Identifier, d2.Identifier);
            Assert.Empty(Db.GetAgeGroups(e.Identifier));
        }

        [Fact]
        public void ResetAgeGroups_OnlyClearsThatEvent()
        {
            var a = AddEvent("A");
            var b = AddEvent("B");
            Db.AddAgeGroup(new AgeGroup(a.Identifier, -1, 0, 99));
            Db.AddAgeGroup(new AgeGroup(b.Identifier, -1, 0, 99));
            Db.ResetAgeGroups(a.Identifier);
            Assert.Empty(Db.GetAgeGroups(a.Identifier));
            Assert.Single(Db.GetAgeGroups(b.Identifier));
        }
    }

    public class TimingSystemTests : DatabaseTestBase
    {
        private const string Type = Constants.Readers.SYSTEM_CHRONOKEEP_PORTAL;

        [Fact]
        public void AddTimingSystem_ThenGet_RoundTrips()
        {
            int id = Db.AddTimingSystem(new TimingSystem(0, "10.0.0.5", 4488, Constants.Timing.LOCATION_FINISH, Type));
            var sys = Assert.Single(Db.GetTimingSystems());
            Assert.Equal(id, sys.SystemIdentifier);
            Assert.Equal("10.0.0.5", sys.IpAddress);
            Assert.Equal(4488, sys.Port);
            Assert.Equal(Constants.Timing.LOCATION_FINISH, sys.LocationId);
            Assert.Equal(Type, sys.Type);
        }

        [Fact]
        public void UpdateTimingSystem_ChangesFields()
        {
            int id = Db.AddTimingSystem(new TimingSystem(0, "10.0.0.5", 4488, Constants.Timing.LOCATION_FINISH, Type));
            Db.UpdateTimingSystem(new TimingSystem(id, "10.0.0.6", 5000, Constants.Timing.LOCATION_START, Type));
            var sys = Assert.Single(Db.GetTimingSystems());
            Assert.Equal("10.0.0.6", sys.IpAddress);
            Assert.Equal(5000, sys.Port);
            Assert.Equal(Constants.Timing.LOCATION_START, sys.LocationId);
        }

        [Fact]
        public void SetTimingSystems_ReplacesAllSystems()
        {
            Db.AddTimingSystem(new TimingSystem(0, "10.0.0.1", 1, Constants.Timing.LOCATION_FINISH, Type));
            Db.SetTimingSystems([
                new TimingSystem(0, "10.0.0.2", 2, Constants.Timing.LOCATION_FINISH, Type),
                new TimingSystem(0, "10.0.0.3", 3, Constants.Timing.LOCATION_START, Type)]);
            Assert.Equal(["10.0.0.2", "10.0.0.3"], Db.GetTimingSystems().Select(s => s.IpAddress).Order());
        }

        [Fact]
        public void RemoveTimingSystem_ById_And_ByObject()
        {
            int a = Db.AddTimingSystem(new TimingSystem(0, "10.0.0.1", 1, Constants.Timing.LOCATION_FINISH, Type));
            int b = Db.AddTimingSystem(new TimingSystem(0, "10.0.0.2", 2, Constants.Timing.LOCATION_FINISH, Type));
            Db.RemoveTimingSystem(a);
            Assert.Single(Db.GetTimingSystems());
            Db.RemoveTimingSystem(new TimingSystem(b, "10.0.0.2", 2, Constants.Timing.LOCATION_FINISH, Type));
            Assert.Empty(Db.GetTimingSystems());
        }
    }

    public class AlarmTests : DatabaseTestBase
    {
        [Fact]
        public void SaveAlarm_ThenGetAlarms_RoundTrips()
        {
            var e = AddEvent();
            int id = Db.SaveAlarm(e.Identifier, new Alarm(-1, "12", "", true, 3));
            var alarm = Assert.Single(Db.GetAlarms(e.Identifier));
            Assert.Equal(id, alarm.Identifier);
            Assert.Equal("12", alarm.Bib);
            Assert.True(alarm.Enabled);
            Assert.Equal(3, alarm.AlarmSound);
        }

        [Fact]
        public void SaveAlarms_SavesEach_AndAssignsIds()
        {
            var e = AddEvent();
            var saved = Db.SaveAlarms(e.Identifier, [new Alarm(-1, "1", "", true, 1), new Alarm(-1, "2", "", false, 2)]);
            Assert.Equal(2, saved.Select(a => a.Identifier).Distinct().Count());
            Assert.Equal(["1", "2"], Db.GetAlarms(e.Identifier).Select(a => a.Bib).Order());
        }

        [Fact]
        public void DeleteAlarm_And_DeleteAlarms()
        {
            var e = AddEvent();
            var saved = Db.SaveAlarms(e.Identifier, [new Alarm(-1, "1", "", true, 1), new Alarm(-1, "2", "", true, 1), new Alarm(-1, "3", "", true, 1)]);
            Db.DeleteAlarm(saved[0]);
            Assert.Equal(2, Db.GetAlarms(e.Identifier).Count);
            Db.DeleteAlarms(e.Identifier);
            Assert.Empty(Db.GetAlarms(e.Identifier));
        }
    }

    public class RemoteReaderTests : DatabaseTestBase
    {
        [Fact]
        public void AddRemoteReaders_ThenGet_RoundTrips()
        {
            var e = AddEvent();
            Db.AddRemoteReaders(e.Identifier, [
                new RemoteReader("Reader A", 1, Constants.Timing.LOCATION_FINISH, e.Identifier),
                new RemoteReader("Reader B", 1, Constants.Timing.LOCATION_START, e.Identifier)]);

            var readers = Db.GetRemoteReaders(e.Identifier).OrderBy(r => r.Name).ToList();
            Assert.Equal(["Reader A", "Reader B"], readers.Select(r => r.Name));
            Assert.Equal(Constants.Timing.LOCATION_START, readers[1].LocationId);
            Assert.All(readers, r => Assert.Equal(1, r.ApiiDentifier));
        }

        [Fact]
        public void DeleteRemoteReader_And_DeleteRemoteReaders()
        {
            var e = AddEvent();
            var readers = new List<RemoteReader>
            {
                new("A", 1, Constants.Timing.LOCATION_FINISH, e.Identifier),
                new("B", 1, Constants.Timing.LOCATION_FINISH, e.Identifier),
                new("C", 1, Constants.Timing.LOCATION_FINISH, e.Identifier),
            };
            Db.AddRemoteReaders(e.Identifier, readers);

            Db.DeleteRemoteReader(e.Identifier, readers[0]);
            Assert.Equal(2, Db.GetRemoteReaders(e.Identifier).Count);

            Db.DeleteRemoteReaders(e.Identifier, [readers[1], readers[2]]);
            Assert.Empty(Db.GetRemoteReaders(e.Identifier));
        }
    }

    public class AlertTests : DatabaseTestBase
    {
        [Fact]
        public void SmsAlerts_RoundTrip()
        {
            var e = AddEvent();
            Db.AddSmsAlert(e.Identifier, 10, Constants.Timing.SEGMENT_FINISH);
            Db.AddSmsAlert(e.Identifier, 11, 5);
            Assert.Equal([(10, Constants.Timing.SEGMENT_FINISH), (11, 5)], Db.GetSmsAlerts(e.Identifier).Order());
        }

        [Fact]
        public void EmailAlerts_RoundTrip()
        {
            var e = AddEvent();
            Db.AddEmailAlert(e.Identifier, 10);
            Db.AddEmailAlert(e.Identifier, 11);
            Assert.Equal([10, 11], Db.GetEmailAlerts(e.Identifier).Order());
            Assert.Empty(Db.GetEmailAlerts(e.Identifier + 1));
        }
    }

    public class BannedTests : DatabaseTestBase
    {
        [Fact]
        public void BannedPhones_AddRemoveClear()
        {
            Db.AddBannedPhone("1");
            Db.AddBannedPhones(["2", "3", "4"]);
            Assert.Equal(["1", "2", "3", "4"], Db.GetBannedPhones().Order());

            Db.RemoveBannedPhone("1");
            Db.RemoveBannedPhones(["2", "3"]);
            Assert.Equal(["4"], Db.GetBannedPhones());

            Db.ClearBannedPhones();
            Assert.Empty(Db.GetBannedPhones());
        }

        [Fact]
        public void BannedEmails_AddRemoveClear()
        {
            Db.AddBannedEmail("a@x.com");
            Db.AddBannedEmails(["b@x.com", "c@x.com", "d@x.com"]);
            Assert.Equal(["a@x.com", "b@x.com", "c@x.com", "d@x.com"], Db.GetBannedEmails().Order());

            Db.RemoveBannedEmail("a@x.com");
            Db.RemoveBannedEmails(["b@x.com", "c@x.com"]);
            Assert.Equal(["d@x.com"], Db.GetBannedEmails());

            Db.ClearBannedEmails();
            Assert.Empty(Db.GetBannedEmails());
        }
    }

    public class SmsSubscriptionTests : DatabaseTestBase
    {
        [Fact]
        public void SmsSubscriptions_AddGetDelete()
        {
            var e = AddEvent();
            Db.AddSmsSubscriptions(e.Identifier, [
                new ApiSmsSubscription { Bib = "1", First = "A", Last = "B", Phone = "555" },
                new ApiSmsSubscription { Bib = "2", First = "C", Last = "D", Phone = "556" }]);

            var subs = Db.GetSmsSubscriptions(e.Identifier).OrderBy(s => s.Bib).ToList();
            Assert.Equal(["1", "2"], subs.Select(s => s.Bib));
            Assert.Equal("555", subs[0].Phone);

            Db.DeleteSmsSubscriptions(e.Identifier);
            Assert.Empty(Db.GetSmsSubscriptions(e.Identifier));
        }
    }

    public class ClockTests : DatabaseTestBase
    {
        [Fact]
        public void AddClock_ThenGet_RoundTrips()
        {
            int id = Db.AddClock(new Chronoclock { Name = "Finish Clock", Url = "10.0.0.9", Enabled = true });
            var clock = Assert.Single(Db.GetClocks());
            Assert.Equal(id, clock.Identifier);
            Assert.Equal("Finish Clock", clock.Name);
            Assert.Equal("10.0.0.9", clock.Url);
            Assert.True(clock.Enabled);
        }

        [Fact]
        public void UpdateClock_ChangesFields()
        {
            int id = Db.AddClock(new Chronoclock { Name = "Clock", Url = "10.0.0.9" });
            Db.UpdateClock(new Chronoclock { Identifier = id, Name = "Renamed", Url = "10.0.0.10", Enabled = true });
            var clock = Assert.Single(Db.GetClocks());
            Assert.Equal("Renamed", clock.Name);
            Assert.Equal("10.0.0.10", clock.Url);
            Assert.True(clock.Enabled);
        }

        [Fact]
        public void RemoveClocks_RemovesEachGivenClock()
        {
            int a = Db.AddClock(new Chronoclock { Name = "A", Url = "1" });
            int b = Db.AddClock(new Chronoclock { Name = "B", Url = "2" });
            Db.AddClock(new Chronoclock { Name = "C", Url = "3" });
            Db.RemoveClocks([new Chronoclock { Identifier = a }, new Chronoclock { Identifier = b }]);
            Assert.Equal("C", Assert.Single(Db.GetClocks()).Name);
        }
    }
}
