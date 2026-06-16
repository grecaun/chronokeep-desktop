using Chronokeep.Objects;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Chronokeep.Database.SQLite
{
    class Events
    {
        internal static int AddEvent(Event anEvent, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = "INSERT INTO events(event_name, event_date, event_yearcode, event_rank_by_gun, " +
                "event_common_age_groups, event_common_start_finish, event_distance_specific_segments, " +
                "event_start_time_seconds, event_start_time_milliseconds, event_finish_max_occurances, event_finish_ignore_within, " +
                "event_start_window, event_type, event_display_placements, event_age_groups_as_divisions, event_days_allowed, event_upload_specific_distance_results, " +
                "event_start_max_occurrences)" +
                " VALUES(@name,@date,@yearcode,@gun,@age,@start,@sepseg,@startsec,@startmill,@occ,@ign,@window," +
                "@type,@display,@agDiv,@daysAllowed,@uploadSpecific,@startOcc)";
            command.Parameters.AddRange([
                new("@name", anEvent.Name),
                new("@date", anEvent.Date),
                new("@yearcode", anEvent.YearCode),
                new("@gun", anEvent.RankByGun),
                new("@age", anEvent.CommonAgeGroups),
                new("@start", anEvent.CommonStartFinish),
                new("@sepseg", anEvent.DistanceSpecificSegments),
                new("@startsec", anEvent.StartSeconds),
                new("@startmill", anEvent.StartMilliseconds),
                new("@occ", anEvent.FinishMaxOccurrences),
                new("@ign", anEvent.FinishIgnoreWithin),
                new("@window", anEvent.StartWindow),
                new("@type", anEvent.EventType),
                new("@display", anEvent.DisplayPlacements),
                new("@agDiv", anEvent.DivisionsEnabled),
                new("@daysAllowed", anEvent.DaysAllowed),
                new("@uploadSpecific", anEvent.UploadSpecific ? 1 : 0),
                new("@startOcc", anEvent.StartMaxOccurrences),
            ]);
            command.ExecuteNonQuery();
            long outVal = connection.LastInsertRowId;
            return (int)outVal;
        }

        internal static void RemoveEvent(int identifier, SQLiteConnection connection)
        {
            using var transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = "DELETE FROM sms_subscriptions WHERE event_id=@event;" +
                "DELETE FROM email_alert WHERE event_id=@event;" +
                "DELETE FROM sms_alert WHERE event_id=@event;" +
                "DELETE FROM remote_readers WHERE event_id=@event;" +
                "DELETE FROM alarms WHERE event_id=@event;" +
                "DELETE FROM time_results WHERE event_id=@event;" +
                "DELETE FROM bib_chip_assoc WHERE event_id=@event;" +
                "DELETE FROM segments WHERE event_id=@event;" +
                "DELETE FROM chipreads WHERE event_id=@event;" +
                "DELETE FROM age_groups WHERE event_id=@event;" +
                "DELETE FROM distances WHERE event_id=@event;" +
                "DELETE FROM timing_locations WHERE event_id=@event;" +
                "DELETE FROM eventspecific WHERE event_id=@event;" +
                "DELETE FROM events WHERE event_id=@event;";
            command.Parameters.AddRange([
                new("@event", identifier) ]);
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        internal static void UpdateEvent(Event anEvent, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = "UPDATE events SET " +
                "event_name=@name," +
                "event_date=@date," +
                "event_yearcode=@yearcode," +
                "event_common_age_groups=@age," +
                "event_common_start_finish=@start," +
                "event_rank_by_gun=@gun," +
                "event_distance_specific_segments=@seg," +
                "event_start_time_seconds=@startsec," +
                "event_start_time_milliseconds=@startmill," +
                "event_type=@type," +
                "event_finish_max_occurances=@maxocc," +
                "event_finish_ignore_within=@ignore," +
                "event_start_window=@startWindow," +
                "api_id=@apiid," +
                "api_event_id=@apieventid," +
                "event_display_placements=@display," +
                "event_age_groups_as_divisions=@agDiv," +
                "event_days_allowed=@daysAllowed," +
                "event_upload_specific_distance_results=@uploadSpecific," +
                "event_start_max_occurrences=@startOcc" +
                " WHERE event_id=@id";
            command.Parameters.AddRange([
                new("@id", anEvent.Identifier),
                new("@name", anEvent.Name),
                new("@date", anEvent.Date),
                new("@yearcode", anEvent.YearCode),
                new("@age", anEvent.CommonAgeGroups),
                new("@start", anEvent.CommonStartFinish),
                new("@gun", anEvent.RankByGun),
                new("@seg", anEvent.DistanceSpecificSegments),
                new("@startsec", anEvent.StartSeconds),
                new("@startmill", anEvent.StartMilliseconds),
                new("@type", anEvent.EventType),
                new("@maxocc", anEvent.FinishMaxOccurrences),
                new("@ignore", anEvent.FinishIgnoreWithin),
                new("@startWindow", anEvent.StartWindow),
                new("@apiid", anEvent.ApiId),
                new("@apieventid", anEvent.ApiEventId),
                new("@display", anEvent.DisplayPlacements),
                new("@agDiv", anEvent.DivisionsEnabled),
                new("@daysAllowed", anEvent.DaysAllowed),
                new("@uploadSpecific", anEvent.UploadSpecific ? 1 : 0),
                new("@startOcc", anEvent.StartMaxOccurrences),
            ]);
            command.ExecuteNonQuery();
        }

        internal static List<Event> GetEvents(SQLiteConnection connection)
        {

            List<Event> output = [];
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM events";
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                output.Add(new(Convert.ToInt32(reader["event_id"]),
                    reader["event_name"].ToString()!,
                    reader["event_date"].ToString()!,
                    Convert.ToInt32(reader["event_common_age_groups"]),
                    Convert.ToInt32(reader["event_common_start_finish"]),
                    Convert.ToInt32(reader["event_distance_specific_segments"]),
                    Convert.ToInt32(reader["event_rank_by_gun"]),
                    reader["event_yearcode"].ToString()!,
                    Convert.ToInt32(reader["event_finish_max_occurances"]),
                    Convert.ToInt32(reader["event_finish_ignore_within"]),
                    Convert.ToInt32(reader["event_start_window"]),
                    Convert.ToInt64(reader["event_start_time_seconds"]),
                    Convert.ToInt32(reader["event_start_time_milliseconds"]),
                    Convert.ToInt32(reader["event_type"]),
                    Convert.ToInt32(reader["api_id"]),
                    reader["api_event_id"].ToString()!,
                    Convert.ToInt32(reader["event_display_placements"]),
                    Convert.ToInt32(reader["event_age_groups_as_divisions"]),
                    Convert.ToInt32(reader["event_days_allowed"]),
                    Convert.ToInt32(reader["event_upload_specific_distance_results"]),
                    Convert.ToInt32(reader["event_start_max_occurrences"])
                    ));
            }
            reader.Close();
            return output;
        }

        internal static int GetEventID(Event anEvent, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT event_id FROM events WHERE event_name=@name AND event_date=@date";
            command.Parameters.AddRange(
            [
                new("@name", anEvent.Name),
                new("@date", anEvent.Date)
            ]);
            SQLiteDataReader reader = command.ExecuteReader();
            int output = -1;
            if (reader.Read())
            {
                output = Convert.ToInt32(reader["event_id"]);
            }
            reader.Close();
            return output;
        }

        internal static Event? GetEvent(int id, SQLiteConnection connection)
        {
            if (id < 0)
            {
                return null;
            }
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM events WHERE event_id=@id";
            command.Parameters.Add(new("@id", id));
            SQLiteDataReader reader = command.ExecuteReader();
            Event? output = null;
            if (reader.Read())
            {
                output = new(Convert.ToInt32(reader["event_id"]),
                    reader["event_name"].ToString()!,
                    reader["event_date"].ToString()!,
                    Convert.ToInt32(reader["event_common_age_groups"]),
                    Convert.ToInt32(reader["event_common_start_finish"]),
                    Convert.ToInt32(reader["event_distance_specific_segments"]),
                    Convert.ToInt32(reader["event_rank_by_gun"]),
                    reader["event_yearcode"].ToString()!,
                    Convert.ToInt32(reader["event_finish_max_occurances"]),
                    Convert.ToInt32(reader["event_finish_ignore_within"]),
                    Convert.ToInt32(reader["event_start_window"]),
                    Convert.ToInt64(reader["event_start_time_seconds"]),
                    Convert.ToInt32(reader["event_start_time_milliseconds"]),
                    Convert.ToInt32(reader["event_type"]),
                    Convert.ToInt32(reader["api_id"]),
                    reader["api_event_id"].ToString()!,
                    Convert.ToInt32(reader["event_display_placements"]),
                    Convert.ToInt32(reader["event_age_groups_as_divisions"]),
                    Convert.ToInt32(reader["event_days_allowed"]),
                    Convert.ToInt32(reader["event_upload_specific_distance_results"]),
                    Convert.ToInt32(reader["event_start_max_occurrences"])
                    );
            }
            reader.Close();
            return output;
        }

        internal static void SetStartOptions(Event anEvent, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE events SET event_start_window=@window, event_start_max_occurrences=@startOcc WHERE event_id=@event;";
            command.Parameters.AddRange(
            [
                new("@window", anEvent.StartWindow),
                new("@event", anEvent.Identifier),
                new("@startOcc", anEvent.StartMaxOccurrences),
            ]);
            command.ExecuteNonQuery();
        }

        internal static void SetFinishOptions(Event anEvent, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE events SET event_finish_max_occurances=@occ, event_finish_ignore_within=@ignore WHERE event_id=@event;";
            command.Parameters.AddRange(
            [
                new("@occ", anEvent.FinishMaxOccurrences),
                new("@ignore", anEvent.FinishIgnoreWithin),
                new("@event", anEvent.Identifier)
            ]);
            command.ExecuteNonQuery();
        }

        internal static void SetStartFinishOptions(Event anEvent, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE events SET event_start_window=@window, event_start_max_occurrences=@startOcc, event_finish_max_occurances=@occ, event_finish_ignore_within=@ignore WHERE event_id=@event;";
            command.Parameters.AddRange(
            [
                new("@window", anEvent.StartWindow),
                new("@startOcc", anEvent.StartMaxOccurrences),
                new("@occ", anEvent.FinishMaxOccurrences),
                new("@ignore", anEvent.FinishIgnoreWithin),
                new("@event", anEvent.Identifier)
            ]);
            command.ExecuteNonQuery();
        }
    }
}
