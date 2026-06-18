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
        // Participants are stored based upon BIB and EVENT SPECIFIC IDENTIFIER because we use both
        public readonly Dictionary<string, Participant> ParticipantBibDictionary = [];
        public readonly Dictionary<int, Participant> ParticipantEventSpecificDictionary = [];
        // Start times. Item at 0 should always be 00:00:00.000. Key is Distance ID
        public readonly Dictionary<int, (long Seconds, int Milliseconds)> DistanceStartDict = [];
        public readonly Dictionary<int, (long Seconds, int Milliseconds)> DistanceEndDict = [];
        public readonly Dictionary<int, Distance> DistanceDictionary = [];
        public readonly Dictionary<string, Distance> DistanceNameDictionary = [];

        // Link bibs and chip reads for adding occurence to bib based dnf entry.
        // We changed the database to allow multiple chips per bib.
        public readonly Dictionary<string, List<string>> BibToChipDictionary = [];
        public readonly Dictionary<string, string> ChipToBibDictionary = [];

        // Linked distance dictionaries
        public readonly Dictionary<string, (Distance, int)> LinkedDistanceDictionary = [];
        public readonly Dictionary<int, int> LinkedDistanceIdentifierDictionary = [];

        // HashSet for non-linked distances.
        public readonly HashSet<Distance> MainDistances = [];
        public readonly Dictionary<int, ApiObject> Apis = [];

        // Dictionaries for keeping track of Segments by distance
        public readonly Dictionary<int, List<Segment>> DistanceSegmentOrder = [];
        public readonly Dictionary<int, Segment> SegmentByIdDictionary = [];

        // HashSet to keep track of chips & bibs of DNS entries.
        public readonly HashSet<string> DnsChips = [];
        public readonly HashSet<string> DnsBibs = [];
        public int DnsEntryCount = 0;
    }
}
