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
    /// <summary>The Runsignup event → Chronokeep distance map for a linked event.</summary>
    internal static class RegistrationDistances
    {
        internal static List<RegistrationDistance> GetRegistrationDistances(int eventId, SQLiteConnection connection)
        {
            List<RegistrationDistance> output = [];
            using SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM registration_distances WHERE event_id=@event ORDER BY rsu_event_name;";
            command.Parameters.Add(new SQLiteParameter("@event", eventId));
            using SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                output.Add(new RegistrationDistance(
                    Convert.ToInt32(reader["event_id"]),
                    reader["rsu_event_id"].ToString()!,
                    reader["rsu_event_name"].ToString()!,
                    Convert.ToInt32(reader["distance_id"])
                    ));
            }
            return output;
        }

        /// <summary>Replaces the event's whole map, so Runsignup events dropped from the wizard are dropped here too.</summary>
        internal static void SetRegistrationDistances(int eventId, List<RegistrationDistance> distances, SQLiteConnection connection)
        {
            using SQLiteTransaction transaction = connection.BeginTransaction();
            using (SQLiteCommand clear = connection.CreateCommand())
            {
                clear.CommandText = "DELETE FROM registration_distances WHERE event_id=@event;";
                clear.Parameters.Add(new SQLiteParameter("@event", eventId));
                clear.ExecuteNonQuery();
            }
            foreach (RegistrationDistance distance in distances)
            {
                using SQLiteCommand insert = connection.CreateCommand();
                insert.CommandText = "INSERT INTO registration_distances (event_id, rsu_event_id, rsu_event_name, distance_id) " +
                    "VALUES (@event, @rsuEvent, @rsuName, @distance);";
                insert.Parameters.AddRange([
                    new SQLiteParameter("@event", eventId),
                    new SQLiteParameter("@rsuEvent", distance.RsuEventId),
                    new SQLiteParameter("@rsuName", distance.RsuEventName),
                    new SQLiteParameter("@distance", distance.DistanceId),
                ]);
                insert.ExecuteNonQuery();
            }
            transaction.Commit();
        }
    }
}
