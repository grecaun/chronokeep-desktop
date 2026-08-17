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
    public class PortalRead
    {
        public const string READ_KIND_CHIP         = "reader";
        public const string READ_KIND_MANUAL       = "manual";
        public const string READ_IDENT_TYPE_CHIP   = "chip";
        public const string READ_IDENT_TYPE_BIB    = "bib";

        [JsonPropertyName("identifier")]
        public string Identifier { get; set; } = "";
        [JsonPropertyName("seconds")]
        public long Seconds { get; set; }
        [JsonPropertyName("milliseconds")]
        public int Milliseconds { get; set; }
        [JsonPropertyName("reader_seconds")]
        public long ReaderSeconds { get; set; }
        [JsonPropertyName("reader_milliseconds")]
        public int ReaderMilliseconds { get; set; }
        [JsonPropertyName("antenna")]
        public int Antenna { get; set; }
        [JsonPropertyName("reader")]
        public string Reader { get; set; } = "";
        [JsonPropertyName("rssi")]
        public string Rssi { get; set; } = "";
        [JsonPropertyName("ident_type")]
        public string IdentType { get; set; } = "";
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";
    }
}

