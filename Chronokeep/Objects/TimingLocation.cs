using System;

namespace Chronokeep.Objects
{
    public class TimingLocation : IEquatable<TimingLocation>, IComparable<TimingLocation>
    {
        public TimingLocation(int eventIdentifier, string nameString)
        {
            EventIdentifier = eventIdentifier;
            Name = nameString;
            MaxOccurrences = 1;
            IgnoreWithin = -1;
        }

        public TimingLocation(int identifier, int eventIdentifier, string nameString)
        {
            Identifier = identifier;
            EventIdentifier = eventIdentifier;
            Name = nameString;
            MaxOccurrences = 1;
            IgnoreWithin = -1;
        }

        public TimingLocation(int id, int eventId, string name, int maxOcc, int ignore)
        {
            Identifier = id;
            EventIdentifier = eventId;
            Name = name;
            MaxOccurrences = maxOcc;
            IgnoreWithin = ignore;
        }

        public int Identifier { get; set; } = -1;
        public int EventIdentifier { get; set; }
        public string Name { get; set; }
        public int MaxOccurrences { get; set; }

        public int IgnoreWithin { get; set; }

        public int CompareTo(TimingLocation? other)
        {
            return other == null ? 1 : Identifier.CompareTo(other.Identifier);
        }

        public bool Equals(TimingLocation? other)
        {
            if (other == null) return false;
            return Identifier == other.Identifier && EventIdentifier == other.EventIdentifier;
        }

        public void CopyFrom(TimingLocation other)
        {
            EventIdentifier = other.EventIdentifier;
            Name = other.Name;
            MaxOccurrences = other.MaxOccurrences;
            IgnoreWithin = other.IgnoreWithin;
        }
    }
}
