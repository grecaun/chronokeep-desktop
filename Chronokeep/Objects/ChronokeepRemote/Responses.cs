using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Chronokeep.Objects.ChronokeepRemote
{
    /**
     * Responses for read requests
     */
    public class GetReadsResponse
    {
        [JsonPropertyName("count")]
        public long Count { get; init; }
        [JsonPropertyName("reads")]
        public List<RemoteRead> Reads { get; init; } = [];
        [JsonPropertyName("notification")]
        public RemoteNotification Notification { get; init; } = new();
    }

    public class DeleteReadsResponse
    {
        [JsonPropertyName("count")]
        public long Count { get; init; }
    }

    /*
     * Response for readers request.
     */
    public class GetReadersResponse
    {
        [JsonPropertyName("readers")]
        public List<RemoteReader> Readers { get; init; } = [];
    }
}
