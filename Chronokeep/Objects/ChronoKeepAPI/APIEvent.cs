/*
Chronokeep Desktop - Race Scoring Software
Copyright (C) 2026 James Sentinella

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

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

