using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Chronokeep.Objects.ChronoKeepAPI
{
    /*
     * 
     * Classes for dealing with ChronoKeep API responses.
     * 
     */

    // Event specific responses.
    public class GetEventsResponse
    {
        [JsonPropertyName("events")]
        public List<ApiEvent> Events { get; init; } = [];
    }

    public class GetEventResponse
    {
        [JsonPropertyName("event")]
        public ApiEvent Event { get; init; } = new();
        [JsonPropertyName("event_years")]
        public List<ApiEventYear> EventYears { get; init; } = [];
        [JsonPropertyName("year")]
        public ApiEventYear Year { get; init; } = new();
        [JsonPropertyName("participants")]
        public List<ApiPerson> Participants { get; init; } = [];
    }

    public class ModifyEventResponse
    {
        [JsonPropertyName("event")]
        public ApiEvent Event { get; init; } = new();
    }

    // Event Year specific responses.
    public class GetEventYearsResponse
    {
        [JsonPropertyName("years")]
        public List<ApiEventYear> EventYears { get; init; } = [];
    }

    public class EventYearResponse
    {
        [JsonPropertyName("event")]
        public ApiEvent Event { get; init; } = new();
        [JsonPropertyName("event_year")]
        public ApiEventYear EventYear { get; init; } = new();
    }

    // Results specific responses.
    public class AddResultsResponse
    {
        [JsonPropertyName("count")]
        public int Count { get; init; }
    }

    // Error response.
    public class ErrorResponse
    {
        [JsonPropertyName("message")]
        public string Message { get; init; } = "";
    }

    // Banned emails/phone number responses
    public class GetBannedPhonesResponse
    {
        [JsonPropertyName("phones")]
        public List<string> Phones { get; init; } = [];
    }

    public class GetBannedEmailsResponse
    {
        [JsonPropertyName("emails")]
        public List<string> Emails { get; init; } = [];
    }

    // Participants responses
    public class GetParticipantsResponse
    {
        [JsonPropertyName("event")]
        public ApiEvent Event { get; init; } = new();
        [JsonPropertyName("year")]
        public ApiEventYear Year { get; init; } = new();
        [JsonPropertyName("participants")]
        public List<ApiPerson> Participants { get; init; } = [];
    }

    // BibChips responses
    public class GetBibChipsResponse
    {
        [JsonPropertyName("bib_chips")]
        public List<BibChip> BibChips { get; init; } = [];
    }

    // SMS Subscription responses
    public class GetSmsSubscriptionsResponse
    {
        [JsonPropertyName("subscriptions")]
        public List<ApiSmsSubscription> Subscriptions { get; init; } = [];
    }

    // Segment responses
    public class GetSegmentsResponse
    {
        [JsonPropertyName("segments")]
        public List<ApiSegment> Segments { get; set; } = [];
    }
    public class AddSegmentsResponse
    {
        [JsonPropertyName("segments")]
        public List<ApiSegment> Segments { get; init; } = [];
    }
    public class DeleteSegmentsResponse
    {
        [JsonPropertyName("count")]
        public long Count { get; init; }
    }

    // Distance responses
    public class GetDistancesResponse
    {
        [JsonPropertyName("distances")]
        public List<ApiDistance> Distances { get; init; } = [];
    }
    public class DeleteDistancesResponse
    {
        [JsonPropertyName("count")]
        public long Count { get; init; }
    }
}
