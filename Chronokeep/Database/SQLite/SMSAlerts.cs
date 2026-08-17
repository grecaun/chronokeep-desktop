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
    internal static class SmsAlerts
    {
        public static List<(int, int)> GetSmsAlerts(int eventId, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM sms_alert WHERE event_id=@event;";
            command.Parameters.Add(new SQLiteParameter("@event", eventId));
            SQLiteDataReader reader = command.ExecuteReader();
            List<(int, int)> output = [];
            while (reader.Read())
            {
                if (int.TryParse(reader["eventspecific_id"].ToString(), out int id) && int.TryParse(reader["segment_id"].ToString(), out int seg))
                {
                    output.Add((id, seg));
                }
            }
            reader.Close();
            return output;
        }

        public static void AddSmsAlert(int eventId, int eventspecificId, int segmentId, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO sms_alert (event_id, eventspecific_id, segment_id) VALUES (@event, @eventspec, @segment);";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@event", eventId),
                new SQLiteParameter("@eventspec", eventspecificId),
                new SQLiteParameter("@segment", segmentId)
            ]);
            command.ExecuteNonQuery();
        }
    }
}
