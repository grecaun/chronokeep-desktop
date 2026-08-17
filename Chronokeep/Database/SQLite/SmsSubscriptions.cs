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

using Chronokeep.Objects.ChronoKeepAPI;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Chronokeep.Database.SQLite
{
    internal static class SmsSubscriptions
    {
        public static List<ApiSmsSubscription> GetSmsSubscriptions(int eventId, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = "SELECT * FROM sms_subscriptions WHERE event_id=@event;";
            command.Parameters.Add(new SQLiteParameter("@event", eventId));
            SQLiteDataReader reader = command.ExecuteReader();
            List<ApiSmsSubscription> output = [];
            while (reader.Read())
            {
                output.Add(new ApiSmsSubscription
                {
                    Bib = reader["bib"].ToString()!,
                    First = reader["first"].ToString()!,
                    Last = reader["last"].ToString()!,
                    Phone = reader["phone"].ToString()!,
                });
            }
            reader.Close();
            return output;
        }

        public static void AddSmsSubscription(int eventId, ApiSmsSubscription subscription, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = "INSERT INTO sms_subscriptions(event_id, bib, first, last, phone) VALUES (@event, @bib, @first, @last, @phone);";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@event", eventId),
                new SQLiteParameter("@bib", subscription.Bib),
                new SQLiteParameter("@first", subscription.First),
                new SQLiteParameter("@last", subscription.Last),
                new SQLiteParameter("@phone", subscription.Phone),
            ]);
            command.ExecuteNonQuery();
        }

        public static void DeleteSmsSubscriptions(int eventId, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = "DELETE FROM sms_subscriptions WHERE event_id=@event;";
            command.Parameters.Add(new SQLiteParameter("@event", eventId));
            command.ExecuteNonQuery();
        }
    }
}
