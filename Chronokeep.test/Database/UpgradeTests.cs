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
        private const int CurrentVersion = 76;

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

        /// <summary>Undoes upgrade step 75 (the Runsignup registration schema; birthdates need no undo).</summary>
        private void DowngradeTo75() => Exec(
            "DROP TABLE registration_distances;" +
            "ALTER TABLE events DROP COLUMN registration_api_id;" +
            "ALTER TABLE events DROP COLUMN registration_race_id;" +
            "ALTER TABLE events DROP COLUMN registration_comment_fields;" +
            "ALTER TABLE eventspecific DROP COLUMN eventspecific_external_id;" +
            "ALTER TABLE eventspecific DROP COLUMN eventspecific_removed;");

        /// <summary>Undoes upgrade steps 74 and 75 (74 adds event_use_male_female).</summary>
        private void DowngradeTo74()
        {
            DowngradeTo75();
            Exec("ALTER TABLE events DROP COLUMN event_use_male_female;");
        }

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

        /// <summary>Stores a birthday exactly as given, bypassing the app's normalization, like an older version would.</summary>
        private void SetRawBirthday(int participantId, string? birthday)
            => Exec($"UPDATE participants SET participant_birthday={(birthday == null ? "NULL" : $"'{birthday}'")} WHERE participant_id={participantId};");

        private string? RawBirthday(int participantId)
            => Scalar($"SELECT participant_birthday FROM participants WHERE participant_id={participantId};") as string;

        [Fact]
        public void Version75_RewritesParticipantBirthdatesAsYearMonthDay()
        {
            Event e = AddEvent();
            Distance d = AddDistance(e.Identifier);
            Participant slashed = AddParticipant(e.Identifier, d, "1", first: "Slash");
            Participant unpadded = AddParticipant(e.Identifier, d, "2", first: "Unpadded");
            Participant timestamped = AddParticipant(e.Identifier, d, "3", first: "Stamp");
            Participant iso = AddParticipant(e.Identifier, d, "4", first: "Iso");
            Participant blank = AddParticipant(e.Identifier, d, "5", first: "Blank");
            Participant junk = AddParticipant(e.Identifier, d, "6", first: "Junk");
            SetRawBirthday(slashed.Identifier, "1990-03-04");
            SetRawBirthday(unpadded.Identifier, "1990/3/4");
            SetRawBirthday(timestamped.Identifier, "1985-07-08 00:00:00");
            SetRawBirthday(iso.Identifier, "2001-12-31");
            SetRawBirthday(blank.Identifier, "");
            SetRawBirthday(junk.Identifier, "unknown");
            DowngradeTo75();
            SetVersion(75);

            Reopen();

            Assert.Equal(CurrentVersion, StoredVersion());
            Assert.Equal("1990/03/04", RawBirthday(slashed.Identifier));
            Assert.Equal("1990/03/04", RawBirthday(unpadded.Identifier));
            Assert.Equal("1985/07/08", RawBirthday(timestamped.Identifier));
            Assert.Equal("2001/12/31", RawBirthday(iso.Identifier));
            Assert.Equal("", RawBirthday(blank.Identifier));
            Assert.Equal("unknown", RawBirthday(junk.Identifier));
            Assert.Equal("1901/01/01", RawBirthday(0));
        }

        [Fact]
        public void Version75_MergesTwoRowsForOnePersonThatOnlyDifferedInDateFormat()
        {
            Event first = AddEvent("First Race");
            Event second = AddEvent("Second Race");
            Distance d1 = AddDistance(first.Identifier);
            Distance d2 = AddDistance(second.Identifier);
            Participant kept = AddParticipant(first.Identifier, d1, "1", birthday: "1990-01-02");
            // Same name, street and zip; stored by an older version as 1990/1/2, so it was a separate row.
            Participant duplicate = AddParticipant(second.Identifier, d2, "2", birthday: "1991-05-05");
            SetRawBirthday(duplicate.Identifier, "1990/1/2");
            Assert.NotEqual(kept.Identifier, duplicate.Identifier);
            DowngradeTo75();
            SetVersion(75);

            Reopen();

            Assert.Equal(1, Count("participants", "participant_first='Jane'"));
            Assert.Equal("1990/01/02", RawBirthday(kept.Identifier));
            Assert.Equal(0, Count("participants", $"participant_id={duplicate.Identifier}"));
            Assert.Equal(2, Count("eventspecific", $"participant_id={kept.Identifier}"));
            Assert.Equal(1, Count("eventspecific", $"participant_id={kept.Identifier} AND event_id={second.Identifier}"));
        }

        [Fact]
        public void Version75_KeepsTheRowAlreadyInTheStoredFormatWhenMerging()
        {
            Event e = AddEvent();
            Event other = AddEvent("Other");
            Distance d = AddDistance(e.Identifier);
            Distance d2 = AddDistance(other.Identifier);
            Participant oldFormat = AddParticipant(e.Identifier, d, "1", birthday: "1980-01-01");
            Participant newFormat = AddParticipant(other.Identifier, d2, "2", birthday: "1990-01-02");
            SetRawBirthday(oldFormat.Identifier, "1990-01-02");
            DowngradeTo75();
            SetVersion(75);

            Reopen();

            Assert.Equal(0, Count("participants", $"participant_id={oldFormat.Identifier}"));
            Assert.Equal("1990/01/02", RawBirthday(newFormat.Identifier));
            Assert.Equal(2, Count("eventspecific", $"participant_id={newFormat.Identifier}"));
        }

        [Fact]
        public void Version75_DoesNotMergeRowsWithANullZip()
        {
            Event e = AddEvent();
            Distance d = AddDistance(e.Identifier);
            Participant a = AddParticipant(e.Identifier, d, "1", birthday: "1990-01-02");
            Participant b = AddParticipant(e.Identifier, d, "2", birthday: "1970-01-01");
            Exec($"UPDATE participants SET participant_zip=NULL WHERE participant_id IN ({a.Identifier},{b.Identifier});");
            SetRawBirthday(b.Identifier, "1990-1-2");
            DowngradeTo75();
            SetVersion(75);

            Reopen();

            Assert.Equal("1990/01/02", RawBirthday(a.Identifier));
            Assert.Equal("1990/01/02", RawBirthday(b.Identifier));
        }

        [Fact]
        public void Version75_AddsTheRunsignupSchemaWithDefaultsForExistingRows()
        {
            Event e = AddEvent();
            Distance d = AddDistance(e.Identifier);
            Participant p = AddParticipant(e.Identifier, d, "1");
            DowngradeTo75();
            SetVersion(75);

            Reopen();

            Assert.Equal(CurrentVersion, StoredVersion());
            Assert.True(TableExists("registration_distances"));
            foreach (string column in new[] { "registration_api_id", "registration_race_id", "registration_comment_fields" })
            {
                Assert.True(ColumnExists("events", column), column);
            }
            Assert.True(ColumnExists("eventspecific", "eventspecific_external_id"));
            Assert.True(ColumnExists("eventspecific", "eventspecific_removed"));
            Event upgraded = Db.GetEvent(e.Identifier)!;
            Assert.Equal(Constants.ApiConstants.NULL_ID, upgraded.RegistrationApiId);
            Assert.Equal("", upgraded.RegistrationRaceId);
            Assert.False(upgraded.IsRegistrationLinked);
            Participant upgradedRunner = Assert.Single(Db.GetParticipants(e.Identifier));
            Assert.Equal("", upgradedRunner.EventSpecific.ExternalId);
            Assert.False(upgradedRunner.EventSpecific.IsRemoved);
            Assert.Equal(p.EventSpecific.Identifier, upgradedRunner.EventSpecific.Identifier);
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
