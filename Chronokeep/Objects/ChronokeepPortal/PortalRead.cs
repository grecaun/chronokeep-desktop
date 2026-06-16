using System.Text.Json.Serialization;

namespace Chronokeep.Objects.ChronokeepPortal
{
    public class PortalRead
    {
        public const string READ_KIND_CHIP         = "reader";
        public const string READ_KIND_MANUAL       = "manual";
        public const string READ_IDENT_TYPE_CHIP   = "chip";
        public const string READ_IDENT_TYPE_BIB    = "bib";

        [JsonPropertyName("identifier")]
        public string Identifier { get; set; } = "";
        [JsonPropertyName("seconds")]
        public long Seconds { get; set; }
        [JsonPropertyName("milliseconds")]
        public int Milliseconds { get; set; }
        [JsonPropertyName("reader_seconds")]
        public long ReaderSeconds { get; set; }
        [JsonPropertyName("reader_milliseconds")]
        public int ReaderMilliseconds { get; set; }
        [JsonPropertyName("antenna")]
        public int Antenna { get; set; }
        [JsonPropertyName("reader")]
        public string Reader { get; set; } = "";
        [JsonPropertyName("rssi")]
        public string Rssi { get; set; } = "";
        [JsonPropertyName("ident_type")]
        public string IdentType { get; set; } = "";
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";
    }
}
