using System.Text.Json.Serialization;

namespace Chronokeep.Objects.ChronokeepRemote
{
    public class RemoteNotification
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
        [JsonPropertyName("type")]
        public string Type { get; init; } = "";
        [JsonPropertyName("when")]
        public string When { get; init; } = "";
        [JsonPropertyName("message")]
        public string Message { get; init; } = "";
    }
}
