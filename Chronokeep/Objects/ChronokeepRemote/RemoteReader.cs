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
    public class RemoteReader(string name, int apiId, int locationId, int eventId)
    {
        public RemoteReader() : this(string.Empty, -1, Constants.Timing.LOCATION_DUMMY, -1) { }

        [JsonPropertyName("name")]
        public string Name { get; init; } = name;

        public int ApiiDentifier { get; set; } = apiId;
        public int LocationId { get; set; } = locationId;
        public int EventId { get; set; } = eventId;
    }
}

