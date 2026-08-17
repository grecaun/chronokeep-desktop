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

using System.Text.Json.Serialization;

namespace Chronokeep.Objects.ChronokeepPortal
{
    public class PortalError
    {
        public const string UNKNOWN_COMMAND       = "UNKNOWN_COMMAND";
        public const string TOO_MANY_CONNECTIONS  = "TOO_MANY_CONNECTIONS";
        public const string TOO_MANY_REMOTE_API   = "TOO_MANY_REMOTE_API";
        public const string SERVER_ERROR          = "SERVER_ERROR";
        public const string DATABASE_ERROR        = "DATABASE_ERROR";
        public const string INVALID_READER_TYPE   = "INVALID_READER_TYPE";
        public const string READER_CONNECTION     = "READER_CONNECTION";
        public const string NOT_FOUND             = "NOT_FOUND";
        public const string INVALID_SETTING       = "INVALID_SETTING";
        public const string INVALID_API_TYPE      = "INVALID_API_TYPE";
        public const string ALREADY_SUBSCRIBED    = "ALREADY_SUBSCRIBED";
        public const string ALREADY_RUNNING       = "ALREADY_RUNNING";
        public const string NOT_RUNNING           = "NOT_RUNNING";
        public const string NO_REMOTE_API         = "NO_REMOTE_API";
        public const string STARTING_UP           = "STARTING_UP";
        public const string INVALID_READ          = "INVALID_READ";
        public const string NOT_ALLOWED           = "NOT_ALLOWED";

        [JsonPropertyName("error_type")]
        public string Type { get; set; } = "";
        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }
}

