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

namespace Chronokeep.Objects
{
    /// <summary>
    /// One Runsignup event (5K, 10K, Kids Run) in a linked race and the Chronokeep distance its
    /// participants go into. A DistanceId of <see cref="SkipDistance"/> means that event is not downloaded.
    /// </summary>
    public class RegistrationDistance(int eventId, string rsuEventId, string rsuEventName, int distanceId)
    {
        public const int SkipDistance = -1;

        public int EventId { get; set; } = eventId;
        public string RsuEventId { get; set; } = rsuEventId;
        public string RsuEventName { get; set; } = rsuEventName;
        public int DistanceId { get; set; } = distanceId;
        public bool IsSkipped => DistanceId == SkipDistance;
    }
}
