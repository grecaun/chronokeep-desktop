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
            else if (EndAge >= 99)
            {
                return $"Over {StartAge}";
            }
            return Name;
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
