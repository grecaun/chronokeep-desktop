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
    internal static class TimingSystems
    {
        internal static int AddTimingSystem(TimingSystem system, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO timing_systems (ts_ip, ts_port, ts_location, ts_type)" +
                " VALUES (@ip, @port, @location, @type);";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@ip", system.IpAddress),
                new SQLiteParameter("@port", system.Port),
                new SQLiteParameter("@location", system.LocationId),
                new SQLiteParameter("@type", system.Type)
            ]);
            command.ExecuteNonQuery();
            long outVal = connection.LastInsertRowId;
            return (int)outVal;
        }

        internal static void UpdateTimingSystem(TimingSystem system, SQLiteConnection connection)
        {
            using SQLiteTransaction? transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE timing_systems SET ts_ip=@ip, ts_port=@port, ts_location=@location, ts_type=@type WHERE ts_identifier=@id;";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@ip", system.IpAddress),
                new SQLiteParameter("@port", system.Port),
                new SQLiteParameter("@location", system.LocationId),
                new SQLiteParameter("@type", system.Type),
                new SQLiteParameter("@id", system.SystemIdentifier)
            ]);
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        internal static void SetTimingSystems(List<TimingSystem> systems, SQLiteConnection connection)
        {
            using SQLiteTransaction? transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM timing_systems;";
            command.ExecuteNonQuery();
            foreach (TimingSystem sys in systems)
            {
                AddTimingSystem(sys, connection);
            }
            transaction.Commit();
        }

        internal static void RemoveTimingSystem(int systemId, SQLiteConnection connection)
        {
            using SQLiteTransaction? transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM timing_systems WHERE ts_identifier=@id;";
            command.Parameters.Add(new SQLiteParameter("@id", systemId));
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        internal static List<TimingSystem> GetTimingSystems(SQLiteConnection connection)
        {
            List<TimingSystem> output = [];
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM timing_systems;";
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                output.Add(new TimingSystem(
                    Convert.ToInt32(reader["ts_identifier"]),
                    reader["ts_ip"].ToString()!,
                    Convert.ToInt32(reader["ts_port"]),
                    Convert.ToInt32(reader["ts_location"]),
                    reader["ts_type"].ToString()!
                    ));
            }
            reader.Close();
            return output;
        }
    }
}

