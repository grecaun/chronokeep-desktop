using System.Text.Json.Serialization;

namespace Chronokeep.Objects.ChronoKeepAPI
{
    public class ApiEventYear
    {
        [JsonPropertyName("year")]
        public string Year { get; set; } = "";
        [JsonPropertyName("date_time")]
        public string DateTime { get; set; } = "";
        [JsonPropertyName("live")]
        public bool Live { get; set; }
        [JsonPropertyName("days_allowed")]
        public int DaysAllowed { get; set; } = 1;
        [JsonPropertyName("ranking_type")]
        public string RankingType { get; set; } = "gun";
    }
}
