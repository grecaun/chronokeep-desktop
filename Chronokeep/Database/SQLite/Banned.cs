using System.Collections.Generic;
using System.Data.SQLite;

namespace Chronokeep.Database.SQLite
{
    internal class Banned
    {
        public static List<string> GetBannedPhones(SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM sms_ban_list;";
            SQLiteDataReader reader = command.ExecuteReader();
            List<string> output = [];
            while (reader.Read())
            {
                output.Add(reader["banned_phone"].ToString()!);
            }
            reader.Close();
            return output;
        }

        public static void AddBannedPhone(string phone, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO sms_ban_list (banned_phone) VALUES (@phone);";
            command.Parameters.Add(new SQLiteParameter("@phone", phone));
            command.ExecuteNonQuery();
        }

        public static void AddBannedPhones(List<string> phones, SQLiteConnection connection)
        {
            using SQLiteTransaction transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO sms_ban_list (banned_phone) VALUES (@phone);";
            foreach (string phone in phones)
            {
                command.Parameters.Add(new SQLiteParameter("@phone", phone));
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        public static void RemoveBannedPhone(string phone, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM sms_ban_list WHERE banned_phone=@phone;";
            command.Parameters.Add(new SQLiteParameter("@phone", phone));
            command.ExecuteNonQuery();
        }

        public static void RemoveBannedPhones(List<string> phones, SQLiteConnection connection)
        {
            using SQLiteTransaction transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM sms_ban_list WHERE banned_phone=@phone;";
            foreach (string phone in phones)
            {
                command.Parameters.Add(new SQLiteParameter("@phone", phone));
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        public static void ClearBannedPhones(SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM sms_ban_list;";
            command.ExecuteNonQuery();
        }

        public static List<string> GetBannedEmails(SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM email_ban_list;";
            SQLiteDataReader reader = command.ExecuteReader();
            List<string> output = [];
            while (reader.Read())
            {
                output.Add(reader["banned_email"].ToString()!);
            }
            reader.Close();
            return output;
        }

        public static void AddBannedEmail(string email, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO email_ban_list (banned_email) VALUES (@email);";
            command.Parameters.Add(new SQLiteParameter("@email", email));
            command.ExecuteNonQuery();
        }

        public static void AddBannedEmails(List<string> emails, SQLiteConnection connection)
        {
            using SQLiteTransaction transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO email_ban_list (banned_email) VALUES (@email);";
            foreach (string email in emails)
            {
                command.Parameters.Add(new SQLiteParameter("@email", email));
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        public static void RemoveBannedEmail(string email, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM email_ban_list WHERE banned_email=@email;";
            command.Parameters.Add(new SQLiteParameter("@email", email));
            command.ExecuteNonQuery();
        }

        public static void RemoveBannedEmails(List<string> emails, SQLiteConnection connection)
        {
            using SQLiteTransaction transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM email_ban_list WHERE banned_email=@email;";
            foreach (string email in emails)
            {
                command.Parameters.Add(new SQLiteParameter("@email", email));
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        public static void ClearBannedEmails(SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM email_ban_list;";
            command.ExecuteNonQuery();
        }
    }
}
