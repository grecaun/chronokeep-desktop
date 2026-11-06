using Chronokeep.IO.HtmlTemplates;
using Chronokeep.Objects;
using System.Reflection;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Logic
{
    public class CertificateEmailTemplateTests
    {
        private static Event TheEvent(string name = "Spring Run") => new()
        {
            Name = name,
            Date = new DateTime(2026, 6, 15).ToShortDateString(),
            ApiEventId = "spring-run,2026",
        };

        private static TimeResult Result(string first = "Jane", string last = "Runner", string distance = "10K")
        {
            var r = new TimeResult(1, -1, 42, TimingConstants.LOCATION_FINISH, TimingConstants.SEGMENT_FINISH, 1, 1510, 0, "Bib:7",
                1500, 0, DateTime.Now, "7", TimingConstants.TIMERESULT_STATUS_NONE, "");
            r.SetParticipant(new Participant(first, last, "", "", "", "", "",
                new EventSpecific(1, 1, distance, "7", 0, "", "", "", false, false, "", ""), "", "", "", "", "", "", "F", "", ""));
            return r;
        }

        private static string Field(HtmlCertificateEmailTemplate t, string name)
            => (string)typeof(HtmlCertificateEmailTemplate).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(t)!;

        /// <summary>The certificate site's route is /:name/:event/:time/:date and it URL-decodes each part.</summary>
        private static string[] CertificateSegments(HtmlCertificateEmailTemplate t)
        {
            Uri uri = new(Field(t, "certificateUrl"));
            Assert.Equal("cert.chronokeep.com", uri.Host);
            return [.. uri.AbsolutePath.Trim('/').Split('/').Select(Uri.UnescapeDataString)];
        }

        [Fact]
        public void CertificateLink_HasNameEventTimeAndDate()
        {
            var t = new HtmlCertificateEmailTemplate(TheEvent(), Result(), "jane@example.com", false, null);
            Assert.Equal(["Jane Runner", "2026 Spring Run 10K", "0:25:00", TheEvent().LongDate], CertificateSegments(t));
        }

        [Fact]
        public void CertificateLink_SingleDistance_LeavesDistanceOut()
        {
            var t = new HtmlCertificateEmailTemplate(TheEvent(), Result(), "jane@example.com", true, null);
            Assert.Equal("2026 Spring Run", CertificateSegments(t)[1]);
        }

        [Theory]
        [InlineData("Jane", "Runner", "5K Run/Walk", "Spring Run")]
        [InlineData("Jane", "O'Neil #2", "10K", "Spring Run")]
        [InlineData("Ann+Marie", "Runner", "10K", "Spring Run?")]
        [InlineData("Jane", "Runner", "10K", "100% Fun Run")]
        public void CertificateLink_SpecialCharacters_StayInTheirSegment(string first, string last, string distance, string eventName)
        {
            var t = new HtmlCertificateEmailTemplate(TheEvent(eventName), Result(first, last, distance), "jane@example.com", false, null);
            var segments = CertificateSegments(t);
            Assert.Equal(4, segments.Length);
            Assert.Equal($"{first} {last}", segments[0]);
            Assert.Equal($"2026 {eventName} {distance}", segments[1]);
        }

        [Fact]
        public void ResultsLink_UsesTheResultsSiteAndEvent()
        {
            var api = new ApiObject(1, Constants.ApiConstants.CHRONOKEEP_RESULTS, "https://api/", "", "", "https://results.example.com/");
            var t = new HtmlCertificateEmailTemplate(TheEvent(), Result(), "jane@example.com", false, api);
            Assert.Contains("href=\"https://results.example.com/results/spring-run/2026\"", Field(t, "resultsLink"));
            Assert.Contains("unsubscribe/jane@example.com", Field(t, "unsubscribe"));
        }

        [Fact]
        public void ResultsLink_EmptyWithoutAResultsSite()
        {
            var t = new HtmlCertificateEmailTemplate(TheEvent(), Result(), "jane@example.com", false, null);
            Assert.Equal("", Field(t, "resultsLink"));
        }
    }

    public class PrintableTemplateTests
    {
        private static TimeResult Result()
        {
            var r = new TimeResult(1, -1, 42, TimingConstants.LOCATION_FINISH, TimingConstants.SEGMENT_FINISH, 1, 1510, 0, "Bib:7",
                1500, 0, DateTime.Now, "7", TimingConstants.TIMERESULT_STATUS_NONE, "");
            r.SetParticipant(new Participant("Jane", "Runner", "", "", "", "", "",
                new EventSpecific(1, 1, "5K", "7", 0, "", "", "", false, false, "", ""), "", "", "", "", "", "", "Woman", "", ""));
            return r;
        }

        private static string Field(HtmlPrintableTemplate t, string name)
            => (string)typeof(HtmlPrintableTemplate).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(t)!;

        [Fact]
        public void Pace_IsChipTimePerDistanceUnit()
        {
            var d = new Distance("5K", 1) { DistanceValue = 3.0, DistanceUnit = Constants.Distances.MILES };
            var t = new HtmlPrintableTemplate(Result(), 10, 5, 2, d);
            Assert.Equal("08:20", Field(t, "pace"));          // 1500 s / 3 miles
            Assert.Equal("min/mile", Field(t, "paceStr"));
            Assert.Equal("miles", Field(t, "distanceValueStr"));
            Assert.Equal("Women", Field(t, "GenderStr"));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        public void Pace_WithoutADistanceValue_IsBlank(double value)
        {
            var d = new Distance("5K", 1) { DistanceValue = value };
            var t = new HtmlPrintableTemplate(Result(), 10, 5, 2, d);
            Assert.Equal("", Field(t, "pace"));
        }

        [Fact]
        public void Pace_WithoutADistance_IsBlank()
        {
            var t = new HtmlPrintableTemplate(Result(), 10, 5, 2, null);
            Assert.Equal("", Field(t, "pace"));
            Assert.Equal("", Field(t, "paceStr"));
        }

        [Fact]
        public void Renders()
        {
            var d = new Distance("5K", 1) { DistanceValue = 3.1, DistanceUnit = Constants.Distances.MILES };
            string html = new HtmlPrintableTemplate(Result(), 10, 5, 2, d).TransformText();
            Assert.Contains("Jane", html);
            Assert.Contains("min/mile", html);
        }
    }

    public class ResultsTemplateTests
    {
        [Fact]
        public void GroupsResultsByDistance_AndLinksParticipants()
        {
            var e = new Event { Name = "Spring Run", Date = new DateTime(2026, 6, 15).ToShortDateString() };
            List<TimeResult> results = [];
            foreach (var (bib, first, distance) in new[] { ("7", "Jane", "5K"), ("8", "Bob", "10K") })
            {
                var r = new TimeResult(1, -1, int.Parse(bib), TimingConstants.LOCATION_FINISH, TimingConstants.SEGMENT_FINISH, 1, 1500, 0,
                    TimeResult.BibToIdentifier(bib), 1500, 0, DateTime.Now, bib, TimingConstants.TIMERESULT_STATUS_PROCESSED, "");
                r.SetParticipant(new Participant(first, "Runner", "", "", "", "", "",
                    new EventSpecific(1, 1, distance, bib, 0, "", "", "", false, false, "", ""), "", "", "", "", "", "", "F", "", ""));
                r.Place = 1;
                results.Add(r);
            }

            string html = new HtmlResultsTemplate(e, results, linkPart: true).TransformText();

            Assert.Contains("Jane", html);
            Assert.Contains("Bob", html);
            Assert.Contains("5K", html);
            Assert.Contains("10K", html);
            Assert.Contains("part/7", html);
        }
    }

    public class SmallObjectBehaviorTests
    {
        [Theory]
        [InlineData(1, 10, true)]
        [InlineData(10, 10, true)]
        [InlineData(11, 10, false)]
        public void Range_IsValid(int start, int end, bool expected)
        {
            Assert.Equal(expected, new Objects.Range { StartBib = start, EndBib = end }.IsValid());
        }

        [Theory]
        [InlineData(1, 10, 100, 109, 5, 15, 200, 210, true)]    // bibs overlap
        [InlineData(1, 10, 100, 109, 11, 20, 105, 114, true)]   // chips overlap
        [InlineData(1, 10, 100, 109, 11, 20, 110, 119, false)]  // neither
        [InlineData(5, 6, 100, 101, 1, 10, 200, 209, true)]     // one bib range inside the other
        public void Range_Violates(int s1, int e1, int c1, int d1, int s2, int e2, int c2, int d2, bool expected)
        {
            var a = new Objects.Range { StartBib = s1, EndBib = e1, StartChip = c1, EndChip = d1 };
            var b = new Objects.Range { StartBib = s2, EndBib = e2, StartChip = c2, EndChip = d2 };
            Assert.Equal(expected, a.Violates(b));
            Assert.Equal(expected, b.Violates(a));
        }

        [Fact]
        public void AnnouncerParticipant_ShowsTimeNameAndAge()
        {
            var p = new Participant("Jane", "Runner", "", "Eugene", "OR", "", new DateTime(1990, 6, 16).ToShortDateString(),
                new EventSpecific(1, 1, "5K", "7", 0, "Go Jane!", "", "", false, false, "", ""), "", "", "", "", "", "", "F", "", "");
            long seconds = TimingConstants.RfidDateToEpoch(new DateTime(2026, 6, 15, 8, 25, 3));
            AnnouncerParticipant.TheEvent = new Event { Date = new DateTime(2026, 6, 15).ToShortDateString() };
            try
            {
                var a = new AnnouncerParticipant(p, seconds);
                Assert.Equal("08:25:03", a.AnnouncerWhen);
                Assert.Equal("Jane Runner", a.ParticipantName);
                Assert.Equal("Eugene OR", a.CityState);
                Assert.Equal("35 F", a.AgeGender);
                Assert.Equal("Go Jane!", a.Comments);
                Assert.True(a.CompareTo(new AnnouncerParticipant(p, seconds + 1)) < 0);
            }
            finally
            {
                AnnouncerParticipant.TheEvent = null;
            }
        }

        [Fact]
        public void DistanceStat_TotalAddsEveryStatus()
        {
            Assert.Equal(10, new DistanceStat { Active = 1, Finished = 2, Dnf = 3, Dns = 4 }.Total);
        }
    }
}
