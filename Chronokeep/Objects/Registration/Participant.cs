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

namespace Chronokeep.Objects.Registration
{
    public class Participant
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = "";
        [JsonPropertyName("bib")]
        public string Bib { get; init; } = "";
        [JsonPropertyName("first")]
        public string FirstName { get; init; } = "";
        [JsonPropertyName("last")]
        public string LastName { get; init; } = "";
        [JsonPropertyName("birthdate")]
        public string Birthdate { get; init; } = "";
        [JsonPropertyName("gender")]
        public string Gender { get; init; } = "";
        [JsonPropertyName("distance")]
        public string Distance { get; init; } = "";
        [JsonPropertyName("mobile")]
        public string Mobile { get; init; } = "";
        [JsonPropertyName("sms")]
        public bool SmsEnabled { get; init; }
        [JsonPropertyName("apparel")]
        public string Apparel { get; set; } = "";
    }
}

