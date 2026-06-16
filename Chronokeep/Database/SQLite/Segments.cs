using Chronokeep.Objects;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Chronokeep.Database.SQLite
{
    class Segments
    {
        internal static int AddSegment(Segment seg, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = "INSERT INTO segments (event_id, distance_id, location_id, location_occurance, name, distance_segment, " +
                "distance_cumulative, distance_unit, gps, map_link) " +
                "VALUES (@event,@distance,@location,@occurance,@name,@dseg,@dcum,@dunit,@gps,@map)";
            command.Parameters.AddRange([
                new("@event",seg.EventId),
                new("@distance",seg.DistanceId),
                new("@location",seg.LocationId),
                new("@occurance",seg.Occurrence),
                new("@name",seg.Name),
                new("@dseg",seg.SegmentDistance),
                new("@dcum",seg.CumulativeDistance),
                new("@dunit",seg.DistanceUnit),
                new("@gps",seg.Gps),
                new("@map",seg.MapLink)
            ]);
            command.ExecuteNonQuery();
            command.CommandText = "SELECT segment_id FROM segments " +
                "WHERE event_id=@event " +
                "AND distance_id=@distance " +
                "AND location_id=@location " +
                "AND location_occurance=@occurance;";
            command.Parameters.AddRange(
            [
                new("@event",seg.EventId),
                new("@distance",seg.DistanceId),
                new("@location",seg.LocationId),
                new("@occurance",seg.Occurrence),
            ]);
            SQLiteDataReader reader = command.ExecuteReader();
            int outVal = -1;
            if (reader.Read())
            {
                outVal = Convert.ToInt32(reader["segment_id"]);
            }
            reader.Close();
            return outVal;
        }
        internal static void RemoveSegment(int identifier, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = "DELETE FROM segments WHERE segment_id=@id";
            command.Parameters.AddRange([
                    new("@id", identifier) ]);
            command.ExecuteNonQuery();
        }

        internal static void UpdateSegment(Segment seg, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = "UPDATE segments SET event_id=@event, distance_id=@distance, location_id=@location, " +
                "location_occurance=@occurance, name=@name, distance_segment=@dseg, distance_cumulative=@dcum, distance_unit=@dunit, gps=@gps, map_link=@map " +
                "WHERE segment_id=@id";
            command.Parameters.AddRange([
                new("@event",seg.EventId),
                new("@distance",seg.DistanceId),
                new("@location",seg.LocationId),
                new("@occurance",seg.Occurrence),
                new("@name",seg.Name),
                new("@dseg",seg.SegmentDistance),
                new("@dcum",seg.CumulativeDistance),
                new("@dunit",seg.DistanceUnit),
                new("@id",seg.Identifier),
                new("@gps",seg.Gps),
                new("@map",seg.MapLink)
            ]);
            command.ExecuteNonQuery();
        }

        internal static int GetSegmentId(Segment seg, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM segments WHERE event_id=@event, distance_id=@distance, location_id=@location, occurance=@occurance;";
            command.Parameters.AddRange(
            [
                new("@event",seg.EventId),
                new("@distance",seg.DistanceId),
                new("@location",seg.LocationId),
                new("@occurance",seg.Occurrence),
            ]);
            SQLiteDataReader reader = command.ExecuteReader();
            int output = -1;
            if (reader.Read())
            {
                output = Convert.ToInt32(reader["segment_id"]);
            }
            reader.Close();
            return output;
        }

        internal static List<Segment> GetSegments(int eventId, SQLiteConnection connection)
        {
            List<Segment> output = [];
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM segments WHERE event_id=@event";
            command.Parameters.AddRange([
                new("@event",eventId), ]);
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                output.Add(new Segment(
                    Convert.ToInt32(reader["segment_id"]),
                    Convert.ToInt32(reader["event_id"]),
                    Convert.ToInt32(reader["distance_id"]),
                    Convert.ToInt32(reader["location_id"]),
                    Convert.ToInt32(reader["location_occurance"]),
                    Convert.ToDouble(reader["distance_segment"]),
                    Convert.ToDouble(reader["distance_cumulative"]),
                    Convert.ToInt32(reader["distance_unit"]),
                    reader["name"].ToString()!,
                    reader["gps"].ToString()!,
                    reader["map_link"].ToString()!
                    ));
            }
            reader.Close();
            return output;
        }

        internal static void ResetSegments(int eventId, SQLiteConnection connection)
        {
            using var transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = "DELETE FROM segments WHERE event_id=@id";
            command.Parameters.AddRange([
                new("@id", eventId) ]);
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        internal static int GetMaxSegments(int eventId, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT MAX(seg_count) max_segments FROM" +
                " (SELECT COUNT(segment_id) seg_count, distance_id FROM segments" +
                " WHERE event_id=@event GROUP BY distance_id);";
            command.Parameters.Add(new("@event", eventId));
            SQLiteDataReader reader = command.ExecuteReader();
            int output = 0;
            if (reader.Read() && reader["max_segments"] != DBNull.Value)
            {
                output = Convert.ToInt32(reader["max_segments"]);
            }
            reader.Close();
            return output;
        }
    }
}
