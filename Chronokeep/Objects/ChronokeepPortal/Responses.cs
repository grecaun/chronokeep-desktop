using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Chronokeep.Objects.ChronokeepPortal
{
    public class Response
    {
        public const string READERS               = "readers";
        public const string READER_ANTENNAS       = "reader_antennas";
        public const string ERROR                 = "error";
        public const string SETTINGS              = "settings";
        public const string SETTINGS_ALL          = "settings_all";
        public const string API_LIST              = "api_list";
        public const string READS                 = "reads";
        public const string SUCCESS               = "success";
        public const string TIME                  = "time";
        public const string READ_AUTO_UPLOAD      = "read_auto_upload";
        public const string CONNECTION_SUCCESSFUL = "connection_successful";
        public const string KEEPALIVE             = "keepalive";
        public const string DISCONNECT            = "disconnect";
        public const string NOTIFICATION          = "notification";

        [JsonPropertyName("command")]
        public string Command { get; init; } = "";
    }

    public class ReadersResponse : Response
    {
        [JsonPropertyName("readers")]
        public List<PortalReader> List { get; init; } = [];
    }

    public class ReaderAntennasResponse : Response
    {
        [JsonPropertyName("reader_name")]
        public string ReaderName { get; init; } = "";
        [JsonPropertyName("antennas")]
        public int[] Antennas { get; init; } = [];
    }

    public class ErrorResponse : Response
    {
        [JsonPropertyName("error")]
        public PortalError Value { get; init; } = new();
    }

    public class SettingsResponse : Response
    {
        [JsonPropertyName("settings")]
        public List<PortalSetting> List { get; init; } = [];
    }

    public class SettingsAllResponse : Response
    {
        [JsonPropertyName("settings")]
        public List<PortalSetting> Settings { get; init; } = [];
        [JsonPropertyName("readers")]
        public List<PortalReader> Readers { get; init; } = [];
        [JsonPropertyName("apis")]
        public List<PortalApi> ApIs { get; init; } = [];
        [JsonPropertyName("auto_upload")]
        public PortalStatus AutoUpload { get; init; }
        [JsonPropertyName("portal_version")]
        public string PortalVersion { get; init; } = "";
    }

    public class ApiListResponse : Response
    {
        [JsonPropertyName("apis")]
        public List<PortalApi> List { get; init; } = [];
    }

    public class ReadsResponse : Response
    {
        [JsonPropertyName("list")]
        public List<PortalRead> List { get; init; } = [];
    }

    public class SuccessResponse : Response
    {
        [JsonPropertyName("count")]
        public ulong Count { get; set; }
    }

    public class TimeResponse : Response
    {
        [JsonPropertyName("local")]
        public string Local { get; init; } = "";
        [JsonPropertyName("utc")]
        public string Utc { get; init; } = "";
    }

    public class EventsResponse : Response
    {
        [JsonPropertyName("events")]
        public List<PortalEvent> List { get; set; } = [];
    }

    public class EventYearsResponse : Response
    {
        [JsonPropertyName("years")]
        public List<string> Years { get; set; } = [];
    }

    public class ReadAutoUploadResponse : Response
    {
        [JsonPropertyName("status")]
        public PortalStatus Status { get; init; }
    }

    public class ConnectionSuccessfulResponse : Response
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";
        [JsonPropertyName("kind")]
        public string Type { get; init; } = "";
        [JsonPropertyName("version")]
        public ulong Version { get; init; }
        [JsonPropertyName("reads_subscribed")]
        public bool ReadsSubscribed { get; init; }
        [JsonPropertyName("readers")]
        public List<PortalReader> Readers { get; init; } = [];
        [JsonPropertyName("updatable")]
        public bool Updateable { get; init; }
        [JsonPropertyName("auto_upload")]
        public PortalStatus AutoUpload { get; init; }
        [JsonPropertyName("portal_version")]
        public string PortalVersion { get; init; } = "";
    }

    public class NotificationResponse : Response
    {
        [JsonPropertyName("kind")]
        public string Type { get; init; } = "";
        [JsonPropertyName("time")]
        public string Time { get; init; } = "";
    }
}
