using Chronokeep.Database;

namespace Chronokeep.Test.Database
{
    public class SetupTests : DatabaseTestBase
    {
        [Theory]
        [InlineData("events")]
        [InlineData("distances")]
        [InlineData("timing_locations")]
        [InlineData("participants")]
        [InlineData("eventspecific")]
        [InlineData("segments")]
        [InlineData("chipreads")]
        [InlineData("time_results")]
        [InlineData("settings")]
        [InlineData("age_groups")]
        [InlineData("timing_systems")]
        [InlineData("alarms")]
        [InlineData("remote_readers")]
        [InlineData("sms_alert")]
        [InlineData("email_alert")]
        [InlineData("sms_ban_list")]
        [InlineData("email_ban_list")]
        [InlineData("sms_subscriptions")]
        [InlineData("chronoclocks")]
        [InlineData("bib_chip_assoc")]
        [InlineData("results_api")]
        public void Initialize_CreatesTable(string table)
        {
            Assert.Equal(1L, Count("sqlite_master", $"type='table' AND name='{table}'"));
        }

        [Fact]
        public void Initialize_StoresDatabaseVersion()
        {
            var version = Db.GetAppSetting(Constants.Settings.DATABASE_VERSION);
            Assert.NotNull(version);
            Assert.True(int.Parse(version.Value) >= SqLiteInterface.MINIMUM_COMPATIBLE_VERSION);
        }

        [Fact]
        public void Initialize_StoresProgramVersionWithoutGitSuffix()
        {
            var version = Db.GetAppSetting(Constants.Settings.PROGRAM_VERSION);
            Assert.NotNull(version);
            Assert.DoesNotContain("-", version.Value);
        }

        [Fact]
        public void Initialize_InsertsPlaceholderParticipant()
        {
            Assert.Equal(1L, Count("participants", "participant_id=0"));
        }

        [Fact]
        public void Initialize_RunTwice_KeepsExistingData()
        {
            var e = AddEvent();
            Db.Initialize();
            Assert.NotNull(Db.GetEvent(e.Identifier));
            Assert.Equal(1L, Count("events"));
        }

        [Fact]
        public void Initialize_WhenDatabaseRequiresNewerClient_Throws()
        {
            Db.SetAppSetting(Constants.Settings.MINIMUM_COMPATIBLE_DATABASE, "100000");
            Assert.Throws<InvalidDatabaseVersion>(() => Db.Initialize());
        }

        [Fact]
        public void ResetDatabase_DeletesRowsButKeepsTables()
        {
            var e = AddEvent();
            AddDistance(e.Identifier);
            Db.AddBannedPhone("5551234567");

            Db.ResetDatabase();

            Assert.Empty(Db.GetEvents());
            Assert.Equal(0L, Count("distances"));
            Assert.Empty(Db.GetBannedPhones());
            Assert.Equal(1L, Count("sqlite_master", "type='table' AND name='events'"));
        }

        [Fact]
        public void HardResetDatabase_RecreatesEmptySchema()
        {
            var e = AddEvent();
            AddDistance(e.Identifier);

            Db.HardResetDatabase();

            Assert.Empty(Db.GetEvents());
            Assert.Equal(0L, Count("distances"));
            Assert.NotNull(Db.GetAppSetting(Constants.Settings.DATABASE_VERSION));
        }
    }
}
