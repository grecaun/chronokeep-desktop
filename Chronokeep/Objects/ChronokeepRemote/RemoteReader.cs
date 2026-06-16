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
