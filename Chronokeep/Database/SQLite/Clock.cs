using Chronokeep.Objects;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Chronokeep.Database.SQLite
{
    internal static class Clock
    {
        public static List<Chronoclock> GetClocks(SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM chronoclocks;";
            SQLiteDataReader reader = command.ExecuteReader();
            List<Chronoclock> output = [];
            while (reader.Read())
            {
                output.Add(new Chronoclock
                {
                    Identifier = Convert.ToInt32(reader["clock_id"]),
                    Name = reader["name"].ToString()!,
                    Url = reader["url"].ToString()!,
                    Enabled = Convert.ToInt32(reader["enabled"]) != 0,
                });
            }
            reader.Close();
            return output;
        }

        public static int AddClock(Chronoclock clock, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO chronoclocks (name, url, enabled) VALUES (@name, @url, @enabled);";
            command.Parameters.AddRange([
                new SQLiteParameter("@name", clock.Name),
                new SQLiteParameter("@url", clock.Url),
                new SQLiteParameter("@enabled", clock.Enabled ? 1 : 0)
                ]);
            command.ExecuteNonQuery();
            return (int)connection.LastInsertRowId;
        }

        public static void UpdateClock(Chronoclock clock, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE chronoclocks SET name=@name, url=@url, enabled=@enabled WHERE clock_id=@clockID;";
            command.Parameters.AddRange([
                new SQLiteParameter("@name", clock.Name),
                new SQLiteParameter("@url", clock.Url),
                new SQLiteParameter("@enabled", clock.Enabled ? 1 : 0),
                new SQLiteParameter("@clockID", clock.Identifier)
                ]);
            command.ExecuteNonQuery();
        }

        public static void RemoveClocks(List<Chronoclock> clocks, SQLiteConnection connection)
        {
            using SQLiteTransaction transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM chronoclocks WHERE clock_id=@clockID;";
            foreach (Chronoclock clock in clocks)
            {
                command.Parameters.Add(new SQLiteParameter("@clockID", clock.Identifier));
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }
    }
}