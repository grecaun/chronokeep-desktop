using System;
using System.Text.Json.Serialization;

namespace Chronokeep.Objects.ChronoKeepAPI
{
    public class ApiEvent
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("cert_name")]
        public string CertificateName { get; set; } = "";
        [JsonPropertyName("slug")]
        public string Slug { get; set; } = "";
        [JsonPropertyName("website")]
        public string Website { get; set; } = "";
        [JsonPropertyName("image")]
        public string Image { get; set; } = "";
        [JsonPropertyName("contact_email")]
        public string ContactEmail { get; set; } = "";
        [JsonPropertyName("access_restricted")]
        public bool AccessRestricted { get; set; }
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";
        [JsonPropertyName("recent_time")]
        public string? RecentTime { get; set; }

        public int CompareTo(ApiEvent other)
        {
            DateTime oneDate, twoDate;
            try
            {
                oneDate = DateTime.Parse(RecentTime!);
            }
            catch
            {
                oneDate = DateTime.Now;
            }
            try
            {
                twoDate = DateTime.Parse(other.RecentTime!);
            }
            catch
            {
                twoDate = DateTime.Now;
            }
            return oneDate.CompareTo(twoDate);
        }
    }
}
