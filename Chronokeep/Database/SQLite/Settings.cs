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
