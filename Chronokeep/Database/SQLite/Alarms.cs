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
using System.Collections.Generic;
using System.Data.SQLite;

namespace Chronokeep.Database.SQLite
{
    internal static class Alarms
    {
        internal static List<Alarm> SaveAlarms(int eventId, List<Alarm> alarms, SQLiteConnection connection)
        {
            List<Alarm> output = [];
            using SQLiteTransaction transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO alarms (event_id, alarm_bib, alarm_chip, alarm_enabled, alarm_sound) VALUES (@eventId, @bib, @chip, @enabled, @sound);";
            foreach (Alarm item in alarms)
            {
                command.Parameters.AddRange(
                [
                    new SQLiteParameter("@eventId", eventId),
                    new SQLiteParameter("@bib", item.Bib),
                    new SQLiteParameter("@chip", item.Chip),
                    new SQLiteParameter("@enabled", item.Enabled ? 1 : 0),
                    new SQLiteParameter("@sound", item.AlarmSound),
                ]);
                command.ExecuteNonQuery();
                item.Identifier = (int)connection.LastInsertRowId;
                output.Add(item);
            }
            transaction.Commit();
            return output;
        }

        internal static int SaveAlarm(int eventId, Alarm alarm, SQLiteConnection connection)
        {
            using SQLiteTransaction transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO alarms (event_id, alarm_bib, alarm_chip, alarm_enabled, alarm_sound) VALUES (@eventId, @bib, @chip, @enabled, @sound);";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@eventId", eventId),
                new SQLiteParameter("@bib", alarm.Bib),
                new SQLiteParameter("@chip", alarm.Chip),
                new SQLiteParameter("@enabled", alarm.Enabled ? 1 : 0),
                new SQLiteParameter("@sound", alarm.AlarmSound),
            ]);
            command.ExecuteNonQuery();
            transaction.Commit();
            long outVal = connection.LastInsertRowId;
            return (int)outVal;
        }

        internal static List<Alarm> GetAlarms(int eventId, SQLiteConnection connection)
        {
            List<Alarm> output = [];
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM alarms WHERE event_id=@eventId;";
            command.Parameters.AddRange([
                new SQLiteParameter("@eventId", eventId)
            ]);
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                output.Add(new Alarm(
                    Convert.ToInt32(reader["alarm_id"]),
                    reader["alarm_bib"].ToString()!,
                    reader["alarm_chip"].ToString()!,
                    Convert.ToInt32(reader["alarm_enabled"]) == 1,
                    Convert.ToInt32(reader["alarm_sound"])
                    ));
            }
            reader.Close();
            return output;
        }

        internal static void DeleteAlarms(int eventId, SQLiteConnection connection)
        {
            using SQLiteTransaction transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM alarms WHERE event_id=@eventId;";
            command.Parameters.AddRange([
                new SQLiteParameter("@eventId", eventId)
            ]);
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        internal static void DeleteAlarm(Alarm alarm, SQLiteConnection connection)
        {
            using SQLiteTransaction transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM alarms WHERE alarm_id=@alarmId;";
            command.Parameters.AddRange([
                new SQLiteParameter("@alarmId", alarm.Identifier)
            ]);
            command.ExecuteNonQuery();
            transaction.Commit();
        }
    }
}

