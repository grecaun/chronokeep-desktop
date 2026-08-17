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
    public class PortalApi
    {
        public const string API_TYPE_CHRONOKEEP_REMOTE        = "CHRONOKEEP_REMOTE";
        public const string API_TYPE_CHRONOKEEP_REMOTE_SELF   = "CHRONOKEEP_REMOTE_SELF";
        public const string API_URI_CHRONOKEEP_REMOTE         = @"https://remote.chronokeep.com/";

        [JsonPropertyName("id")]
        public long Id { get; init; }
        [JsonPropertyName("nickname")]
        public string Nickname { get; set; } = "";
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "";
        [JsonPropertyName("token")]
        public string Token { get; set; } = "";
        [JsonPropertyName("uri")]
        public string Uri { get; set; } = "";
    }
}

