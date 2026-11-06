using Chronokeep.Objects;
using Chronokeep.Objects.ChronoKeepAPI;
using System.Text.Json;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Logic
{
    /// <summary>What gets sent to the Chronokeep results website.</summary>
    public class ApiPayloadTests
    {
        private static readonly string EventDate = new DateTime(2026, 6, 15).ToShortDateString();
        private static readonly DateTime Start = new(2026, 6, 15, 8, 0, 0, DateTimeKind.Local);

        private static Event TheEvent() => new() { Identifier = 1, Name = "Race", Date = EventDate };

        private static Participant Runner(bool anonymous = false, string birthday = "06/16/1990", string gender = "Woman")
            => new(7, "Jane", "Runner", "", "", "", "", birthday,
                new EventSpecific(42, 1, 3, "5K", "101", 0, "", "", "", 0, "30-39", 5, anonymous, true, "M", "Elite", 0, 0),
                "", "", "555-0101", "", "", "", gender, "", "");

        private static TimeResult Result(long seconds = 3725, int ms = 5, int status = TimingConstants.TIMERESULT_STATUS_NONE,
            int segment = TimingConstants.SEGMENT_FINISH, Participant? p = null)
        {
            var r = new TimeResult(1, 9, 42, TimingConstants.LOCATION_FINISH, segment, 1, seconds, ms, "Bib:101",
                seconds - 10, ms, Start.AddSeconds(seconds), "101", status, "Elite");
            r.SetParticipant(p ?? Runner());
            r.Place = 3;
            r.AgePlace = 2;
            r.GenderPlace = 1;
            r.DivisionPlace = 4;
            return r;
        }

        [Fact]
        public void ApiResult_CopiesResultDetails()
        {
            var api = new ApiResult(TheEvent(), Result(), Start, "XYZ");

            Assert.Equal("42-XYZ", api.PersonId);
            Assert.Equal("101", api.Bib);
            Assert.Equal("Jane", api.First);
            Assert.Equal("Runner", api.Last);
            Assert.Equal(35, api.Age);
            Assert.Equal("Woman", api.Gender);
            Assert.Equal("30-39", api.AgeGroup);
            Assert.Equal("5K", api.Distance);
            Assert.Equal(3725, api.Seconds);
            Assert.Equal(5, api.Milliseconds);
            Assert.Equal(3715, api.ChipSeconds);
            Assert.Equal((3, 2, 1, 4), (api.Ranking, api.AgeRanking, api.GenderRanking, api.DivisionRanking));
            Assert.Equal("Elite", api.Division);
            Assert.True(api.Finish);
            Assert.Equal(TimingConstants.DISTANCE_TYPE_NORMAL, api.Type);
        }

        [Fact]
        public void ApiResult_LocalTimeIsStartPlusGunTime()
        {
            var api = new ApiResult(TheEvent(), Result(3725, 5), Start, "XYZ");
            Assert.Equal(Start.AddSeconds(3725).AddMilliseconds(5), DateTime.Parse(api.LocalTime));
        }

        [Fact]
        public void ApiResult_AnonymousRunner_HasNoName()
        {
            var api = new ApiResult(TheEvent(), Result(p: Runner(anonymous: true)), Start, "XYZ");
            Assert.Equal("", api.First);
            Assert.Equal("", api.Last);
            Assert.True(api.Anonymous);
        }

        [Fact]
        public void ApiResult_NotAFinish_IsMarked()
        {
            var api = new ApiResult(TheEvent(), Result(segment: 12), Start, "XYZ");
            Assert.False(api.Finish);
        }

        [Fact]
        public void ApiResult_Dnf_HasDnfTypeAndNoTime()
        {
            var api = new ApiResult(TheEvent(), Result(status: TimingConstants.TIMERESULT_STATUS_DNF), Start, "XYZ");
            Assert.Equal(TimingConstants.API_TYPE_DNF, api.Type);
            Assert.Equal(0, api.Seconds);
            Assert.Equal(0, api.Milliseconds);
        }

        [Fact]
        public void ApiResult_Dns_HasDnsTypeAndNoTime()
        {
            var api = new ApiResult(TheEvent(), Result(status: TimingConstants.TIMERESULT_STATUS_DNS), Start, "XYZ");
            Assert.Equal(TimingConstants.API_TYPE_DNS, api.Type);
            Assert.Equal(0, api.Seconds);
        }

        [Fact]
        public void ApiResult_UnknownBirthday_HasNegativeAge()
        {
            var api = new ApiResult(TheEvent(), Result(p: Runner(birthday: "")), Start, "XYZ");
            Assert.Equal(-1, api.Age);
        }

        [Fact]
        public void ApiResult_SerializesWithApiFieldNames()
        {
            string json = JsonSerializer.Serialize(new ApiResult(TheEvent(), Result(), Start, "XYZ"));
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.Equal("42-XYZ", root.GetProperty("person_id").GetString());
            Assert.Equal(3725, root.GetProperty("seconds").GetInt32());
            Assert.Equal(1, root.GetProperty("gender_ranking").GetInt32());
            Assert.True(root.GetProperty("finish").GetBoolean());
            Assert.Equal("Elite", root.GetProperty("division").GetString());
        }

        [Fact]
        public void ApiPerson_CopiesParticipantDetails()
        {
            var person = new ApiPerson(Runner(), "XYZ");

            Assert.Equal("XYZ42", person.Identifier);
            Assert.Equal("101", person.Bib);
            Assert.Equal("Jane", person.First);
            Assert.Equal("06/16/1990", person.Birthdate);
            Assert.Equal("Woman", person.Gender);
            Assert.Equal("30-39", person.AgeGroup);
            Assert.Equal("5K", person.Distance);
            Assert.True(person.SmsEnabled);
            Assert.Equal("555-0101", person.Mobile);
            Assert.Equal("M", person.Apparel);
        }

        [Fact]
        public void ApiPerson_Anonymous_HasNoName()
        {
            var person = new ApiPerson(Runner(anonymous: true), "XYZ");
            Assert.Equal("", person.First);
            Assert.Equal("", person.Last);
            Assert.True(person.Anonymous);
        }

        [Fact]
        public void ApiPerson_UnknownBirthday_UsesPlaceholder()
        {
            Assert.Equal("1901/01/01", new ApiPerson(Runner(birthday: ""), "XYZ").Birthdate);
        }

        [Fact]
        public void ApiPerson_TrimAndFormat()
        {
            var person = new ApiPerson { Bib = " 7 ", First = " Jane ", Birthdate = "garbage" };
            person.Trim();
            person.FormatData();
            Assert.Equal("7", person.Bib);
            Assert.Equal("Jane", person.First);
            Assert.Equal(new DateTime(DateTime.Now.Year - 130, 1, 1).ToShortDateString(), person.Birthdate);
        }
    }
}
