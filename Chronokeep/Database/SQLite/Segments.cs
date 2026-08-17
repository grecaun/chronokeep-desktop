/*
Chronokeep Desktop - Race Scoring Software
Copyright (C) 2026 James Sentinella

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using Chronokeep.Objects;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Chronokeep.Database.SQLite
{
    internal static class Segments
    {
        internal static int AddSegment(Segment seg, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = "INSERT INTO segments (event_id, distance_id, location_id, location_occurance, name, distance_segment, " +
                "distance_cumulative, distance_unit, gps, map_link) " +
                "VALUES (@event,@distance,@location,@occurance,@name,@dseg,@dcum,@dunit,@gps,@map)";
            command.Parameters.AddRange([
                new SQLiteParameter("@event",seg.EventId),
                new SQLiteParameter("@distance",seg.DistanceId),
                new SQLiteParameter("@location",seg.LocationId),
                new SQLiteParameter("@occurance",seg.Occurrence),
                new SQLiteParameter("@name",seg.Name),
                new SQLiteParameter("@dseg",seg.SegmentDistance),
                new SQLiteParameter("@dcum",seg.CumulativeDistance),
                new SQLiteParameter("@dunit",seg.DistanceUnit),
                new SQLiteParameter("@gps",seg.Gps),
                new SQLiteParameter("@map",seg.MapLink)
            ]);
            command.ExecuteNonQuery();
            command.CommandText = "SELECT segment_id FROM segments " +
                "WHERE event_id=@event " +
                "AND distance_id=@distance " +
                "AND location_id=@location " +
                "AND location_occurance=@occurance;";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@event",seg.EventId),
                new SQLiteParameter("@distance",seg.DistanceId),
                new SQLiteParameter("@location",seg.LocationId),
                new SQLiteParameter("@occurance",seg.Occurrence),
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
                    new SQLiteParameter("@id", identifier) ]);
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
                new SQLiteParameter("@event",seg.EventId),
                new SQLiteParameter("@distance",seg.DistanceId),
                new SQLiteParameter("@location",seg.LocationId),
                new SQLiteParameter("@occurance",seg.Occurrence),
                new SQLiteParameter("@name",seg.Name),
                new SQLiteParameter("@dseg",seg.SegmentDistance),
                new SQLiteParameter("@dcum",seg.CumulativeDistance),
                new SQLiteParameter("@dunit",seg.DistanceUnit),
                new SQLiteParameter("@id",seg.Identifier),
                new SQLiteParameter("@gps",seg.Gps),
                new SQLiteParameter("@map",seg.MapLink)
            ]);
            command.ExecuteNonQuery();
        }

        internal static int GetSegmentId(Segment seg, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM segments WHERE event_id=@event, distance_id=@distance, location_id=@location, occurance=@occurance;";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@event",seg.EventId),
                new SQLiteParameter("@distance",seg.DistanceId),
                new SQLiteParameter("@location",seg.LocationId),
                new SQLiteParameter("@occurance",seg.Occurrence),
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
                new SQLiteParameter("@event",eventId), ]);
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
            using SQLiteTransaction? transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = "DELETE FROM segments WHERE event_id=@id";
            command.Parameters.AddRange([
                new SQLiteParameter("@id", eventId) ]);
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        internal static int GetMaxSegments(int eventId, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT MAX(seg_count) max_segments FROM" +
                " (SELECT COUNT(segment_id) seg_count, distance_id FROM segments" +
                " WHERE event_id=@event GROUP BY distance_id);";
            command.Parameters.Add(new SQLiteParameter("@event", eventId));
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

