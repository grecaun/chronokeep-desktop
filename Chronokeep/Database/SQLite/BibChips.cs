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

using Chronokeep.Helpers;
using Chronokeep.Objects;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Chronokeep.Database.SQLite
{
    internal static class BibChips
    {
        internal static void AddBibChipAssociation(int eventId, List<BibChipAssociation> assoc, SQLiteConnection connection)
        {
            using SQLiteTransaction transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO bib_chip_assoc (event_id, bib, chip) VALUES (@eventId, @bib, @chip);";
            foreach (BibChipAssociation item in assoc)
            {
                Log.D("Database.SQLite.BibChips", $"Event id {eventId} Bib {item.Bib} Chip {item.Chip}");
                command.Parameters.AddRange(
                    [
                        new SQLiteParameter("@eventId", eventId),
                        new SQLiteParameter("@bib", item.Bib),
                        new SQLiteParameter("@chip", item.Chip),
                    ]);
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        internal static List<BibChipAssociation> GetBibChips(SQLiteConnection connection)
        {
            List<BibChipAssociation> output = [];
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM bib_chip_assoc";
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                output.Add(new BibChipAssociation
                {
                    EventId = Convert.ToInt32(reader["event_id"]),
                    Bib = reader["bib"].ToString()!,
                    Chip = reader["chip"].ToString()!
                });
            }
            reader.Close();
            return output;
        }

        internal static List<BibChipAssociation> GetBibChips(int eventId, SQLiteConnection connection)
        {
            List<BibChipAssociation> output = [];
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM bib_chip_assoc WHERE event_id=@eventId";
            command.Parameters.Add(new SQLiteParameter("@eventId", eventId));
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                output.Add(new BibChipAssociation
                {
                    EventId = Convert.ToInt32(reader["event_id"]),
                    Bib = reader["bib"].ToString()!,
                    Chip = reader["chip"].ToString()!
                });
            }
            reader.Close();
            return output;
        }

        internal static void RemoveBibChipAssociation(int eventId, string chip, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandType = System.Data.CommandType.Text;
            command.CommandText = "DELETE FROM bib_chip_assoc WHERE event_id=@event AND chip=@chip;";
            command.Parameters.AddRange([
                new SQLiteParameter("@event", eventId),
                new SQLiteParameter("@chip", chip) ]);
            command.ExecuteNonQuery();
        }
    }
}

