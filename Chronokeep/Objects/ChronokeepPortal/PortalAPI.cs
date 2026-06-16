using System.Text.Json.Serialization;

namespace Chronokeep.Objects.ChronokeepPortal
{
    public class PortalApi
    {
        public const string API_TYPE_CHRONOKEEP_REMOTE        = "CHRONOKEEP_REMOTE";
        public const string API_TYPE_CHRONOKEEP_REMOTE_SELF   = "CHRONOKEEP_REMOTE_SELF";
        public const string API_URI_CHRONOKEEP_REMOTE         = @"https://remote.chronokeep.com/";

        [JsonPropertyName("id")]
        public long Id { get; init; }
        [JsonPropertyName("nickname")]
        public string Nickname { get; set; } = "";
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "";
        [JsonPropertyName("token")]
        public string Token { get; set; } = "";
        [JsonPropertyName("uri")]
        public string Uri { get; set; } = "";
    }
}
