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

using System;

namespace Chronokeep.Objects
{
    public class AgeGroup(
        int groupId,
        int eventId,
        int distanceId,
        int startAge,
        int endAge,
        int lastGroup,
        string customName)
        : IEquatable<AgeGroup>, IComparable<AgeGroup>
    {
        private int lastGroup = lastGroup;

        public AgeGroup(
            int eventId,
            int distanceId,
            int startAge,
            int endAge,
            string customName = ""
            ) : this(
                -1,
                eventId,
                distanceId,
                startAge,
                endAge,
                Constants.Timing.AGEGROUPS_LASTGROUP_FALSE,
                customName
                ) { }

        public int EventId { get; set; } = eventId;
        public int DistanceId { get; set; } = distanceId;
        public int StartAge { get; set; } = startAge;
        public int EndAge { get; set; } = endAge;
        public int GroupId { get; set; } = groupId;

        public bool LastGroup
        {
            get => lastGroup == Constants.Timing.AGEGROUPS_LASTGROUP_TRUE;
            set => lastGroup = value ? Constants.Timing.AGEGROUPS_LASTGROUP_TRUE : Constants.Timing.AGEGROUPS_LASTGROUP_FALSE;
        }
        private string Name => LastGroup ? $"Over {StartAge}" : $"{StartAge}-{EndAge}";
        public string CustomName { get; set; } = customName;

        public string PrettyName()
        {
            if (CustomName.Length > 0)
            {
                return CustomName;
            }
            if (StartAge < 1 && EndAge > 0)
            {
                return $"Under {EndAge + 1}";
            }
            return EndAge >= 99 ? $"Over {StartAge}" : Name;
        }

        public int CompareTo(AgeGroup? other)
        {
            if (other == null) return 1;
            if (EventId != other.EventId)
            {
                return EventId.CompareTo(other.EventId);
            }
            return DistanceId != other.DistanceId ? DistanceId.CompareTo(other.DistanceId) : StartAge.CompareTo(other.StartAge);
        }

        public bool Equals(AgeGroup? that)
        {
            if (that == null) return false;
            return EventId == that.EventId && DistanceId == that.DistanceId && StartAge == that.StartAge && EndAge == that.StartAge;
        }
    }
}

