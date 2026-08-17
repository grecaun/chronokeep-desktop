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

using Chronokeep.Objects.ChronokeepRemote;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Chronokeep.Database.SQLite
{
    internal static class RemoteReaders
    {
        public static void AddRemoteReaders(int eventId, List<RemoteReader> remoteReaders, SQLiteConnection connection)
        {
            using SQLiteTransaction? transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO remote_readers (event_id, api_id, location_id, reader_name) VALUES (@event, @api, @location, @name);";
            foreach (RemoteReader reader in remoteReaders)
            {
                command.Parameters.AddRange(
                [
                    new SQLiteParameter("@event", eventId),
                    new SQLiteParameter("@api", reader.ApiiDentifier),
                    new SQLiteParameter("@location", reader.LocationId),
                    new SQLiteParameter("@name", reader.Name)
                ]);
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        public static void DeleteRemoteReaders(int eventId, List<RemoteReader> remoteReaders, SQLiteConnection connection)
        {
            using SQLiteTransaction? transaction = connection.BeginTransaction();
            foreach (RemoteReader reader in remoteReaders)
            {
                DeleteRemoteReader(eventId, reader, connection);
            }
            transaction.Commit();
        }

        public static void DeleteRemoteReader(int eventId, RemoteReader reader, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM remote_readers WHERE event_id=@event AND api_id=@api AND reader_name=@name;";
            command.Parameters.AddRange(
            [
                    new SQLiteParameter("@event", eventId),
                    new SQLiteParameter("@api", reader.ApiiDentifier),
                    new SQLiteParameter("@name", reader.Name)
            ]);
            command.ExecuteNonQuery();
        }

        public static List<RemoteReader> GetRemoteReaders(int eventId, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM remote_readers WHERE event_id=@event;";
            command.Parameters.Add(new SQLiteParameter("@event", eventId));
            SQLiteDataReader reader = command.ExecuteReader();
            List<RemoteReader> output = [];
            while (reader.Read())
            {
                output.Add(new RemoteReader(
                        reader["reader_name"].ToString()!,
                        Convert.ToInt32(reader["api_id"]),
                        Convert.ToInt32(reader["location_id"]),
                        Convert.ToInt32(reader["event_id"])
                    ));
            }
            reader.Close();
            return output;
        }
    }
}
