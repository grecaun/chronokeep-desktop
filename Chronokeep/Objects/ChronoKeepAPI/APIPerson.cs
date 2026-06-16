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
            Bib = person.Bib.ToString();
            First = person.Anonymous ? "" : person.FirstName;
            Last = person.Anonymous ? "" : person.LastName;
            Birthdate = person.Birthdate.Length < 1 ? "1901/01/01" : person.Birthdate;
            Gender = person.Gender;
            AgeGroup = person.EventSpecific.AgeGroupName;
            Distance = person.EventSpecific.DistanceName;
            Anonymous = person.Anonymous;
            SmsEnabled = person.EventSpecific.SMSEnabled;
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
