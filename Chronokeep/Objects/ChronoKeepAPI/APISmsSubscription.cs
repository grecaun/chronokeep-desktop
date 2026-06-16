using System.Text.Json.Serialization;

namespace Chronokeep.Objects.ChronoKeepAPI
{
    public class ApiSmsSubscription
    {
        [JsonPropertyName("bib")]
        public string Bib { get; init; } = "";
        [JsonPropertyName("first")]
        public string First { get; init; } = "";
        [JsonPropertyName("last")]
        public string Last { get; init; } = "";
        [JsonPropertyName("phone")]
        public string Phone { get; init; } = "";
    }
}
