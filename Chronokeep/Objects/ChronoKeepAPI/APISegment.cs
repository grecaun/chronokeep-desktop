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
    public class ApiSegment
    {
        [JsonPropertyName("location")]
        public string Location { get; set; } = "";
        [JsonPropertyName("distance_name")]
        public string DistanceName { get; set; } = "";
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("distance_value")]
        public double DistanceValue { get; init; }
        [JsonPropertyName("distance_unit")]
        public string DistanceUnit { get; set; } = "";
        [JsonPropertyName("gps")]
        public string Gps { get; set; } = "";
        [JsonPropertyName("map_link")]
        public string MapLink { get; set; } = "";
    }
}

