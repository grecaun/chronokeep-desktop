using System.Text.Json.Serialization;

namespace Chronokeep.Objects.Registration
{
    public class Participant
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = "";
        [JsonPropertyName("bib")]
        public string Bib { get; init; } = "";
        [JsonPropertyName("first")]
        public string FirstName { get; init; } = "";
        [JsonPropertyName("last")]
        public string LastName { get; init; } = "";
        [JsonPropertyName("birthdate")]
        public string Birthdate { get; init; } = "";
        [JsonPropertyName("gender")]
        public string Gender { get; init; } = "";
        [JsonPropertyName("distance")]
        public string Distance { get; init; } = "";
        [JsonPropertyName("mobile")]
        public string Mobile { get; init; } = "";
        [JsonPropertyName("sms")]
        public bool SmsEnabled { get; init; }
        [JsonPropertyName("apparel")]
        public string Apparel { get; set; } = "";
    }
}
