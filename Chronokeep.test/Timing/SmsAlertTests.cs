using Chronokeep.Objects;
using Chronokeep.Objects.ChronoKeepAPI;
using Chronokeep.Test.Database;
using Chronokeep.Timing;
using static Chronokeep.Objects.TimeResult;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Timing
{
    /// <summary>Who gets a text message for a result, and what happens when it's sent. No real messages are sent.</summary>
    internal static class SmsData
    {
        public static readonly Event TheEvent = new() { Identifier = 1, Name = "Spring Run", Date = new DateTime(2026, 6, 15).ToShortDateString(), ApiEventId = "spring-run,2026" };

        public static TimeResult R(int esId, string bib, int segment = TimingConstants.SEGMENT_FINISH, string distance = "5K",
            DateTime? when = null, bool anonymous = false, string first = "Jane", string last = "Runner")
        {
            TimeResult r = new(1, -1, esId, TimingConstants.LOCATION_FINISH, segment, 1, 1500, 0, TimeResult.BibToIdentifier(bib),
                1500, 0, when ?? DateTime.Now, bib, TimingConstants.TIMERESULT_STATUS_PROCESSED, "");
            r.SetParticipant(new Participant(first, last, "", "", "", "", "",
                new EventSpecific(1, 1, distance, bib, 0, "", "", "", anonymous, true, "", ""), "", "", "", "", "", "", "F", "", ""));
            return r;
        }

        public static Participant P(string bib, string first, string last)
            => new(first, last, "", "", "", "", "", new EventSpecific(1, 1, "5K", bib, 0, "", "", "", false, true, "", ""), "", "", "", "", "", "", "F", "", "");
    }

    public class SmsSelectionTests
    {
        private static readonly Dictionary<string, Distance> Distances = new()
        {
            ["5K"] = new Distance("5K", 1) { SmsEnabled = true },
            ["10K"] = new Distance("10K", 1) { SmsEnabled = false },
        };

        private static List<TimeResult> Pick(List<TimeResult> results, HashSet<(int, int)>? sent = null)
            => TimingWorker.GetSmsResults(results, Distances, sent ?? [], DateTime.Now.AddMinutes(-15));

        [Fact]
        public void FinishAndSplitResults_InTextEnabledDistances()
        {
            TimeResult finish = SmsData.R(1, "1");
            TimeResult split = SmsData.R(2, "2", segment: 12);
            Assert.Equal([finish, split], Pick([finish, split]));
        }

        [Fact]
        public void LeavesOutWhatShouldntGetAText()
        {
            List<TimeResult> results =
            [
                SmsData.R(1, "1", segment: TimingConstants.SEGMENT_START),
                SmsData.R(2, "2", segment: TimingConstants.SEGMENT_NONE),
                SmsData.R(3, "3", distance: "10K"),
                SmsData.R(4, "4", distance: "Marathon"),
                SmsData.R(TimingConstants.TIMERESULT_DUMMYPERSON, "5"),
                SmsData.R(6, "6", when: DateTime.Now.AddMinutes(-20)),
                SmsData.R(7, "7"),
            ];
            Assert.Empty(Pick(results, [(7, TimingConstants.SEGMENT_FINISH)]));
        }

        [Fact]
        public void AlreadySentForOneSegment_StillSendsForAnother()
        {
            TimeResult split = SmsData.R(7, "7", segment: 12);
            Assert.Equal([split], Pick([SmsData.R(7, "7"), split], [(7, TimingConstants.SEGMENT_FINISH)]));
        }

        [Fact]
        public void NameToBib_IgnoresCaseAndNonLetters()
        {
            Dictionary<string, string> names = TimingWorker.GetNameToBib([SmsData.P("7", "Mary-Ann", "O'Neil")]);
            Assert.Equal("7", names["maryannoneil"]);
        }

        [Fact]
        public void NameToBib_LeavesOutSharedNames()
        {
            Dictionary<string, string> names = TimingWorker.GetNameToBib([SmsData.P("1", "John", "Smith"), SmsData.P("2", "john", "SMITH"), SmsData.P("3", "Jane", "Doe")]);
            Assert.False(names.ContainsKey("johnsmith"));
            Assert.Equal("3", names["janedoe"]);
        }

        [Fact]
        public void BibToPhones_ByBibOrByName()
        {
            Dictionary<string, string> names = new() { ["janerunner"] = "7" };
            List<ApiSmsSubscription> subs =
            [
                new() { Bib = "7", Phone = "555-555-0100" },
                new() { First = "Jane", Last = "Runner", Phone = "(555) 555-0101" },
                new() { Bib = "8", Phone = "+15555550102" },
                new() { Bib = "9", Phone = "not a phone" },
                new() { First = "Nobody", Last = "Here", Phone = "5555550103" },
            ];

            Dictionary<string, HashSet<string>> phones = TimingWorker.GetBibToPhones(subs, names);

            Assert.Equal(["+15555550100", "+15555550101"], phones["7"].Order());
            Assert.Equal(["+15555550102"], phones["8"]);
            Assert.Equal(["7", "8"], phones.Keys.Order());
        }

        [Fact]
        public void BibToPhones_SamePhoneTwice_IsTextedOnce()
        {
            List<ApiSmsSubscription> subs = [new() { Bib = "7", Phone = "5555550100" }, new() { Bib = "7", Phone = "555-555-0100" }];
            Assert.Single(TimingWorker.GetBibToPhones(subs, [])["7"]);
        }

        [Fact]
        public void PhonesToText_OnlyResultsWithSubscribers()
        {
            TimeResult jane = SmsData.R(1, "7");
            TimeResult bob = SmsData.R(2, "8");
            Dictionary<TimeResult, HashSet<string>> toText = TimingWorker.GetPhonesToText([jane, bob], new() { ["7"] = ["+15555550100"] });
            Assert.Equal([jane], toText.Keys);
            Assert.Equal(["+15555550100"], toText[jane]);
        }

        [Theory]
        [InlineData("https://results.example.com/", "spring-run,2026", " More results @ https://results.example.com/results/spring-run/2026.")]
        [InlineData("https://results.example.com/", "", " More results @ https://results.example.com/.")]
        [InlineData("", "spring-run,2026", "")]
        public void ResultsUrl(string webUrl, string apiEventId, string expected)
        {
            Event e = new() { ApiEventId = apiEventId };
            ApiObject api = new(1, Constants.ApiConstants.CHRONOKEEP_RESULTS, "https://api/", "", "", webUrl);
            Assert.Equal(expected, TimingWorker.GetResultsUrl(e, api));
        }

        [Fact]
        public void ResultsUrl_NoApi()
        {
            Assert.Equal("", TimingWorker.GetResultsUrl(new Event(), null));
        }
    }

    public class SmsSendingTests : DatabaseTestBase
    {
        private readonly Event theEvent;
        private readonly List<(string Phone, string Message)> sent = [];
        private readonly List<string> banned = [];
        private readonly Dictionary<string, SmsState> replies = [];

        public SmsSendingTests()
        {
            theEvent = AddEvent(makeCurrent: true);
        }

        private SmsState FakeSend(string phone, string message)
        {
            sent.Add((phone, message));
            return replies.GetValueOrDefault(phone, SmsState.Success);
        }

        private void Send(Dictionary<TimeResult, HashSet<string>> toSendTo)
            => TimingWorker.SendSmsAlerts(toSendTo, theEvent, false, " More results @ https://x/.", Db, FakeSend, banned.Add);

        private List<(int, int)> Recorded() => Db.GetSmsAlerts(theEvent.Identifier);

        [Fact]
        public void TextsEveryPhone_AndRecordsTheAlert()
        {
            TimeResult jane = SmsData.R(5, "7");
            Send(new() { [jane] = ["+15555550100", "+15555550101"] });

            Assert.Equal(["+15555550100", "+15555550101"], sent.Select(s => s.Phone).Order());
            Assert.All(sent, s => Assert.StartsWith("Jane Runner has finished", s.Message));
            Assert.All(sent, s => Assert.Contains("More results @ https://x/.", s.Message));
            Assert.Equal([(5, TimingConstants.SEGMENT_FINISH)], Recorded());
        }

        [Fact]
        public void Anonymous_IsNotTexted_ButIsRecorded()
        {
            Send(new() { [SmsData.R(5, "7", anonymous: true)] = ["+15555550100"] });
            Assert.Empty(sent);
            Assert.Equal([(5, TimingConstants.SEGMENT_FINISH)], Recorded());
        }

        [Fact]
        public void UnknownRunner_IsSkipped()
        {
            Send(new() { [SmsData.R(TimingConstants.TIMERESULT_DUMMYPERSON, "7")] = ["+15555550100"] });
            Assert.Empty(sent);
            Assert.Empty(Recorded());
        }

        [Fact]
        public void NetworkError_IsTriedAgainLater()
        {
            replies["+15555550100"] = SmsState.NetworkError;
            Send(new() { [SmsData.R(5, "7")] = ["+15555550100"] });
            Assert.Empty(Recorded());
        }

        [Fact]
        public void NetworkErrorForOnePhone_ButAnotherWorked_IsRecorded()
        {
            replies["+15555550100"] = SmsState.NetworkError;
            Send(new() { [SmsData.R(5, "7")] = ["+15555550100", "+15555550101"] });
            Assert.Single(Recorded());
        }

        [Fact]
        public void RejectedPhone_IsBanned()
        {
            replies["+15555550100"] = SmsState.AddToBanned;
            Send(new() { [SmsData.R(5, "7")] = ["+15555550100"] });
            Assert.Equal(["+15555550100"], banned);
            // It wasn't a network problem, so it isn't retried.
            Assert.Single(Recorded());
        }

        [Fact]
        public void TextingNotSetUp_IsTriedAgainLater()
        {
            replies["+15555550100"] = SmsState.NotSetUp;
            Send(new() { [SmsData.R(5, "7")] = ["+15555550100"] });
            Assert.Empty(Recorded());
        }

        [Fact]
        public void InvalidPhone_IsNotRetried()
        {
            // Invalid or banned numbers will never work, so don't keep trying them.
            replies["+15555550100"] = SmsState.Invalid;
            Send(new() { [SmsData.R(5, "7")] = ["+15555550100"] });
            Assert.Single(Recorded());
            Assert.Empty(banned);
        }
    }

    /// <summary>The checks SendSmsAlert makes before it would contact Twilio. Nothing here gets that far.</summary>
    public class SendSmsAlertTests : IDisposable
    {
        private readonly string sid = Constants.GlobalVars.TwilioCredentials.AccountSid;
        private readonly string token = Constants.GlobalVars.TwilioCredentials.AuthToken;
        private readonly string number = Constants.GlobalVars.TwilioCredentials.PhoneNumber;

        public void Dispose()
        {
            Constants.GlobalVars.TwilioCredentials.AccountSid = sid;
            Constants.GlobalVars.TwilioCredentials.AuthToken = token;
            Constants.GlobalVars.TwilioCredentials.PhoneNumber = number;
            Constants.GlobalVars.BannedPhones.Remove("+15555550199");
        }

        private static void SetUp(string sid, string token, string number)
        {
            Constants.GlobalVars.TwilioCredentials.AccountSid = sid;
            Constants.GlobalVars.TwilioCredentials.AuthToken = token;
            Constants.GlobalVars.TwilioCredentials.PhoneNumber = number;
        }

        [Theory]
        [InlineData("", "", "")]
        [InlineData("AC123", "", "+15555550100")]
        [InlineData("", "token", "+15555550100")]
        [InlineData("AC123", "token", "")]
        [InlineData("AC123", "token", "5555550100")]
        public void NotSetUp(string sid, string token, string number)
        {
            SetUp(sid, token, number);
            Assert.Equal(SmsState.NotSetUp, SendSmsAlert("+15555550199", "Hello"));
        }

        [Fact]
        public void InvalidPhone()
        {
            SetUp("AC123", "token", "+15555550100");
            Assert.Equal(SmsState.Invalid, SendSmsAlert("5555550199", "Hello"));
        }

        [Fact]
        public void BannedPhone()
        {
            SetUp("AC123", "token", "+15555550100");
            Constants.GlobalVars.BannedPhones.Add("+15555550199");
            Assert.Equal(SmsState.Invalid, SendSmsAlert("+15555550199", "Hello"));
        }
    }
}
