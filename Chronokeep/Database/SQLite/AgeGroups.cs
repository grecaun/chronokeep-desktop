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
    internal static class AgeGroups
    {
        internal static int AddAgeGroup(AgeGroup group, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO age_groups (event_id, distance_id, start_age, end_age, custom_name)" +
                " VALUES (@event, @distance, @start, @end, @custom);";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@event", group.EventId),
                new SQLiteParameter("@distance", group.DistanceId),
                new SQLiteParameter("@start", group.StartAge),
                new SQLiteParameter("@end", group.EndAge),
                new SQLiteParameter("@custom", group.CustomName)
            ]);
            command.ExecuteNonQuery();
            long outVal = connection.LastInsertRowId;
            return (int)outVal;
        }

        internal static void UpdateAgeGroup(AgeGroup group, SQLiteConnection connection)
        {
            using SQLiteTransaction transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE age_groups SET event_id=@event, distance_id=@distance, " +
                "start_age=@start, end_age=@end, custom_name=@custom WHERE group_id=@group;";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@event", group.EventId),
                new SQLiteParameter("@distance", group.DistanceId),
                new SQLiteParameter("@start", group.StartAge),
                new SQLiteParameter("@end", group.EndAge),
                new SQLiteParameter("@group", group.GroupId),
                new SQLiteParameter("@custom", group.CustomName)
            ]);
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        internal static void RemoveAgeGroup(AgeGroup group, SQLiteConnection connection)
        {
            using SQLiteTransaction transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM age_groups WHERE group_id=@group;";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@group", group.GroupId)
            ]);
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        internal static void RemoveAgeGroups(int eventId, int distanceId, SQLiteConnection connection)
        {
            using SQLiteTransaction transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM age_groups WHERE event_id=@event AND distance_id=@distance;";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@event", eventId),
                new SQLiteParameter("@distance", distanceId),
            ]);
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        internal static void RemoveAgeGroups(List<AgeGroup> groups, SQLiteConnection connection)
        {
            using SQLiteTransaction transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            foreach (AgeGroup ag in groups)
            {
                command.CommandText = "DELETE FROM age_groups WHERE group_id=@group;";
                command.Parameters.AddRange(
                [
                    new SQLiteParameter("@group", ag.GroupId),
                ]);
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        internal static void ResetAgeGroups(int eventId, SQLiteConnection connection)
        {
            using SQLiteTransaction transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM age_groups WHERE event_id=@event;";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@event", eventId),
            ]);
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        internal static List<AgeGroup> GetAgeGroups(int eventId, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM age_groups WHERE event_id=@event;";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@event", eventId)
            ]);
            SQLiteDataReader reader = command.ExecuteReader();
            List<AgeGroup> output = [];
            while (reader.Read())
            {
                output.Add(
                    new AgeGroup(
                        Convert.ToInt32(reader["group_id"]),
                        Convert.ToInt32(reader["event_id"]),
                        Convert.ToInt32(reader["distance_id"]),
                        Convert.ToInt32(reader["start_age"]),
                        Convert.ToInt32(reader["end_age"]),
                        Convert.ToInt32(reader["last_group"]),
                        reader["custom_name"].ToString()!
                        ));
            }
            reader.Close();
            return output;
        }

        internal static List<AgeGroup> GetAgeGroups(int eventId, int distanceId, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM age_groups WHERE event_id=@event AND distance_id=@distance;";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@event", eventId),
                new SQLiteParameter("@distance", distanceId)
            ]);
            SQLiteDataReader reader = command.ExecuteReader();
            List<AgeGroup> output = [];
            while (reader.Read())
            {
                output.Add(
                    new AgeGroup(
                        Convert.ToInt32(reader["group_id"]),
                        Convert.ToInt32(reader["event_id"]),
                        Convert.ToInt32(reader["distance_id"]),
                        Convert.ToInt32(reader["start_age"]),
                        Convert.ToInt32(reader["end_age"]),
                        Convert.ToInt32(reader["last_group"]),
                        reader["custom_name"].ToString()!
                    ));
            }
            reader.Close();
            return output;
        }
    }
}

