using System;
using Chronokeep.Constants;

namespace Chronokeep.Objects
{
    public class Distance : IEquatable<Distance>, IComparable<Distance>
    {
        public Distance(string name, int eventIdentifier)
        {
            Name = name;
            EventIdentifier = eventIdentifier;
        }

        public Distance(string name, int eventIdentifier, int linkedIdentifier, int type, int ranking, int wave, int startOffsetSeconds, int startOffsetMilliseconds)
        {
            Name = name;
            EventIdentifier = eventIdentifier;
            LinkedDistance = linkedIdentifier;
            Type = type;
            Ranking = ranking;
            Wave = wave;
            StartOffsetSeconds = startOffsetSeconds;
            StartOffsetMilliseconds = startOffsetMilliseconds;
            Upload = false;
        }

        public Distance(int identifier, string name, int eventIdentifier,
            double distance, int distanceUnit, int finishLocation, int finishOccurrence,
            int startLocation, int startWithin, int wave, int startOffsetSeconds, int startOffsetMilliseconds,
            int endseconds, int linkedDistance, int type, int ranking, bool smsEnabled, bool upload, string certification)
        {
            Identifier = identifier;
            Name = name;
            EventIdentifier = eventIdentifier;
            DistanceValue = distance;
            DistanceUnit = distanceUnit;
            FinishLocation = finishLocation;
            FinishOccurrence = finishOccurrence;
            StartLocation = startLocation;
            StartWithin = startWithin;
            Wave = wave;
            StartOffsetSeconds = startOffsetSeconds;
            StartOffsetMilliseconds = startOffsetMilliseconds;
            EndSeconds = endseconds;
            LinkedDistance = linkedDistance;
            Type = type;
            Ranking = ranking;
            SmsEnabled = smsEnabled;
            Upload = upload;
            Certification = certification;
        }

        public int Identifier { get; set; }
        public string Name { get; set; }
        public int EventIdentifier { get; set; }
        public double DistanceValue { get; set; }
        public int DistanceUnit { get; set; } = Distances.MILES;
        public int FinishLocation { get; set; } = Constants.Timing.LOCATION_FINISH;
        public int FinishOccurrence { get; set; } = 1;
        public int StartLocation { get; set; } = Constants.Timing.LOCATION_START;
        public int StartWithin { get; set; }
        public int Wave { get; set; } = 1;
        public int StartOffsetSeconds { get; set; }
        public int StartOffsetMilliseconds { get; set; }
        public int EndSeconds { get; set; }
        public int LinkedDistance { get; set; } = Constants.Timing.DISTANCE_NO_LINKED_ID;
        public int Type { get; set; }
        public int Ranking { get; set; }
        public bool SmsEnabled { get; set; }
        public bool Upload { get; set; }
        public string Certification { get; set; } = "";

        public int CompareTo(Distance? other)
        {
            if (other == null) return 1;
            return EventIdentifier == other.EventIdentifier ? string.Compare(Name, other.Name, StringComparison.Ordinal) : EventIdentifier.CompareTo(other.EventIdentifier);
        }

        public bool Equals(Distance? other)
        {
            if (other == null) return false;
            return EventIdentifier == other.EventIdentifier && Identifier == other.Identifier;
        }

        public void Update(Distance other)
        {
            Name = other.Name;
            EventIdentifier = other.EventIdentifier;
            DistanceValue = other.DistanceValue;
            DistanceUnit = other.DistanceUnit;
            StartLocation = other.StartLocation;
            StartWithin = other.StartWithin;
            FinishLocation = other.FinishLocation;
            FinishOccurrence = other.FinishOccurrence;
            Wave = other.Wave;
            StartOffsetSeconds = other.StartOffsetSeconds;
            StartOffsetMilliseconds = other.StartOffsetMilliseconds;
            EndSeconds = other.EndSeconds;
            LinkedDistance = other.LinkedDistance;
            Type = other.Type;
            Ranking = other.Ranking;
            SmsEnabled = other.SmsEnabled;
            Certification = other.Certification;
        }

        public void SetWaveTime(int iWave, long seconds, int milliseconds)
        {
            Wave = iWave;
            StartOffsetSeconds = (int)seconds;
            StartOffsetMilliseconds = milliseconds;
        }
    }
}
