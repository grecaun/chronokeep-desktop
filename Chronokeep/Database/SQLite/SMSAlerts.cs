using System.Collections.Generic;
using System.Data.SQLite;

namespace Chronokeep.Database.SQLite
{
    internal class SmsAlerts
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