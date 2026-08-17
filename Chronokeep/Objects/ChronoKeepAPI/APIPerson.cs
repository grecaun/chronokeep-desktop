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
    public class ApiPerson
    {
        public ApiPerson() { }

        public ApiPerson(Participant person, string uniqueId)
        {
            Identifier = $"{uniqueId}{person.EventSpecific.Identifier}";
            Bib = person.Bib;
            First = person.Anonymous ? "" : person.FirstName;
            Last = person.Anonymous ? "" : person.LastName;
            Birthdate = person.Birthdate.Length < 1 ? "1901/01/01" : person.Birthdate;
            Gender = person.Gender;
            AgeGroup = person.EventSpecific.AgeGroupName;
            Distance = person.EventSpecific.DistanceName;
            Anonymous = person.Anonymous;
            SmsEnabled = person.EventSpecific.SmsEnabled;
            Mobile = person.Mobile;
            Apparel = person.EventSpecific.Apparel;
        }

        [JsonPropertyName("id")]
        public string Identifier { get; set; } = "";
        [JsonPropertyName("bib")]
        public string Bib { get; set; } = "";
        [JsonPropertyName("first")]
        public string First { get; set; } = "";
        [JsonPropertyName("last")]
        public string Last { get; set; } = "";
        [JsonPropertyName("birthdate")]
        public string Birthdate { get; set; } = "";
        [JsonPropertyName("gender")]
        public string Gender { get; set; } = "";
        [JsonPropertyName("age_group")]
        public string AgeGroup { get; set; } = "";
        [JsonPropertyName("distance")]
        public string Distance { get; set; } = "";
        [JsonPropertyName("anonymous")]
        public bool Anonymous { get; set; }
        [JsonPropertyName("sms_enabled")]
        public bool SmsEnabled { get; set; }
        [JsonPropertyName("mobile")]
        public string Mobile { get; set; } = "";
        [JsonPropertyName("apparel")]
        public string Apparel { get; set; } = "";

        public void Trim()
        {
            Bib = Bib.Trim();
            First = First.Trim();
            Last = Last.Trim();
            Birthdate = Birthdate.Trim();
            Gender = Gender.Trim();
            AgeGroup = AgeGroup.Trim();
            Distance = Distance.Trim();
            Mobile = Mobile.Trim();
            Apparel = Apparel.Trim();
        }

        public void FormatData()
        {
            string dummyYear = $"{DateTime.Now.Year - 130}";
            if (!DateTime.TryParse(Birthdate, out DateTime birthDateTime))
            {
                birthDateTime = DateTime.Parse($"{dummyYear}/01/01");
            }
            Birthdate = birthDateTime.ToShortDateString();
        }
    }
}

