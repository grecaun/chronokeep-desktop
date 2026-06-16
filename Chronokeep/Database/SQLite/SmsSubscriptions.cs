using Chronokeep.Objects.ChronoKeepAPI;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Chronokeep.Database.SQLite
{
    internal class SmsSubscriptions
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