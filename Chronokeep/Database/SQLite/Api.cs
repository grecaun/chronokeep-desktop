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
    internal static class Api
    {
        internal static int AddApi(ApiObject anApi, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO results_api (api_type, api_url, api_auth_token, api_nickname, api_web_url)" +
                " VALUES (@type, @url, @token, @nickname, @weburl);";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@type", anApi.Type),
                new SQLiteParameter("@url", anApi.Url),
                new SQLiteParameter("@token", anApi.AuthToken),
                new SQLiteParameter("@nickname", anApi.Nickname),
                new SQLiteParameter("@weburl", anApi.WebUrl)
            ]);
            command.ExecuteNonQuery();
            long outVal = connection.LastInsertRowId;
            return (int)outVal;
        }

        internal static void UpdateApi(ApiObject anApi, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE results_api SET api_type=@type, api_url=@url, api_auth_token=@token, api_nickname=@nickname, api_web_url=@weburl WHERE api_id=@id;";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@type", anApi.Type),
                new SQLiteParameter("@url", anApi.Url),
                new SQLiteParameter("@token", anApi.AuthToken),
                new SQLiteParameter("@nickname", anApi.Nickname),
                new SQLiteParameter("@id", anApi.Identifier),
                new SQLiteParameter("@weburl", anApi.WebUrl)
            ]);
            command.ExecuteNonQuery();
        }

        internal static void RemoveApi(int identifier, SQLiteConnection connection)
        {
            using SQLiteTransaction transaction = connection.BeginTransaction();
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE events SET api_id=-1, api_event_id='' WHERE api_id=@id; DELETE FROM results_api WHERE api_id=@id;";
            command.Parameters.Add(new SQLiteParameter("@id", identifier));
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        internal static ApiObject? GetApi(int identifier, SQLiteConnection connection)
        {
            if (identifier < 0)
            {
                return null;
            }
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM results_api WHERE api_id=@id";
            command.Parameters.Add(new SQLiteParameter("@id", identifier));
            SQLiteDataReader reader = command.ExecuteReader();
            ApiObject? output = null;
            if (reader.Read())
            {
                output = new ApiObject(
                    Convert.ToInt32(reader["api_id"]),
                    reader["api_type"].ToString()!,
                    reader["api_url"].ToString()!,
                    reader["api_nickname"].ToString()!,
                    reader["api_auth_token"].ToString()!,
                    reader["api_web_url"].ToString()!
                    );
            }
            reader.Close();
            return output;
        }

        internal static List<ApiObject> GetAllApi(SQLiteConnection connection)
        {
            List<ApiObject> output = [];
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM results_api;";
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                output.Add(new ApiObject(
                    Convert.ToInt32(reader["api_id"]),
                    reader["api_type"].ToString()!,
                    reader["api_url"].ToString()!,
                    reader["api_nickname"].ToString()!,
                    reader["api_auth_token"].ToString()!,
                    reader["api_web_url"].ToString()!
                    ));
            }
            reader.Close();
            return output;
        }
    }
}
