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

using System.Data.SQLite;

namespace Chronokeep.Database.SQLite
{
    internal static class DatabaseHelpers
    {
        internal static void HardResetDatabase(SQLiteConnection connection)
        {
            using SQLiteTransaction transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText =
                "DROP TABLE email_alert;" +
                "DROP TABLE email_ban_list;" +
                "DROP TABLE sms_ban_list;" +
                "DROP TABLE sms_alert;" +
                "DROP TABLE remote_readers;" +
                "DROP TABLE alarms;" +
                "DROP TABLE timing_systems;" +
                "DROP TABLE age_groups;" +
                "DROP TABLE settings;" +
                "DROP TABLE chipreads;" +
                "DROP TABLE time_results;" +
                "DROP TABLE segments;" +
                "DROP TABLE eventspecific;" +
                "DROP TABLE participants;" +
                "DROP TABLE timing_locations;" +
                "DROP TABLE distances;" +
                "DROP TABLE events;" +
                "DROP TABLE bib_chip_assoc;" +
                "DROP TABLE results_api;";
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        internal static void ResetDatabase(SQLiteConnection connection)
        {
            using SQLiteTransaction transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText =
                "DELETE FROM email_alert;" +
                "DELETE FROM email_ban_list;" +
                "DELETE FROM sms_ban_list;" +
                "DELETE FROM sms_alert;" +
                "DELETE FROM remote_readers;" +
                "DELETE FROM alarms;" +
                "DELETE FROM timing_systems;" +
                "DELETE FROM age_groups;" +
                "DELETE FROM settings;" +
                "DELETE FROM chipreads;" +
                "DELETE FROM time_results;" +
                "DELETE FROM segments;" +
                "DELETE FROM eventspecific;" +
                "DELETE FROM participants;" +
                "DELETE FROM timing_locations;" +
                "DELETE FROM distances;" +
                "DELETE FROM events;" +
                "DELETE FROM bib_chip_assoc;" +
                "DELETE FROM results_api;";
            command.ExecuteNonQuery();
            transaction.Commit();
        }
    }
}
