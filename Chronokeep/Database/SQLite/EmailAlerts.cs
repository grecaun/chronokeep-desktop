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

using System.Collections.Generic;
using System.Data.SQLite;

namespace Chronokeep.Database.SQLite
{
    internal static class EmailAlerts
    {
        public static List<int> GetEmailAlerts(int eventId, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM email_alert WHERE event_id=@event;";
            command.Parameters.Add(new SQLiteParameter("@event", eventId));
            SQLiteDataReader reader = command.ExecuteReader();
            List<int> output = [];
            while (reader.Read())
            {
                if (int.TryParse(reader["eventspecific_id"].ToString(), out int id))
                {
                    output.Add(id);
                }
            }
            reader.Close();
            return output;
        }

        public static void AddEmailAlert(int eventId, int eventspecificId, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO email_alert (event_id, eventspecific_id) VALUES (@event, @eventspec);";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@event", eventId),
                new SQLiteParameter("@eventspec", eventspecificId)
            ]);
            command.ExecuteNonQuery();
        }

        public static void RemoveEmailAlert(int eventId, int eventspecificId, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM email_alert WHERE event_id=@event AND eventspecific_id=@eventspec;";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@event", eventId),
                new SQLiteParameter("@eventspec", eventspecificId)
            ]);
            command.ExecuteNonQuery();
        }
    }
}

