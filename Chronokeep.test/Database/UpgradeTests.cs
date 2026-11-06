using Chronokeep.Database;
using Chronokeep.Objects;
using System.Data.SQLite;

namespace Chronokeep.Test.Database
{
    /// <summary>
    /// Builds databases as they looked at older versions (by undoing the later upgrade steps on a
    /// fresh database) and checks that opening them upgrades them to the current version.
    /// </summary>
    public class UpgradeTests : DatabaseTestBase
    {
        private const int CurrentVersion = 75;

        private void Exec(string sql)
        {
            using SQLiteConnection connection = new($"Data Source={DbPath};Version=3");
            connection.Open();
            using SQLiteCommand command = new(sql, connection);
            command.ExecuteNonQuery();
        }

        private void SetVersion(int version)
            => Exec($"UPDATE settings SET value='{version}' WHERE setting='{Constants.Settings.DATABASE_VERSION}';");

        private bool ColumnExists(string table, string column)
            => Convert.ToInt64(Scalar($"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name='{column}';")) == 1;

        private bool TableExists(string table) => Count("sqlite_master", $"type='table' AND name='{table}'") == 1;

        private int StoredVersion() => int.Parse(Db.GetAppSetting(Constants.Settings.DATABASE_VERSION)!.Value);

        /// <summary>Opens the database the way the app does at startup.</summary>
        private SqLiteInterface Reopen()
        {
            SqLiteInterface db = new(DbPath);
            db.Initialize();
            return db;
        }

        /// <summary>Undoes upgrade step 74 (adds event_use_male_female).</summary>
        private void DowngradeTo74() => Exec("ALTER TABLE events DROP COLUMN event_use_male_female;");

        /// <summary>Undoes upgrade steps 63 through 74 so the schema matches version 63.</summary>
        private void DowngradeTo63()
        {
            DowngradeTo74();
            Exec(
                // 71: chronoclocks
                "DROP TABLE chronoclocks;" +
                // 70
                "ALTER TABLE events DROP COLUMN event_start_max_occurrences;" +
                // 69
                "ALTER TABLE eventspecific DROP COLUMN eventspecific_uploaded_version;" +
                "ALTER TABLE eventspecific DROP COLUMN eventspecific_version;" +
                // 68
                "ALTER TABLE time_results DROP COLUMN timeresult_division_place;" +
                "ALTER TABLE eventspecific DROP COLUMN eventspecific_division;" +
                // 67
                "ALTER TABLE distances DROP COLUMN distance_certification;" +
                // 66
                "ALTER TABLE distances DROP COLUMN distance_upload_results;" +
                "ALTER TABLE events DROP COLUMN event_upload_specific_distance_results;" +
                // 65: indexes
                "DROP INDEX idx_event_id; DROP INDEX idx_segment_id; DROP INDEX idx_location_id;" +
                "DROP INDEX idx_chipread_id; DROP INDEX idx_participant_id; DROP INDEX idx_distance_id;" +
                // 64
                "DROP TABLE sms_subscriptions;" +
                "DROP TABLE sms_alert;" +
                "CREATE TABLE sms_alert(event_id INTEGER NOT NULL REFERENCES events(event_id), " +
                "eventspecific_id INTEGER NOT NULL REFERENCES eventspecific(eventspecific_id), UNIQUE(event_id, eventspecific_id));" +
                "ALTER TABLE segments DROP COLUMN map_link;" +
                "ALTER TABLE segments DROP COLUMN gps;" +
                "ALTER TABLE events DROP COLUMN event_days_allowed;" +
                // 63
                "ALTER TABLE events DROP COLUMN event_age_groups_as_divisions;");
        }

        [Fact]
        public void CurrentDatabase_IsNotChanged()
        {
            Assert.Equal(CurrentVersion, StoredVersion());
            Reopen();
            Assert.Equal(CurrentVersion, StoredVersion());
        }

        [Fact]
        public void Version74_UpgradesToCurrent()
        {
            DowngradeTo74();
            SetVersion(74);

            Reopen();

            Assert.Equal(CurrentVersion, StoredVersion());
            Assert.True(ColumnExists("events", "event_use_male_female"));
        }

        [Fact]
        public void Version73_ClearsOldPlaceholderBibsAndChips()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            var p = AddParticipant(e.Identifier, d, "1");
            Exec($"UPDATE eventspecific SET eventspecific_bib='-1' WHERE eventspecific_id={p.EventSpecific.Identifier};" +
                 $"INSERT INTO chipreads (event_id, location_id, read_chipnumber, read_seconds, read_milliseconds, read_antenna, read_reader, " +
                 $"read_box, read_logindex, read_rssi, read_isrewind, read_readertime, read_starttime, read_time_seconds, read_time_milliseconds, read_bib) " +
                 $"VALUES ({e.Identifier}, -2, '-1', 1, 0, 0, '', '', 0, 0, 0, '', 0, 1, 0, '-1');");
            DowngradeTo74();
            SetVersion(73);

            Reopen();

            Assert.Equal(CurrentVersion, StoredVersion());
            Assert.Equal(0L, Count("eventspecific", "eventspecific_bib='-1'"));
            Assert.Equal(0L, Count("chipreads", "read_chipnumber='-1' OR read_bib='-1'"));
            Assert.Equal(1L, Count("chipreads", "read_chipnumber='' AND read_bib=''"));
        }

        [Fact]
        public void Version72_UpgradesAndKeepsParticipants()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            AddParticipant(e.Identifier, d, "1");
            DowngradeTo74();
            SetVersion(72);

            var db = Reopen();

            Assert.Equal(CurrentVersion, StoredVersion());
            Assert.Equal("1", Assert.Single(db.GetParticipants(e.Identifier)).Bib);
        }

        [Fact]
        public void Version63_UpgradesToCurrentAndKeepsData()
        {
            var e = AddEvent();
            var d = AddDistance(e.Identifier);
            AddParticipant(e.Identifier, d, "1", division: "Elite");
            Db.AddSegment(new Segment(e.Identifier, d.Identifier, Constants.Timing.LOCATION_FINISH, 1, 1, 1, 0, "Lap", "gps", "map"));
            DowngradeTo63();
            SetVersion(63);
            Assert.False(ColumnExists("events", "event_use_male_female"));

            var db = Reopen();

            Assert.Equal(CurrentVersion, StoredVersion());
            Assert.True(TableExists("chronoclocks"));
            Assert.True(TableExists("sms_subscriptions"));
            Assert.True(ColumnExists("eventspecific", "eventspecific_division"));
            Assert.True(ColumnExists("distances", "distance_certification"));
            Assert.True(ColumnExists("segments", "map_link"));
            Assert.True(ColumnExists("sms_alert", "segment_id"));
            Assert.Equal(e.Name, Assert.Single(db.GetEvents()).Name);
            Assert.Equal("1", Assert.Single(db.GetParticipants(e.Identifier)).Bib);
            Assert.Single(db.GetSegments(e.Identifier));
            // New columns start with their defaults.
            Assert.Equal("", Assert.Single(db.GetParticipants(e.Identifier)).Division);
            Assert.True(db.GetDistance(d.Identifier)!.Upload);
        }

        [Fact]
        public void Version71_UpgradesToCurrent()
        {
            DowngradeTo74();
            Exec("DROP TABLE chronoclocks;");
            SetVersion(71);

            Reopen();

            Assert.Equal(CurrentVersion, StoredVersion());
            Assert.True(TableExists("chronoclocks"));
        }
    }
}
