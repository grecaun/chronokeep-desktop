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

namespace Chronokeep.Objects.ChronoKeepAPI
{
    public class ApiEventYear
    {
        [JsonPropertyName("year")]
        public string Year { get; set; } = "";
        [JsonPropertyName("date_time")]
        public string DateTime { get; set; } = "";
        [JsonPropertyName("live")]
        public bool Live { get; set; }
        [JsonPropertyName("days_allowed")]
        public int DaysAllowed { get; set; } = 1;
        [JsonPropertyName("ranking_type")]
        public string RankingType { get; set; } = "gun";
    }
}

