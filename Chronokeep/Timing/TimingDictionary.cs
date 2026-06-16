using Chronokeep.Objects;
using System.Collections.Generic;

namespace Chronokeep.Timing
{
    public class TimingDictionary
    {
        // Dictionaries for storing information about the race.
        public readonly Dictionary<int, TimingLocation> LocationDictionary = [];
        // (DistanceId, LocationId, Occurrence)
        public readonly Dictionary<(int, int, int), Segment> SegmentDictionary = [];
        // Participants are stored based upon BIB and EVENTSPECIFICIDENTIFIER because we use both
        public readonly Dictionary<string, Participant> ParticipantBibDictionary = [];
        public readonly Dictionary<int, Participant> ParticipantEventSpecificDictionary = [];
        // Start times. Item at 0 should always be 00:00:00.000. Key is Distance ID
        public readonly Dictionary<int, (long Seconds, int Milliseconds)> DistanceStartDict = [];
        public readonly Dictionary<int, (long Seconds, int Milliseconds)> DistanceEndDict = [];
        public readonly Dictionary<int, Distance> DistanceDictionary = [];
        public readonly Dictionary<string, Distance> DistanceNameDictionary = [];

        // Link bibs and chipreads for adding occurence to bib based dnf entry.
        // We changed the database to allow multiple chips per bib.
        public readonly Dictionary<string, List<string>> BibToChipDictionary = [];
        public Dictionary<string, string> ChipToBibDictionary = [];

        // Linked distance dictionaries
        public Dictionary<string, (Distance, int)> LinkedDistanceDictionary = [];
        public Dictionary<int, int> LinkedDistanceIdentifierDictionary = [];

        // HashSet for non-linked distances.
        public HashSet<Distance> MainDistances = [];
        public Dictionary<int, ApiObject> Apis = [];

        // Dictionaries for keeping track of Segments by distance
        public Dictionary<int, List<Segment>> DistanceSegmentOrder = [];
        public Dictionary<int, Segment> SegmentByIdDictionary = [];

        // HashSet to keep track of chips & bibs of DNS entries.
        public HashSet<string> DnsChips = [];
        public HashSet<string> DnsBibs = [];
        public int DnsEntryCount = 0;
    }
}
