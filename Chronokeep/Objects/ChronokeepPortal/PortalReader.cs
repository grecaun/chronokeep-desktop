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
    public class PortalReader
    {
        public const string READER_KIND_ZEBRA = "ZEBRA";
        public const string READER_KIND_IMPINJ = "IMPINJ";
        public const string READER_KIND_RFID = "RFID";

        public const string READER_DEFAULT_PORT_ZEBRA = "5084";
        public const string READER_DEFAULT_PORT_IMPINJ = "5084";
        public const string READER_DEFAULT_PORT_RFID = "23";

        [JsonPropertyName("id")]
        public long Id { get; init; }
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "";
        [JsonPropertyName("ip_address")]
        public string IpAddress { get; set; } = "";
        [JsonPropertyName("port")]
        public uint Port { get; set; }
        [JsonPropertyName("auto_connect")]
        public bool AutoConnect { get; set; }
        [JsonPropertyName("reading")]
        public bool Reading { get; set; } = false;
        [JsonPropertyName("connected")]
        public bool Connected { get; set; } = false;
        [JsonPropertyName("antennas")]
        public int[] Antennas { get; set; } = [];
    }
}

