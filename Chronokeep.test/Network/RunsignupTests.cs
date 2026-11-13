using Chronokeep.Network.API;
using Chronokeep.Network.Runsignup;
using Chronokeep.Objects;
using Chronokeep.Objects.Runsignup;
using Chronokeep.Test.Database;
using Chronokeep.Test.Timing;
using System.Net;
using System.Text.Json;
using RegistrationState = Chronokeep.Constants.Registration;

namespace Chronokeep.Test.Network
{
    /// <summary>Reading Runsignup's replies, which vary in shape: numbers or strings, one object or a list.</summary>
    public class RunsignupJsonTests
    {
        private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

        [Fact]
        public void Participants_are_read_from_each_event_block()
        {
            List<RsuParticipant> people = RsuJson.Participants(Json("""
                [
                  {"event":{"event_id":101},"participants":[
                    {"registration_id":5001,"bib_num":"12","chip_num":"ABC12","age":36,"team_name":"Hill Hoppers",
                     "giveaway":"Large","giveaway_option_id":7210892,
                     "registration_addons":[{"addon_name":"Pint Glass","quantity":2}],
                     "question_responses":[
                        {"question_id":77,"question_text":"Hometown","response":"Eugene"},
                        {"question_id":78,"question_text":"Running for","responses":["Mom","Dad"]}],
                     "user":{"user_id":900,"first_name":"Ann","last_name":"Lee","dob":"1990-01-02","gender":"F",
                             "email":"ann@example.com","phone":"555-0100","is_anonymous":"T",
                             "address":{"street":"1 Main","city":"Eugene","state":"OR","zipcode":"97401","country_code":"US"}}}
                  ]},
                  {"event":{"event_id":"102"},"participants":[
                    {"registration_id":"5002","user":{"first_name":"Bo","last_name":"Diaz"}}
                  ]}
                ]
                """));

            Assert.Equal(2, people.Count);
            RsuParticipant ann = people[0];
            Assert.Equal(("5001", "101", "900", "12", "ABC12", "36"), (ann.RegistrationId, ann.EventId, ann.UserId, ann.Bib, ann.Chip, ann.Age));
            Assert.Equal(("Ann", "Lee", "1990-01-02", "F"), (ann.FirstName, ann.LastName, ann.Birthdate, ann.Gender));
            Assert.Equal(("1 Main", "Eugene", "OR", "97401", "US"), (ann.Street, ann.City, ann.State, ann.Zip, ann.Country));
            Assert.True(ann.Anonymous);
            Assert.Equal("Hill Hoppers", ann.TeamName);
            Assert.Equal("Large", ann.GiveawayChoice);
            Assert.Equal("Pint Glass x2", Assert.Single(ann.AddOns).ToString());
            Assert.Equal(["Eugene", "Mom, Dad"], ann.Answers.Select(a => a.Response));
            Assert.Equal(("5002", "102"), (people[1].RegistrationId, people[1].EventId));
        }

        [Fact]
        public void Add_ons_give_one_item_per_chosen_option()
        {
            List<RsuParticipant> people = RsuJson.Participants(Json("""
                [{"event":{"event_id":1022170},"participants":[
                  {"registration_id":1,"giveaway":null,"user":{"first_name":"Ann"},
                   "participant_addons":[
                     {"addon_id":"2","addon_name":"T-Shirt","quantity":"1","amount":"$0.00",
                      "participant_addon_options":[{"participant_addon_option_id":"2","participant_addon_option_text":"Small","quantity":"1"}]},
                     {"addon_id":"3","addon_name":"Pint Glass","quantity":"2","amount":"$10.00","participant_addon_options":[]},
                     {"addon_id":"4","addon_name":"Hat","quantity":"3",
                      "participant_addon_options":[{"participant_addon_option_text":"Red","quantity":"2"},{"participant_addon_option_text":"Blue","quantity":"1"}]}]},
                  {"registration_id":2,"giveaway":null,"user":{"first_name":"Bo"}}]}]
                """));

            Assert.Equal(["T-Shirt (Small)", "Pint Glass x2", "Hat (Red) x2", "Hat (Blue)"], people[0].AddOns.Select(a => a.ToString()));
            Assert.Equal("", people[0].GiveawayChoice);
            Assert.Empty(people[1].AddOns);
        }

        [Fact]
        public void A_single_event_can_come_back_as_one_object()
        {
            List<RsuParticipant> people = RsuJson.Participants(Json("""
                {"event":{"event_id":101},"participants":[{"registration_id":1,"user":{"first_name":"Ann"}}]}
                """));

            Assert.Equal("101", Assert.Single(people).EventId);
        }

        [Fact]
        public void Removals_are_read_from_inside_the_event()
        {
            List<RsuRemoval> removals = RsuJson.Removals(Json("""
                {"event":{"event_id":101,"participants":[
                  {"registration_id":5001,"removed_reason":"Transferred","new_registration_id":5009},
                  {"registration_id":5003,"removed_reason":"cleared","new_registration_id":null}]}}
                """));

            Assert.Equal([new RsuRemoval("5001", "101", "transferred", "5009"), new RsuRemoval("5003", "101", "cleared", "")], removals);
        }

        [Fact]
        public void Store_purchases_give_the_token_for_the_next_page()
        {
            RsuPurchasePage page = RsuJson.Purchases(Json("""
                {"purchases":[{"purchase_id":1,"user_id":900,"registration_id":null,
                   "items":[{"item_id":4,"item_name":"Quarter-Zip - Navy","item_option":"L","quantity":2},
                            {"item_id":5,"item_name":"Sticker","item_option":null,"quantity":1}]}],
                 "next_url":"https://api.runsignup.com/rest/v2/race-store/get-race-store-purchases.json?race_id=1&next_token=abc%3D%3D"}
                """));

            RsuPurchase purchase = Assert.Single(page.Purchases);
            Assert.Equal(("", "900"), (purchase.RegistrationId, purchase.UserId));
            Assert.Equal(["Quarter-Zip - Navy (L) x2", "Sticker"], purchase.Items.Select(i => i.ToString()));
            Assert.Equal("abc==", page.NextToken);
        }

        [Fact]
        public void The_last_store_page_has_no_token()
        {
            Assert.Equal("", RsuJson.Purchases(Json("""{"purchases":[],"next_url":null}""")).NextToken);
        }

        [Fact]
        public void Races_and_their_events_are_read()
        {
            RsuRace race = Assert.Single(RsuJson.Races(Json("""
                {"races":[{"race":{"race_id":42,"name":"Spring Fling","next_date":"06/15/2026",
                  "events":[{"event_id":101,"name":"5K","start_time":"6/15/2026 08:00","distance":"5K"}]}}]}
                """)));

            Assert.Equal(("42", "Spring Fling (06/15/2026)"), (race.RaceId, race.ToString()));
            Assert.Equal(new RsuEvent("101", "5K", "6/15/2026 08:00", "5K"), Assert.Single(race.Events));
        }

        [Fact]
        public void An_events_giveaway_names_the_item()
        {
            RsuRace race = RsuJson.Race(Json("""
                {"race":{"race_id":177,"events":[
                  {"event_id":1090845,"name":"5K","start_time":"11/14/2026 10:00","giveaway":"Long Sleeve Shirt"},
                  {"event_id":1090846,"name":"10K","start_time":"11/14/2026 10:00","giveaway":null}]}}
                """));

            Assert.Equal(["Long Sleeve Shirt", ""], race.Events.Select(e => e.Giveaway));
        }

        [Fact]
        public void Errors_are_found_in_the_reply()
        {
            Assert.Equal("Invalid API key.", RsuJson.Error(Json("""{"error":{"error_code":6,"error_msg":"Invalid API key."}}""")));
            Assert.Null(RsuJson.Error(Json("""{"races":[]}""")));
        }
    }

    /// <summary>Requests to Runsignup: the key in the query, the secret only in a header, one page at a time.</summary>
    public class RunsignupHandlerTests : IDisposable
    {
        private readonly FakeApiServer server = new();
        private readonly string savedBase = RunsignupHandlers.BaseUrl;
        private readonly ApiObject api = new(1, Constants.ApiConstants.RUNSIGNUP, "s3cret", "Timer", "key123", "");

        public RunsignupHandlerTests() => RunsignupHandlers.BaseUrl = server.Url;

        public void Dispose()
        {
            RunsignupHandlers.BaseUrl = savedBase;
            server.Dispose();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task The_key_goes_in_the_query_and_the_secret_in_a_header()
        {
            server.ResponseBody = """{"generic_entity_id":9,"entity_type":"partner","entity_name":"Fast Timing","entity_specific_id":"12"}""";

            RsuEntityInfo info = await RunsignupHandlers.GetEntityInfo(api);

            FakeApiServer.Request request = server.Single();
            Assert.Equal("rest/v2/auth-info/entity-info.json", request.Path);
            Assert.Equal(("key123", "json"), (request.Query["rsu_api_key"], request.Query["format"]));
            Assert.Equal("s3cret", request.Headers["X-RSU-API-SECRET"]);
            Assert.DoesNotContain("s3cret", string.Join("&", request.Query.Values));
            Assert.Equal("Partner: Fast Timing", info.Describe());
        }

        [Fact]
        public async Task Participants_are_paged_until_a_short_page()
        {
            server.Responder = request =>
            {
                int count = request.Query["page"] == "1" ? RunsignupHandlers.ParticipantsPerPage : 3;
                string people = string.Join(",", Enumerable.Range(0, count).Select(i => $$$"""{"registration_id":{{{request.Query["page"]}}}{{{i:D3}}},"user":{"first_name":"P{{{i}}}"}}"""));
                return (HttpStatusCode.OK, $$"""[{"event":{"event_id":101},"participants":[{{people}}]}]""");
            };
            List<int> pages = [];

            List<RsuParticipant> all = await RunsignupHandlers.GetParticipants(api, "42", ["101", "102"], pages.Add);

            Assert.Equal(RunsignupHandlers.ParticipantsPerPage + 3, all.Count);
            Assert.Equal([1, 2], pages);
            FakeApiServer.Request first = server.Requests[0];
            Assert.Equal("rest/race/42/participants", first.Path);
            Assert.Equal(("101,102", "T", "T", "T"), (first.Query["event_id"], first.Query["include_registration_addons"], first.Query["include_questions"], first.Query["include_user_anonymous_flag"]));
        }

        [Fact]
        public async Task Store_purchases_follow_the_next_page_token()
        {
            server.Responder = request => request.Query.TryGetValue("next_token", out string? token) && token == "p2"
                ? (HttpStatusCode.OK, """{"purchases":[{"registration_id":2,"user_id":1,"items":[{"item_name":"Mug","quantity":1}]}],"next_url":null}""")
                : (HttpStatusCode.OK, """{"purchases":[{"registration_id":1,"user_id":1,"items":[]}],"next_url":"https://x/y?race_id=42&next_token=p2"}""");

            List<RsuPurchase> purchases = await RunsignupHandlers.GetStorePurchases(api, "42");

            Assert.Equal(["1", "2"], purchases.Select(p => p.RegistrationId));
            Assert.Equal(2, server.Requests.Count);
            Assert.Equal("42", server.Requests[1].Query["race_id"]);
        }

        [Fact]
        public async Task An_error_in_a_successful_reply_is_still_an_error()
        {
            server.ResponseBody = """{"error":{"error_code":6,"error_msg":"Invalid API key."}}""";

            ApiException ex = await Assert.ThrowsAsync<ApiException>(() => RunsignupHandlers.GetEntityInfo(api));

            Assert.Contains("Invalid API key.", ex.Message);
        }

        [Fact]
        public async Task A_failed_reply_is_an_error()
        {
            server.Status = HttpStatusCode.InternalServerError;
            server.ResponseBody = "<html>down</html>";

            await Assert.ThrowsAsync<ApiException>(() => RunsignupHandlers.GetRaces(api));
        }
    }

    /// <summary>Merging a Runsignup download into an event, including removals and reactivated people.</summary>
    public class RunsignupDownloadTests : DatabaseTestBase
    {
        private readonly Event theEvent;
        private readonly Distance fiveK;
        private readonly Distance tenK;

        public RunsignupDownloadTests()
        {
            theEvent = AddEvent(makeCurrent: true);
            fiveK = AddDistance(theEvent.Identifier, "5K");
            tenK = AddDistance(theEvent.Identifier, "10K");
            Db.SetRegistrationDistances(theEvent.Identifier,
            [
                new(theEvent.Identifier, "101", "5K Run", fiveK.Identifier),
                new(theEvent.Identifier, "102", "10K Run", tenK.Identifier),
                new(theEvent.Identifier, "103", "Kids Mile", RegistrationDistance.SkipDistance),
            ]);
        }

        private static RsuParticipant Person(string reg, string first = "Ann", string last = "Lee", string ev = "101", string bib = "", string user = "")
            => new() { RegistrationId = reg, EventId = ev, FirstName = first, LastName = last, Birthdate = "1990-01-02", Gender = "F", Bib = bib, UserId = user.Length > 0 ? user : "u" + reg };

        private RsuDownloadSummary Download(params RsuParticipant[] people) => Download(new RsuDownloadData { Participants = [.. people] });

        private RsuDownloadSummary Download(RsuDownloadData data) => RunsignupDownload.Apply(Db, theEvent, data);

        private List<Participant> People() => Db.GetParticipants(theEvent.Identifier);

        private Participant One(string reg) => People().Single(p => p.EventSpecific.ExternalId == "rsu-" + reg);

        [Fact]
        public void New_registrations_are_added_in_their_mapped_distance()
        {
            RsuDownloadSummary summary = Download(Person("1", bib: "12"), Person("2", "Bo", "Diaz", ev: "102"));

            Assert.Equal(2, summary.Added);
            Participant ann = One("1");
            Assert.Equal(("5K", "12", "1990/01/02"), (ann.Distance, ann.Bib, ann.Birthdate));
            Assert.Equal(RegistrationState.ACTIVE, ann.EventSpecific.Removed);
            Assert.False(ann.EventSpecific.IsRemoved);
            Assert.Equal("10K", One("2").Distance);
        }

        [Fact]
        public void Skipped_events_are_left_out_and_new_events_are_reported()
        {
            RsuDownloadSummary summary = Download(new RsuDownloadData
            {
                Participants = [Person("1"), Person("2", ev: "103")],
                RaceEvents = [new("101", "5K Run", "", ""), new("103", "Kids Mile", "", ""), new("104", "Dog Jog", "", "")],
            });

            Assert.Single(People());
            Assert.Equal(1, summary.SkippedUnmapped);
            Assert.Equal(["Dog Jog"], summary.UnmappedEvents);
            Assert.Contains("Dog Jog", summary.Describe());
        }

        [Fact]
        public void A_name_change_updates_the_same_person()
        {
            Download(Person("1", "Ann"));

            RsuDownloadSummary summary = Download(Person("1", "Anne"));

            Assert.Equal("Anne", Assert.Single(People()).FirstName);
            Assert.Equal((0, 1), (summary.Added, summary.Updated));
        }

        [Fact]
        public void Downloading_again_with_no_changes_updates_nobody()
        {
            Download(Person("1"), Person("2", "Bo"));

            RsuDownloadSummary summary = Download(Person("1"), Person("2", "Bo"));

            Assert.Equal((0, 0), (summary.Added, summary.Updated));
        }

        [Fact]
        public void A_street_with_an_apartment_stays_the_same_on_every_download()
        {
            RsuParticipant ann = Person("1");
            ann.Street = "1221 Cedar Ave, Apt 4";

            Download(ann);
            RsuDownloadSummary again = Download(ann);

            Participant saved = One("1");
            Assert.Equal(("1221 Cedar Ave", "Apt 4"), (saved.Street, saved.Street2));
            Assert.Equal(0, again.Updated);
        }

        [Fact]
        public void Someone_entered_before_the_link_is_matched_by_name_and_birthdate()
        {
            AddParticipant(theEvent.Identifier, fiveK, "7", first: "Ann", last: "Lee", birthday: "1990/01/02");

            Download(Person("1", bib: "7"));

            Participant ann = Assert.Single(People());
            Assert.Equal("rsu-1", ann.EventSpecific.ExternalId);
        }

        [Fact]
        public void People_edited_in_chronokeep_keep_their_edits_but_take_the_runsignup_id()
        {
            Participant local = AddParticipant(theEvent.Identifier, fiveK, "7", first: "Ann", last: "Lee", birthday: "1990/01/02");
            local.EventSpecific.Version = local.EventSpecific.UploadedVersion + 1;
            Db.UpdateParticipant(local);

            RsuDownloadSummary summary = Download(Person("1", bib: "99"));

            Participant ann = Assert.Single(People());
            Assert.Equal(("7", "rsu-1"), (ann.Bib, ann.EventSpecific.ExternalId));
            Assert.Equal(1, summary.SkippedEdited);
        }

        [Fact]
        public void Removed_registrations_are_marked_and_keep_their_bib()
        {
            Download(Person("1", bib: "12"), Person("2", "Bo"));

            RsuDownloadSummary summary = Download(new RsuDownloadData
            {
                Participants = [Person("2", "Bo")],
                Removals = [new("1", "101", "cleared", "")],
            });

            Participant ann = One("1");
            Assert.Equal(("removed_cleared", "12"), (ann.EventSpecific.Removed, ann.Bib));
            Assert.True(ann.EventSpecific.IsRemoved);
            Assert.Equal(1, summary.MarkedRemoved);
        }

        [Fact]
        public void A_removed_registration_back_in_runsignup_is_active_again()
        {
            Download(Person("1"));
            Download(new RsuDownloadData { Removals = [new("1", "101", "removed", "")] });

            RsuDownloadSummary summary = Download(Person("1"));

            Assert.Equal(RegistrationState.ACTIVE, One("1").EventSpecific.Removed);
            Assert.Equal(1, summary.Reinstated);
        }

        [Fact]
        public void Someone_reactivated_by_hand_isnt_removed_again()
        {
            Download(Person("1", bib: "12"));
            Download(new RsuDownloadData { Removals = [new("1", "101", "removed", "")] });
            Participant ann = One("1");
            ann.EventSpecific.Removed = RegistrationState.Reactivated(RegistrationState.REMOVED);
            Db.UpdateParticipant(ann);

            RsuDownloadSummary summary = Download(new RsuDownloadData { Removals = [new("1", "101", "removed", "")] });

            Assert.Equal("reactivated_removed", One("1").EventSpecific.Removed);
            Assert.False(One("1").EventSpecific.IsRemoved);
            Assert.Equal((0, 1), (summary.MarkedRemoved, summary.KeptReactivated));
        }

        [Fact]
        public void Someone_reactivated_by_hand_stays_reactivated_when_runsignup_updates_them()
        {
            Download(Person("1"));
            Participant ann = One("1");
            ann.EventSpecific.Removed = RegistrationState.Reactivated(RegistrationState.REMOVED);
            Db.UpdateParticipant(ann);

            Download(Person("1", "Anne"));
            Download(new RsuDownloadData { Removals = [new("1", "101", "removed", "")] });

            Assert.Equal(("Anne", "reactivated_removed"), (One("1").FirstName, One("1").EventSpecific.Removed));
        }

        [Fact]
        public void A_transfer_keeps_the_old_entry_only_while_it_holds_the_only_bib()
        {
            Download(Person("1", bib: "12"));

            Download(new RsuDownloadData
            {
                Participants = [Person("9", "Ann", "Lee", ev: "102")],
                Removals = [new("1", "101", "transferred", "9")],
            });
            Assert.Equal("removed_transferred", One("1").EventSpecific.Removed);
            Assert.Equal(RegistrationState.ACTIVE, One("9").EventSpecific.Removed);

            RsuDownloadSummary summary = Download(new RsuDownloadData
            {
                Participants = [Person("9", "Ann", "Lee", ev: "102", bib: "40")],
                Removals = [new("1", "101", "transferred", "9")],
            });
            Assert.Equal("rsu-9", Assert.Single(People()).EventSpecific.ExternalId);
            Assert.Equal(1, summary.TransfersDropped);
        }

        [Fact]
        public void A_transfer_without_a_bib_drops_the_old_entry()
        {
            Download(Person("1"));

            Download(new RsuDownloadData
            {
                Participants = [Person("9", ev: "102")],
                Removals = [new("1", "101", "transferred", "9")],
            });

            Assert.Equal("rsu-9", Assert.Single(People()).EventSpecific.ExternalId);
        }

        [Fact]
        public void Removals_for_people_never_downloaded_are_ignored()
        {
            RsuDownloadSummary summary = Download(new RsuDownloadData { Removals = [new("5", "101", "cleared", "")] });

            Assert.Empty(People());
            Assert.Equal(0, summary.MarkedRemoved);
        }

        [Fact]
        public void A_changed_bib_keeps_the_old_bib_as_a_marked_row()
        {
            Download(Person("1", bib: "12"));

            Download(Person("1", bib: "15"));

            List<Participant> people = People();
            Assert.Equal("15", One("1").Bib);
            Participant old = people.Single(p => p.Bib == "12");
            Assert.Equal(("", RunsignupDownload.BibChanged), (old.EventSpecific.ExternalId, old.EventSpecific.Removed));
        }

        [Fact]
        public void Apparel_joins_the_giveaway_add_ons_and_store_items()
        {
            RsuParticipant ann = Person("1", user: "900");
            ann.GiveawayChoice = "M";
            ann.AddOns = [new("Pint Glass", "", 2)];
            RsuParticipant bo = Person("2", "Bo", user: "901");

            Download(new RsuDownloadData
            {
                RaceEvents = [new("101", "5K Run", "", "", "Race Tee")],
                Participants = [ann, bo, Person("3", "Cy", user: "901")],
                Purchases =
                [
                    new("1", "900", [new("Quarter-Zip - Navy", "L", 2)]),
                    new("", "900", [new("Sticker", "", 1)]),
                    new("", "901", [new("Mug", "", 1)]),
                ],
            });

            Assert.Equal("Race Tee (M); Pint Glass x2; Quarter-Zip - Navy (L) x2; Sticker", One("1").EventSpecific.Apparel);
            Assert.Equal("", One("2").EventSpecific.Apparel);
        }

        [Fact]
        public void A_declined_giveaway_is_left_out_of_apparel()
        {
            RsuParticipant ann = Person("1", user: "900");
            ann.GiveawayChoice = "No shirt";
            RsuParticipant bo = Person("2", "Bo", user: "901");
            bo.GiveawayChoice = "Large";

            Download(new RsuDownloadData
            {
                RaceEvents = [new("101", "5K Run", "", "", "Long Sleeve Shirt")],
                Participants = [ann, bo],
                Purchases = [new("1", "900", [new("Additional Fowl Fun Run Shirt", "S", 1)])],
            });

            Assert.Equal("Additional Fowl Fun Run Shirt (S)", One("1").EventSpecific.Apparel);
            Assert.Equal("Long Sleeve Shirt (Large)", One("2").EventSpecific.Apparel);
        }

        [Fact]
        public void An_add_on_option_that_says_no_is_left_out()
        {
            RsuParticipant ann = Person("1");
            ann.AddOns = [new("T-Shirt", "No shirt", 1), new("Mug", "", 1)];

            Assert.Equal("Mug", RunsignupDownload.Apparel(ann, "", null));
        }

        [Fact]
        public void A_choice_without_an_item_name_is_kept_as_it_is()
        {
            RsuParticipant ann = Person("1");
            ann.GiveawayChoice = "One size fits most.-Small to XL";

            Assert.Equal("One size fits most.-Small to XL", RunsignupDownload.Apparel(ann, "", null));
        }

        // Answers seen in real Runsignup races, and ones that only start with the same letters as "no".
        [Theory]
        [InlineData("No shirt", true)]
        [InlineData("No Shirt", true)]
        [InlineData("No hoodie", true)]
        [InlineData("NO SHIRT!", true)]
        [InlineData("No long sleeve shirt", true)]
        [InlineData("No preference", true)]
        [InlineData("No shirt - donate my fee", true)]
        [InlineData("None", true)]
        [InlineData("N/A", true)]
        [InlineData("No", true)]
        [InlineData("I don't want a shirt", true)]
        [InlineData("Don\u2019t need one", true)]
        [InlineData("Decline", true)]
        [InlineData("Large", false)]
        [InlineData("2XL", false)]
        [InlineData("XX-Large", false)]
        [InlineData("One size fits most.-Small to XL", false)]
        [InlineData("Youth Medium", false)]
        [InlineData("Nordic Blue L", false)]
        [InlineData("Northwest Medium", false)]
        [InlineData("Not sure - Medium", false)]
        [InlineData("", false)]
        public void Answers_that_say_no_are_declines(string choice, bool declined)
        {
            Assert.Equal(declined, RunsignupDownload.IsDeclined(choice));
        }

        [Fact]
        public void Purchases_by_someone_with_several_registrations_are_unmatched()
        {
            int unmatched;
            Dictionary<string, List<RsuItem>> items = RunsignupDownload.MatchPurchases(
                [Person("2", user: "901"), Person("3", user: "901")],
                [new("", "901", [new("Mug", "", 1)]), new("", "777", [new("Hat", "", 1)])],
                out unmatched);

            Assert.Empty(items);
            Assert.Equal(2, unmatched);
        }

        [Fact]
        public void Chosen_comment_fields_fill_comments_in_order()
        {
            theEvent.RegistrationCommentFields = "q:78,team_name,q:77,age";
            RsuParticipant ann = Person("1");
            ann.TeamName = "Hill Hoppers";
            ann.Answers = [new("77", "Hometown", "Eugene"), new("78", "Running for", "")];

            Download(ann);

            Assert.Equal("Hill Hoppers | Eugene", One("1").EventSpecific.Comments);
        }

        [Fact]
        public void Without_comment_fields_local_comments_are_kept()
        {
            AddParticipant(theEvent.Identifier, fiveK, "7", first: "Ann", last: "Lee", birthday: "1990/01/02");

            Download(Person("1", bib: "7"));

            Assert.Equal("comment", One("1").EventSpecific.Comments);
        }

        [Fact]
        public void Chips_are_assigned_when_bib_and_chip_are_set()
        {
            RsuParticipant ann = Person("1", bib: "12");
            ann.Chip = "CHIP12";
            RsuParticipant bo = Person("2", "Bo");
            bo.Chip = "CHIP99";

            Download(ann, bo);

            BibChipAssociation chip = Assert.Single(Db.GetBibChips(theEvent.Identifier));
            Assert.Equal(("12", "CHIP12"), (chip.Bib, chip.Chip));
        }

        [Fact]
        public void Reactivated_and_active_people_count_on_the_timing_page_but_removed_ones_dont()
        {
            Download(Person("1"), Person("2", "Bo"), Person("3", "Cy"));
            Download(new RsuDownloadData
            {
                Participants = [Person("1")],
                Removals = [new("2", "101", "removed", ""), new("3", "101", "removed", "")],
            });
            Participant bo = One("2");
            bo.EventSpecific.Removed = RegistrationState.Reactivated(RegistrationState.REMOVED);
            Db.UpdateParticipant(bo);

            int total = Db.GetDistanceStats(theEvent.Identifier).Where(s => s.DistanceId != -1).Sum(s => s.Total);

            Assert.Equal(2, total);
        }

        [Fact]
        public void Only_the_events_nearest_the_event_date_belong_to_it()
        {
            theEvent.Date = "2026/06/15";
            List<RsuEvent> events =
            [
                new("1", "5K", "6/14/2025 8:00", ""),
                new("2", "5K", "6/13/2026 8:00", ""),
                new("3", "Marathon", "6/14/2026 6:30 AM", ""),
                new("4", "5K", "2027-06-12 08:00:00", ""),
                new("5", "Virtual", "", ""),
            ];

            Assert.Equal(["2", "3"], RunsignupDownload.EventsNearest(theEvent, events).Select(e => e.EventId));
        }

        [Fact]
        public void A_past_event_picks_that_years_events()
        {
            theEvent.Date = "2025/06/15";

            List<RsuEvent> nearest = RunsignupDownload.EventsNearest(theEvent,
                [new("1", "5K", "6/14/2025 8:00", ""), new("2", "5K", "6/13/2026 8:00", "")]);

            Assert.Equal("1", Assert.Single(nearest).EventId);
        }

        [Fact]
        public void Events_without_start_times_are_all_kept()
        {
            List<RsuEvent> events = [new("1", "5K", "", ""), new("2", "10K", "", "")];

            Assert.Equal(events, RunsignupDownload.EventsNearest(theEvent, events));
        }

        [Theory]
        [InlineData("", false)]
        [InlineData("active", false)]
        [InlineData("reactivated_transferred", false)]
        [InlineData("reactivated_unknown", false)]
        [InlineData("removed_cleared", true)]
        [InlineData("removed_transferred", true)]
        [InlineData("removed_bib_changed", true)]
        public void Only_reasons_count_as_removed(string state, bool removed)
        {
            Assert.Equal(removed, RegistrationState.IsRemoved(state));
        }

        [Theory]
        [InlineData("transferred", "Transferred")]
        [InlineData("removed", "Removed")]
        [InlineData("cleared", "Cleared")]
        [InlineData("deferred", "Deferred")]
        [InlineData("unknown", "Removed")]
        [InlineData("bib_changed", "Bib Changed")]
        [InlineData("refunded", "Refunded")]
        public void Each_reason_has_a_label(string reason, string label)
        {
            Assert.Equal(label, RegistrationState.Label(reason));
        }

        [Theory]
        [InlineData("transferred", "transferred")]
        [InlineData("Deferred", "deferred")]
        [InlineData("", "unknown")]
        [InlineData(null, "unknown")]
        public void Runsignup_reasons_are_stored_lower_case_and_a_missing_one_is_unknown(string? given, string stored)
        {
            Assert.Equal(stored, RegistrationState.FromRunsignup(given));
        }

        [Fact]
        public void A_removal_without_a_reason_is_stored_as_unknown()
        {
            Download(Person("1"));

            Download(new RsuDownloadData { Removals = [new("1", "101", "", "")] });

            Assert.Equal("removed_unknown", One("1").EventSpecific.Removed);
            Assert.Equal("Removed", One("1").RemovedReason);
        }

        [Fact]
        public void Reactivating_keeps_the_reason_they_were_removed_for()
        {
            Download(Person("1"));
            Download(new RsuDownloadData { Removals = [new("1", "101", "deferred", "")] });
            Participant ann = One("1");
            ann.EventSpecific.Removed = RegistrationState.Reactivated(RegistrationState.Reason(ann.EventSpecific.Removed));
            Db.UpdateParticipant(ann);

            Download(new RsuDownloadData { Removals = [new("1", "101", "deferred", "")] });

            Assert.Equal("reactivated_deferred", One("1").EventSpecific.Removed);
        }

        [Theory]
        [InlineData("1990-01-02", "1990/01/02")]
        [InlineData("01/02/1990", "1990/01/02")]
        [InlineData("1/2/1990", "1990/01/02")]
        public void Runsignup_birthdates_are_stored_year_first(string given, string expected)
        {
            Assert.Equal(expected, CultureScope.In("de-DE", () => RunsignupDownload.Birthdate(given)));
        }
    }

    /// <summary>A download asks Runsignup only for this year's mapped events, even if older ones are still mapped.</summary>
    public class RunsignupDownloadRequestTests : DatabaseTestBase
    {
        private readonly FakeApiServer server = new();
        private readonly string savedBase = RunsignupHandlers.BaseUrl;

        public RunsignupDownloadRequestTests() => RunsignupHandlers.BaseUrl = server.Url;

        public override void Dispose()
        {
            RunsignupHandlers.BaseUrl = savedBase;
            server.Dispose();
            base.Dispose();
        }

        [Fact]
        public async Task Last_years_mapped_events_are_not_downloaded()
        {
            Event theEvent = AddEvent(date: "2026/06/15", makeCurrent: true);
            Distance fiveK = AddDistance(theEvent.Identifier, "5K");
            theEvent.RegistrationApiId = Db.AddApi(new ApiObject(0, Constants.ApiConstants.RUNSIGNUP, "s3cret", "Timer", "key123", ""));
            theEvent.RegistrationRaceId = "42";
            Db.UpdateEvent(theEvent);
            Db.SetRegistrationDistances(theEvent.Identifier,
            [
                new(theEvent.Identifier, "1", "5K", fiveK.Identifier),
                new(theEvent.Identifier, "2", "5K", fiveK.Identifier),
            ]);
            server.Responder = request => request.Path switch
            {
                "rest/race/42" => (HttpStatusCode.OK, """
                    {"race":{"race_id":42,"events":[
                      {"event_id":1,"name":"5K","start_time":"6/14/2025 8:00"},
                      {"event_id":2,"name":"5K","start_time":"6/13/2026 8:00"}]}}
                    """),
                "rest/v2/race-store/get-race-store-purchases.json" => (HttpStatusCode.OK, """{"purchases":[]}"""),
                _ => (HttpStatusCode.OK, "[]"),
            };

            RsuDownloadSummary summary = await RunsignupDownload.Download(Db, Db.GetEvent(theEvent.Identifier)!);

            List<FakeApiServer.Request> eventRequests = [.. server.Requests.Where(r => r.Query.ContainsKey("event_id"))];
            Assert.NotEmpty(eventRequests);
            Assert.All(eventRequests, r => Assert.Equal("2", r.Query["event_id"]));
            Assert.Empty(summary.UnmappedEvents);
        }
    }
}
