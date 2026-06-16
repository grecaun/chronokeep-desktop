using Chronokeep.Helpers;
using Chronokeep.Objects;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Chronokeep.Database.SQLite
{
    class Distances
    {

        internal static int AddDistance(Distance d, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = "INSERT INTO distances (distance_name, event_id, distance_distance, distance_distance_unit," +
                "distance_start_location, distance_start_within, distance_finish_location, distance_finish_occurance, distance_wave, " +
                "distance_start_offset_seconds, distance_start_offset_milliseconds, distance_end_offset_seconds, " +
                "distance_linked_id, distance_type, distance_ranking_order, distance_sms_enabled, distance_upload_results, distance_certification) " +
                "values (@name,@event_id,@distance,@unit,@startloc,@startwithin,@finishloc,@finishocc,@wave,@soffsec,@soffmill,@endSec,@linked,@type,@rank,@sms,@upload,@cert)";
            command.Parameters.AddRange([
                new("@name", d.Name),
                new("@event_id", d.EventIdentifier),
                new("@distance", d.DistanceValue),
                new("@unit", d.DistanceUnit),
                new("@startloc", d.StartLocation),
                new("@startwithin", d.StartWithin),
                new("@finishloc", d.FinishLocation),
                new("@finishocc", d.FinishOccurrence),
                new("@wave", d.Wave),
                new("@soffsec", d.StartOffsetSeconds),
                new("@soffmill", d.StartOffsetMilliseconds),
                new("@endSec", d.EndSeconds),
                new("@linked", d.LinkedDistance),
                new("@type", d.Type),
                new("@rank", d.Ranking),
                new("@sms", d.SmsEnabled ? 1 : 0),
                new("@upload", d.Upload ? 1 : 0),
                new("@cert", d.Certification),
            ]);
            Log.D("Database.SQLite.Distances", "SQL query: '" + command.CommandText + "'");
            command.ExecuteNonQuery();
            command.CommandText = "SELECT distance_id FROM distances " +
                "WHERE event_id=@event_id " +
                "AND distance_name=@name;";
            command.Parameters.AddRange(
            [
                new("@event_id", d.EventIdentifier),
                new("@name", d.Name)
            ]);
            SQLiteDataReader reader = command.ExecuteReader();
            int outVal = -1;
            if (reader.Read())
            {
                outVal = Convert.ToInt32(reader["distance_id"]);
            }
            reader.Close();
            return outVal;
        }

        internal static void RemoveDistance(int identifier, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = "DELETE FROM segments WHERE distance_id=@id; DELETE FROM eventspecific WHERE distance_id=@id; DELETE FROM age_groups WHERE distance_id=@id; DELETE FROM distances WHERE distance_id=@id";
            command.Parameters.AddRange([
                new("@id", identifier) ]);
            command.ExecuteNonQuery();
        }

        internal static void UpdateDistance(Distance d, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = "UPDATE distances SET distance_name=@name, event_id=@event, distance_distance=@distance," +
                "distance_distance_unit=@unit, distance_start_location=@startloc, distance_start_within=@within, distance_finish_location=@finishloc," +
                "distance_finish_occurance=@occurance, distance_wave=@wave, distance_start_offset_seconds=@soffsec, " +
                "distance_start_offset_milliseconds=@soffmill, distance_end_offset_seconds=@endSec, " +
                "distance_linked_id=@linked, distance_type=@type, distance_ranking_order=@rank, distance_sms_enabled=@sms, " +
                "distance_upload_results=@upload, distance_certification=@cert " +
                "WHERE distance_id=@id";
            command.Parameters.AddRange([
                new("@name", d.Name),
                new("@event", d.EventIdentifier),
                new("@distance", d.DistanceValue),
                new("@unit", d.DistanceUnit),
                new("@startloc", d.StartLocation),
                new("@within", d.StartWithin),
                new("@finishloc", d.FinishLocation),
                new("@occurance", d.FinishOccurrence),
                new("@wave", d.Wave),
                new("@soffsec", d.StartOffsetSeconds),
                new("@soffmill", d.StartOffsetMilliseconds),
                new("@id", d.Identifier),
                new("@endSec", d.EndSeconds),
                new("@linked", d.LinkedDistance),
                new("@type", d.Type),
                new("@rank", d.Ranking),
                new("@sms", d.SmsEnabled ? 1 : 0),
                new("@upload", d.Upload ? 1 : 0),
                new("@cert", d.Certification),
            ]);
            command.ExecuteNonQuery();
        }

        internal static List<Distance> GetDistances(SQLiteConnection connection)
        {
            List<Distance> output = [];
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM distances";
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                output.Add(new(Convert.ToInt32(reader["distance_id"]),
                    reader["distance_name"].ToString()!,
                    Convert.ToInt32(reader["event_id"]),
                    Convert.ToDouble(reader["distance_distance"]),
                    Convert.ToInt32(reader["distance_distance_unit"]),
                    Convert.ToInt32(reader["distance_finish_location"]),
                    Convert.ToInt32(reader["distance_finish_occurance"]),
                    Convert.ToInt32(reader["distance_start_location"]),
                    Convert.ToInt32(reader["distance_start_within"]),
                    Convert.ToInt32(reader["distance_wave"]),
                    Convert.ToInt32(reader["distance_start_offset_seconds"]),
                    Convert.ToInt32(reader["distance_start_offset_milliseconds"]),
                    Convert.ToInt32(reader["distance_end_offset_seconds"]),
                    Convert.ToInt32(reader["distance_linked_id"]),
                    Convert.ToInt32(reader["distance_type"]),
                    Convert.ToInt32(reader["distance_ranking_order"]),
                    Convert.ToInt32(reader["distance_sms_enabled"]) != 0,
                    Convert.ToInt32(reader["distance_upload_results"]) != 0,
                    reader["distance_certification"].ToString()!
                    ));
            }
            reader.Close();
            return output;
        }

        internal static List<Distance> GetDistances(int eventId, SQLiteConnection connection)
        {
            List<Distance> output = [];
            if (eventId <= 0)
            {
                return output;
            }
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM distances WHERE event_id = " + eventId;
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                output.Add(new(Convert.ToInt32(reader["distance_id"]),
                    reader["distance_name"].ToString()!,
                    Convert.ToInt32(reader["event_id"]),
                    Convert.ToDouble(reader["distance_distance"]),
                    Convert.ToInt32(reader["distance_distance_unit"]),
                    Convert.ToInt32(reader["distance_finish_location"]),
                    Convert.ToInt32(reader["distance_finish_occurance"]),
                    Convert.ToInt32(reader["distance_start_location"]),
                    Convert.ToInt32(reader["distance_start_within"]),
                    Convert.ToInt32(reader["distance_wave"]),
                    Convert.ToInt32(reader["distance_start_offset_seconds"]),
                    Convert.ToInt32(reader["distance_start_offset_milliseconds"]),
                    Convert.ToInt32(reader["distance_end_offset_seconds"]),
                    Convert.ToInt32(reader["distance_linked_id"]),
                    Convert.ToInt32(reader["distance_type"]),
                    Convert.ToInt32(reader["distance_ranking_order"]),
                    Convert.ToInt32(reader["distance_sms_enabled"]) != 0,
                    Convert.ToInt32(reader["distance_upload_results"]) != 0,
                    reader["distance_certification"].ToString()!
                    ));
            }
            reader.Close();
            return output;
        }

        internal static int GetDistanceID(Distance d, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT distance_id FROM distances WHERE distance_name=@name AND event_id=@eventid";
            command.Parameters.AddRange(
            [
                new("@name", d.Name),
                new("@eventid", d.EventIdentifier)
            ]);
            SQLiteDataReader reader = command.ExecuteReader();
            int output = -1;
            if (reader.Read())
            {
                output = Convert.ToInt32(reader["distance_id"]);
            }
            reader.Close();
            return output;
        }

        internal static Distance? GetDistance(int distanceId, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM distances WHERE distance_id=@div";
            command.Parameters.AddRange(
            [
                new("@div", distanceId)
            ]);
            SQLiteDataReader reader = command.ExecuteReader();
            Distance? output = null;
            if (reader.Read())
            {
                output = new(Convert.ToInt32(reader["distance_id"]),
                    reader["distance_name"].ToString()!,
                    Convert.ToInt32(reader["event_id"]),
                    Convert.ToDouble(reader["distance_distance"]),
                    Convert.ToInt32(reader["distance_distance_unit"]),
                    Convert.ToInt32(reader["distance_finish_location"]),
                    Convert.ToInt32(reader["distance_finish_occurance"]),
                    Convert.ToInt32(reader["distance_start_location"]),
                    Convert.ToInt32(reader["distance_start_within"]),
                    Convert.ToInt32(reader["distance_wave"]),
                    Convert.ToInt32(reader["distance_start_offset_seconds"]),
                    Convert.ToInt32(reader["distance_start_offset_milliseconds"]),
                    Convert.ToInt32(reader["distance_end_offset_seconds"]),
                    Convert.ToInt32(reader["distance_linked_id"]),
                    Convert.ToInt32(reader["distance_type"]),
                    Convert.ToInt32(reader["distance_ranking_order"]),
                    Convert.ToInt32(reader["distance_sms_enabled"]) != 0,
                    Convert.ToInt32(reader["distance_upload_results"]) != 0,
                    reader["distance_certification"].ToString()!
                    );
            }
            reader.Close();
            return output;
        }

        internal static void SetWaveTimes(int eventId, int wave, long seconds, int milliseconds, SQLiteConnection connection)
        {
            using var transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE distances SET distance_start_offset_seconds=@seconds," +
                " distance_start_offset_milliseconds=@milli WHERE event_id=@event AND distance_wave=@wave;";
            command.Parameters.AddRange(
            [
                new("@event", eventId),
                    new("@wave", wave),
                    new("@seconds", seconds),
                    new("@milli", milliseconds)
            ]);
            command.ExecuteNonQuery();
            transaction.Commit();
        }
    }
}
