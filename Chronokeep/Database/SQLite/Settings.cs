using Chronokeep.Objects;
using System;
using System.Data.SQLite;

namespace Chronokeep.Database.SQLite
{
    internal static class Settings
    {
        internal static AppSetting? GetAppSetting(string name, SQLiteConnection connection)
        {
            AppSetting? output = null;
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM settings WHERE setting=@name";
            command.Parameters.Add(new SQLiteParameter("@name", name));
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                output = new AppSetting
                {
                    Name = Convert.ToString(reader["setting"])!,
                    Value = Convert.ToString(reader["value"])!
                };
            }
            reader.Close();
            return output;
        }

        internal static void SetAppSetting(AppSetting setting, SQLiteConnection connection)
        {
            using SQLiteTransaction? transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO settings (setting, value) VALUES (@name,@value)";
            command.Parameters.AddRange([
                new SQLiteParameter("@name", setting.Name),
                new SQLiteParameter("@value", setting.Value) ]);
            command.ExecuteNonQuery();
            transaction.Commit();
        }
    }
}