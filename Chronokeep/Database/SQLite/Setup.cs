using Chronokeep.Helpers;
using Chronokeep.Objects;
using System;
using System.Collections;
using System.Data.SQLite;
using System.IO;
using System.Reflection;

namespace Chronokeep.Database.SQLite
{
    internal static class Setup
    {
        internal static void Initialize(int version, string connectionInfo)
        {
            ArrayList queries = [];
            SQLiteConnection connection = new($"Data Source={connectionInfo};Version=3");
            connection.Open();
            SQLiteCommand command = new("SELECT name FROM sqlite_master WHERE type='table' AND name='settings'", connection);
            SQLiteDataReader reader = command.ExecuteReader();
            int oldVersion = -1;
            if (reader.Read())
            {
                Log.D("SQLite.Setup", "Tables do not need to be made.");
                try
                {
                    // As of version 43 we've changed how we store settings values to something more sensible.
                    command = new SQLiteCommand($"SELECT value FROM settings WHERE setting='{Constants.Settings.DATABASE_VERSION}';", connection);
                    // If we've got an upgraded version then command.ExecuteReader will throw an exception.
                    using SQLiteDataReader versionChecker = command.ExecuteReader();
                    if (versionChecker.Read())
                    {
                        oldVersion = Convert.ToInt32(versionChecker["value"]);
                    }
                    else
                    {
                        Log.D("SQLite.Setup", "Tables made, database version not found.");
                    }
                    versionChecker.Close();
                }
                catch
                {
                    // Check for an older version
                    Log.D("SQLite.Setup", "We may have a database older than version 43.");
                    command = new SQLiteCommand("SELECT version FROM settings;", connection);
                    using SQLiteDataReader v2Checker = command.ExecuteReader();
                    if (v2Checker.Read())
                    {
                        oldVersion = Convert.ToInt32(v2Checker["version"]);
                    }
                    else
                    {
                        Log.D("SQLite.Setup", "Tables made, database version not found.");
                    }
                    v2Checker.Close();
                }
                Log.D("SQLite.Setup", $"Old Version: {oldVersion}");
            }
            else
            {
                Log.D("SQLite.Setup", "Tables haven't been created. Doing so now.");
                command = new SQLiteCommand("PRAGMA foreign_keys = ON;", connection); // Ensure Foreign key constraints work.
                command.ExecuteNonQuery();
                queries.Add("CREATE TABLE IF NOT EXISTS bib_chip_assoc (" +
                    "event_id INTEGER NOT NULL REFERENCES events(event_id)," +
                    "bib VARCHAR NOT NULL," +
                    "chip VARCHAR NOT NULL," +
                    "UNIQUE (event_id, chip) ON CONFLICT REPLACE" +
                    ");");
                queries.Add("CREATE TABLE IF NOT EXISTS results_api(" +
                    "api_id INTEGER PRIMARY KEY," +
                    "api_type VARCHAR(50) NOT NULL," +
                    "api_url VARCHAR(150) NOT NULL," +
                    "api_auth_token VARCHAR(100) NOT NULL," +
                    "api_nickname VARCHAR(75) NOT NULL," +
                    "api_web_url VARCHAR NOT NULL DEFAULT ''," +
                    "UNIQUE (api_url, api_auth_token) ON CONFLICT REPLACE);");
                queries.Add("CREATE TABLE IF NOT EXISTS events (" +
                    "event_id INTEGER PRIMARY KEY," +
                    "event_name VARCHAR(100) NOT NULL," +
                    "event_date VARCHAR(15) NOT NULL," +
                    "event_yearcode VARCHAR(10) NOT NULL DEFAULT ''," +
                    "event_rank_by_gun INTEGER DEFAULT 1," +
                    "event_common_age_groups INTEGER DEFAULT 1," +
                    "event_common_start_finish INTEGER DEFAULT 1," +
                    "event_distance_specific_segments INTEGER DEFAULT 0," +
                    "event_start_time_seconds INTEGER NOT NULL DEFAULT -1," +
                    "event_start_time_milliseconds INTEGER NOT NULL DEFAULT 0," +
                    "event_finish_max_occurances INTEGER NOT NULL DEFAULT 1," +
                    "event_finish_ignore_within INTEGER NOT NULL DEFAULT 0," +
                    "event_start_window INTEGER NOT NULL DEFAULT -1," +
                    "event_start_max_occurrences INTEGER NOT NULL DEFAULT 1," +
                    $"event_age_groups_as_divisions INTEGER NOT NULL DEFAULT {Constants.Timing.AGEGROUPS_LASTGROUP_FALSE}," +
                    $"event_type INTEGER NOT NULL DEFAULT {Constants.Timing.EVENT_TYPE_DISTANCE}," +
                    "event_days_allowed INTEGER NOT NULL DEFAULT 1," +
                    "event_upload_specific_distance_results INTEGER NOT NULL DEFAULT 0," +
                    "api_id INTEGER REFERENCES results_api(api_id) NOT NULL DEFAULT -1," +
                    "api_event_id VARCHAR(200) NOT NULL DEFAULT ''," +
                    "event_display_placements INTEGER NOT NULL DEFAULT 1," +
                    "UNIQUE (event_name, event_date) ON CONFLICT IGNORE" +
                    ");");
                queries.Add("CREATE TABLE IF NOT EXISTS distances (" +
                    "distance_id INTEGER PRIMARY KEY," +
                    "distance_name VARCHAR(100) NOT NULL," +
                    "distance_certification VARCHAR(150) NOT NULL," +
                    "event_id INTEGER NOT NULL REFERENCES events(event_id)," +
                    "distance_distance DECIMAL(10,2) DEFAULT 0.0," +
                    "distance_distance_unit INTEGER DEFAULT 0," +
                    "distance_start_location INTEGER DEFAULT -2," +
                    "distance_start_within INTEGER DEFAULT -1," +
                    "distance_finish_location INTEGER DEFAULT -1," +
                    "distance_finish_occurance INTEGER DEFAULT 1," +
                    "distance_wave INTEGER NOT NULL DEFAULT 1," +
                    "distance_start_offset_seconds INTEGER NOT NULL DEFAULT 0," +
                    "distance_start_offset_milliseconds INTEGER NOT NULL DEFAULT 0," +
                    "distance_end_offset_seconds INTEGER NOT NULL DEFAULT 0," +
                    "distance_linked_id INTEGER NOT NULL REFERENCES distances(distance_id) DEFAULT -1," +
                    "distance_type INTEGER NOT NULL DEFAULT 0," +
                    "distance_ranking_order INTEGER NOT NULL DEFAULT 0, " +
                    "distance_sms_enabled INTEGER NOT NULL DEFAULT 0, " +
                    "distance_upload_results INTEGER NOT NULL DEFAULT 1," +
                    "UNIQUE (distance_name, event_id) ON CONFLICT IGNORE" +
                    ");");
                queries.Add("CREATE TABLE IF NOT EXISTS timing_locations (" +
                    "location_id INTEGER PRIMARY KEY," +
                    "event_id INTEGER NOT NULL REFERENCES events(event_id)," +
                    "location_name VARCHAR(100) NOT NULL," +
                    "location_max_occurances INTEGER NOT NULL DEFAULT 1," +
                    "location_ignore_within INTEGER NOT NULL DEFAULT -1," +
                    "UNIQUE (event_id, location_name) ON CONFLICT IGNORE" +
                    ");");
                queries.Add("CREATE TABLE IF NOT EXISTS participants (" +
                    "participant_id INTEGER PRIMARY KEY," +
                    "participant_first VARCHAR(50) NOT NULL," +
                    "participant_last VARCHAR(75) NOT NULL," +
                    "participant_street VARCHAR(150)," +
                    "participant_city VARCHAR(75)," +
                    "participant_state VARCHAR(25)," +
                    "participant_zip VARCHAR(10)," +
                    "participant_birthday VARCHAR(15) NOT NULL," +
                    "participant_email VARCHAR(150)," +
                    "participant_phone VARCHAR(20)," +
                    "participant_mobile VARCHAR(20)," +
                    "participant_parent VARCHAR(150)," +
                    "participant_country VARCHAR(50)," +
                    "participant_street2 VARCHAR(50)," +
                    "participant_gender VARCHAR(50)," +
                    "emergencycontact_name VARCHAR(150) NOT NULL DEFAULT '911'," +
                    "emergencycontact_phone VARCHAR(20)," +
                    "UNIQUE (participant_first, participant_last, participant_street, participant_zip, participant_birthday) ON CONFLICT IGNORE" +
                    ");");
                queries.Add("CREATE TABLE IF NOT EXISTS eventspecific (" +
                    "eventspecific_id INTEGER PRIMARY KEY," +
                    "participant_id INTEGER NOT NULL REFERENCES participants(participant_id)," +
                    "event_id INTEGER NOT NULL REFERENCES events(event_id)," +
                    "distance_id INTEGER NOT NULL REFERENCES distances(distance_id)," +
                    "eventspecific_bib VARCHAR," +
                    "eventspecific_checkedin INTEGER DEFAULT 0," +
                    "eventspecific_comments VARCHAR," +
                    "eventspecific_owes VARCHAR(50)," +
                    "eventspecific_other VARCHAR," +
                    "eventspecific_registration_date VARCHAR NOT NULL DEFAULT ''," +
                    $"eventspecific_status INT NOT NULL DEFAULT {Constants.Timing.EVENTSPECIFIC_UNKNOWN}," +
                    $"eventspecific_age_group_id INT NOT NULL DEFAULT {Constants.Timing.TIMERESULT_DUMMYAGEGROUP}," +
                    "eventspecific_age_group_name VARCHAR NOT NULL DEFAULT ''," +
                    "eventspecific_anonymous SMALLINT NOT NULL DEFAULT 0," +
                    "eventspecific_sms_enabled SMALLINT NOT NULL DEFAULT 0, " +
                    "eventspecific_apparel VARCHAR NOT NULL DEFAULT '', " +
                    "eventspecific_division VARCHAR NOT NULL DEFAULT '', " +
                    $"eventspecific_version INTEGER NOT NULL DEFAULT {Constants.Timing.EVENTSPECIFIC_DEFAULT_VERSION}, " +
                    $"eventspecific_uploaded_version INTEGER NOT NULL DEFAULT {Constants.Timing.EVENTSPECIFIC_DEFAULT_UPLOADED_VERSION}, " +
                    "UNIQUE (participant_id, event_id, distance_id, eventspecific_bib) ON CONFLICT REPLACE" +
                    ");");
                queries.Add("CREATE TABLE IF NOT EXISTS segments (" +
                    "segment_id INTEGER PRIMARY KEY," +
                    "event_id INTEGER NOT NULL REFERENCES events(event_id)," +
                    "distance_id INTEGER DEFAULT -1," +
                    "location_id INTEGER DEFAULT -1," +
                    "location_occurance INTEGER DEFAULT 1," +
                    "name VARCHAR DEFAULT ''," +
                    "distance_segment DECIMAL (10,2) DEFAULT 0.0," +
                    "distance_cumulative DECIMAL (10,2) DEFAULT 0.0," +
                    "distance_unit INTEGER DEFAULT 0," +
                    "gps VARCHAR NOT NULL DEFAULT ''," +
                    "map_link VARCHAR NOT NULL DEFAULT ''," +
                    "UNIQUE (event_id, distance_id, location_id, location_occurance) ON CONFLICT IGNORE" +
                    ");");
                queries.Add("CREATE TABLE IF NOT EXISTS chipreads (" +
                    "read_id INTEGER PRIMARY KEY," +
                    "event_id INTEGER NOT NULL REFERENCES events(event_id)," +
                    "read_status INTEGER NOT NULL DEFAULT 0," +
                    "location_id INTEGER NOT NULL," +
                    "read_chipnumber VARCHAR NOT NULL," +
                    "read_seconds INTEGER NOT NULL," +
                    "read_milliseconds INTEGER NOT NULL," +
                    "read_antenna INTEGER NOT NULL," +
                    "read_reader TEXT NOT NULL," +
                    "read_box TEXT NOT NULL," +
                    "read_logindex INTEGER NOT NULL," +
                    "read_rssi INTEGER NOT NULL," +
                    "read_isrewind INTEGER NOT NULL," +
                    "read_readertime TEXT NOT NULL," +
                    "read_starttime INTEGER NOT NULL," +
                    "read_time_seconds INTEGER NOT NULL," +
                    "read_time_milliseconds INTEGER NOT NULL," +
                    "read_split_seconds INTEGER NOT NULL DEFAULT 0," +
                    "read_split_milliseconds INTEGER NOT NULL DEFAULT 0," +
                    $"read_bib VARCHAR NOT NULL DEFAULT '{Constants.Timing.CHIPREAD_DUMMYBIB}'," +
                    $"read_type INTEGER NOT NULL DEFAULT '{Constants.Timing.CHIPREAD_TYPE_CHIP}'," +
                    "UNIQUE (event_id, read_chipnumber, read_bib, read_seconds, read_milliseconds) ON CONFLICT IGNORE" +
                    ");");
                queries.Add("CREATE TABLE IF NOT EXISTS time_results (" +
                    "event_id INTEGER NOT NULL REFERENCES events(event_id)," +
                    "eventspecific_id INTEGER NOT NULL REFERENCES eventspecific(eventspecific_id)," +
                    "read_id INTEGER," +
                    "location_id INTEGER NOT NULL," +
                    $"segment_id INTEGER NOT NULL DEFAULT {Constants.Timing.SEGMENT_NONE}," +
                    "timeresult_occurance INTEGER NOT NULL," +
                    "timeresult_time TEXT NOT NULL," +
                    "timeresult_splittime TEXT NOT NULL DEFAULT ''," +
                    "timeresult_chiptime TEXT NOT NULL," +
                    "timeresult_unknown_id TEXT NOT NULL DEFAULT ''," +
                    $"timeresult_place INT NOT NULL DEFAULT {Constants.Timing.TIMERESULT_DUMMYPLACE}," +
                    $"timeresult_age_place INT NOT NULL DEFAULT {Constants.Timing.TIMERESULT_DUMMYPLACE}," +
                    $"timeresult_gender_place INT NOT NULL DEFAULT {Constants.Timing.TIMERESULT_DUMMYPLACE}," +
                    $"timeresult_division_place INT NOT NULL DEFAULT {Constants.Timing.TIMERESULT_DUMMYPLACE}," +
                    $"timeresult_status INT NOT NULL DEFAULT {Constants.Timing.CHIPREAD_STATUS_NONE}," +
                    $"timeresult_uploaded INT NOT NULL DEFAULT {Constants.Timing.TIMERESULT_UPLOADED_FALSE}," +
                    "UNIQUE (event_id, eventspecific_id, location_id, timeresult_occurance, timeresult_unknown_id) ON CONFLICT REPLACE" +
                    ");");
                queries.Add("INSERT INTO participants (participant_id, participant_first, participant_last," +
                    " participant_birthday) VALUES (0, 'J', 'Doe', '01/01/1901');");
                queries.Add("CREATE TABLE IF NOT EXISTS settings (" +
                    "setting VARCHAR NOT NULL," +
                    "value VARCHAR NOT NULL," +
                    "UNIQUE (setting) ON CONFLICT REPLACE" +
                    ");" +
                    "INSERT INTO settings (setting, value) VALUES " +
                    $"('{Constants.Settings.DATABASE_VERSION}','{version}');");
                queries.Add("CREATE TABLE IF NOT EXISTS age_groups (" +
                    "group_id INTEGER PRIMARY KEY," +
                    "event_id INTEGER NOT NULL REFERENCES events(event_id)," +
                    "distance_id INTEGER NOT NULL DEFAULT -1," +
                    "start_age INTEGER NOT NULL," +
                    "end_age INTEGER NOT NULL," +
                    "custom_name VARCHAR NOT NULL DEFAULT ''," +
                    $"last_group INTEGER DEFAULT {Constants.Timing.AGEGROUPS_LASTGROUP_FALSE} NOT NULL);");
                queries.Add("CREATE TABLE IF NOT EXISTS timing_systems (" +
                    "ts_identifier INTEGER PRIMARY KEY," +
                    "ts_ip TEXT NOT NULL," +
                    "ts_port INTEGER NOT NULL," +
                    "ts_location INTEGER NOT NULL," +
                    "ts_type TEXT NOT NULL," +
                    "UNIQUE (ts_ip, ts_location) ON CONFLICT REPLACE);");
                queries.Add("CREATE TABLE IF NOT EXISTS alarms (" +
                    "alarm_id INTEGER PRIMARY KEY ON CONFLICT REPLACE, " +
                    "event_id INTEGER NOT NULL REFERENCES events(event_id), " +
                    "alarm_bib VARCHAR, " +
                    "alarm_chip VARCHAR, " +
                    "alarm_enabled INTEGER NOT NULL DEFAULT 0," +
                    "alarm_sound INTEGER NOT NULL DEFAULT 0, " +
                    "UNIQUE (event_id, alarm_bib, alarm_chip) ON CONFLICT REPLACE" +
                    ");");
                queries.Add("CREATE TABLE IF NOT EXISTS remote_readers(" +
                    "event_id INTEGER NOT NULL REFERENCES events(event_id), " +
                    "api_id INTEGER NOT NULL REFERENCES results_api(api_id), " +
                    "location_id INTEGER NOT NULL DEFAULT -1, " +
                    "reader_name VARCHAR NOT NULL, " +
                    "UNIQUE(event_id, api_id, reader_name) ON CONFLICT REPLACE" +
                    ");");
                queries.Add("CREATE TABLE IF NOT EXISTS sms_alert(" +
                    "event_id INTEGER NOT NULL REFERENCES events(event_id), " +
                    "eventspecific_id INTEGER NOT NULL REFERENCES eventspecific(eventspecific_id)," +
                    $"segment_id INTEGER NOT NULL DEFAULT '{Constants.Timing.SEGMENT_FINISH}'," +
                    "UNIQUE(event_id, eventspecific_id, segment_id)" +
                    ");");
                queries.Add("CREATE TABLE IF NOT EXISTS sms_ban_list(" +
                    "banned_phone VARCHAR(100), " +
                    "UNIQUE (banned_phone)" +
                    ");");
                queries.Add("CREATE TABLE IF NOT EXISTS email_ban_list(" +
                    "banned_email VARCHAR(100), " +
                    "UNIQUE(banned_email)" +
                    ");");
                queries.Add("CREATE TABLE IF NOT EXISTS email_alert(" +
                    "event_id INTEGER NOT NULL REFERENCES events(event_id), " +
                    "eventspecific_id INTEGER NOT NULL REFERENCES eventspecific(eventspecific_id)" +
                    ");");
                queries.Add("CREATE TABLE IF NOT EXISTS sms_subscriptions(" +
                    "event_id INTEGER NOT NULL REFERENCES events(event_id), " +
                    "bib VARCHAR(100) NOT NULL DEFAULT '', " +
                    "first VARCHAR(100) NOT NULL DEFAULT '', " +
                    "last VARCHAR(100) NOT NULL DEFAULT '', " +
                    "phone VARCHAR(100) NOT NULL DEFAULT '', " +
                    "UNIQUE(event_id, bib, first, last, phone)" +
                    ");");
                queries.Add("CREATE TABLE IF NOT EXISTS chronoclocks(" +
                    "clock_id INTEGER PRIMARY KEY ON CONFLICT REPLACE, " +
                    "name VARCHAR(100) NOT NULL DEFAULT '', " +
                    "url VARCHAR(100) NOT NULL DEFAULT '', " +
                    "enabled INTEGER NOT NULL DEFAULT 0, " +
                    "UNIQUE(name) ON CONFLICT IGNORE, UNIQUE(url) ON CONFLICT IGNORE" +
                    ");");
                queries.Add("CREATE INDEX idx_eventspecific_bibs ON eventspecific(eventspecific_bib);");
                queries.Add("CREATE INDEX idx_event_id ON events(event_id);");
                queries.Add("CREATE INDEX idx_segment_id ON segments(segment_id);");
                queries.Add("CREATE INDEX idx_location_id ON timing_locations(location_id);");
                queries.Add("CREATE INDEX idx_chipread_id ON chipreads(read_id);");
                queries.Add("CREATE INDEX idx_participant_id ON participants(participant_id);");
                queries.Add("CREATE INDEX idx_distance_id ON distances(distance_id);");

                string gitVersion;
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Chronokeep.version.txt")!)
                {
                    using StreamReader fileReader = new(stream);
                    gitVersion = fileReader.ReadToEnd();
                }
                if (gitVersion.Contains('-'))
                {
                    gitVersion = gitVersion.Split('-')[0];
                }
                queries.Add("INSERT INTO settings (setting, value) VALUES " +
                    $"('{Constants.Settings.PROGRAM_VERSION}','{gitVersion}');");

                using (SQLiteTransaction? transaction = connection.BeginTransaction())
                {
                    int counter = 1;
                    foreach (string q in queries)
                    {
                        Log.D("SQLite.Setup", $"Table query number {counter} Query string is: {q}");
                        counter++;
                        command = new SQLiteCommand(q, connection);
                        command.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }

                command = new SQLiteCommand($"SELECT value FROM settings WHERE setting='{Constants.Settings.DATABASE_VERSION}';", connection);
                using SQLiteDataReader versionChecker = command.ExecuteReader();
                if (versionChecker.Read())
                {
                    oldVersion = Convert.ToInt32(versionChecker["value"]);
                }
                else
                {
                    Log.D("SQLite.Setup", "Something went wrong when checking the version...");
                }
                versionChecker.Close();
            }
            reader.Close();
            AppSetting? dbSetting = Settings.GetAppSetting(Constants.Settings.MINIMUM_COMPATIBLE_DATABASE, connection);
            int maxVers = dbSetting != null ? Convert.ToInt32(dbSetting.Value) : -1;
            connection.Close();
            if (oldVersion == -1) Log.D("SQLite.Setup", "Unable to get a version number. Something is terribly wrong.");
            else if (oldVersion < version) Update.UpdateDatabase(oldVersion, connectionInfo);
            // Check if the db version is greater than the minimum required.
            else if (maxVers > version)
            {
                Update.UpdateClient(version, maxVers);
            }
        }
    }
}