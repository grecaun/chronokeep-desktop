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

namespace Chronokeep.Objects.ChronokeepRemote
{
    public class RemoteRead
    {
        // Identifier is either a BIB or a CHIP value.
        [JsonPropertyName("identifier")]
        public string Identifier { get; set; } = "";
        [JsonPropertyName("seconds")]
        public long Seconds { get; set; }
        [JsonPropertyName("milliseconds")]
        public int Milliseconds { get; set; }
        [JsonPropertyName("ident_type")]
        public IdentType IdentType { get; set; }
        [JsonPropertyName("type")]
        public ReadType Type { get; set; }
        [JsonPropertyName("antenna")]
        public int Antenna { get; set; }
        [JsonPropertyName("reader")]
        public string Reader { get; set; } = "";
        [JsonPropertyName("rssi")]
        public string Rssi { get; set; } = "";

        public ChipRead ConvertToChipRead(int eventId, int locationId)
        {
            return new ChipRead(
                eventId,
                locationId,
                IdentType == IdentType.chip ? Identifier : Constants.Timing.CHIPREAD_DUMMYCHIP,
                IdentType == IdentType.bib ? Identifier : Constants.Timing.CHIPREAD_DUMMYBIB,
                Seconds,
                Milliseconds,
                Antenna,
                Reader,
                Rssi,
                IdentType == IdentType.chip ? Constants.Timing.CHIPREAD_TYPE_CHIP : Constants.Timing.CHIPREAD_TYPE_MANUAL
                );
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum IdentType
    {
        bib,
        chip,
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ReadType
    {
        reader,
        manual,
    }
}

