using System.Text.Json.Serialization;

namespace Chronokeep.Objects.ChronokeepRemote
{
    public class RemoteRead
    {
        // Identifier is either a BIB or a CHIP value.
        [JsonPropertyName("identifier")]
        public string Identifier { get; set; } = "";
        [JsonPropertyName("seconds")]
        public long Seconds { get; set; }
        [JsonPropertyName("milliseconds")]
        public int Milliseconds { get; set; }
        [JsonPropertyName("ident_type")]
        public IdentType IdentType { get; set; }
        [JsonPropertyName("type")]
        public ReadType Type { get; set; }
        [JsonPropertyName("antenna")]
        public int Antenna { get; set; }
        [JsonPropertyName("reader")]
        public string Reader { get; set; } = "";
        [JsonPropertyName("rssi")]
        public string Rssi { get; set; } = "";

        public ChipRead ConvertToChipRead(int eventId, int locationId)
        {
            return new ChipRead(
                eventId,
                locationId,
                IdentType == IdentType.chip ? Identifier : Constants.Timing.CHIPREAD_DUMMYCHIP,
                IdentType == IdentType.bib ? Identifier : Constants.Timing.CHIPREAD_DUMMYBIB,
                Seconds,
                Milliseconds,
                Antenna,
                Reader,
                Rssi,
                IdentType == IdentType.chip ? Constants.Timing.CHIPREAD_TYPE_CHIP : Constants.Timing.CHIPREAD_TYPE_MANUAL
                );
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum IdentType
    {
        bib,
        chip,
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ReadType
    {
        reader,
        manual,
    }
}
