using System.Net;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Objects;
using Chronokeep.Test.Network;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.MainPages;
using static Chronokeep.Constants.ApiConstants;

namespace Chronokeep.Test.UI
{
    /// <summary>
    /// The Participants page: the list and its filters, the bib stats table, and the Chronokeep API
    /// download/upload panel. Runsignup adds a second download button, hides removed participants,
    /// and adds a Removed bib column here, so the current behaviour is pinned first.
    /// </summary>
    public class ParticipantsPageTests : UiTestBase
    {
        private readonly FakeApiServer server = new();

        public override void Dispose()
        {
            base.Dispose();
            server.Dispose();
        }

        private async Task<ParticipantsPage> OpenPage()
        {
            ParticipantsPage page = new(MainWindow, Db);
            Host(page);
            page.UpdateView();
            await WaitUntil(() => Find<DataGrid>(page, "ParticipantsList").ItemsSource != null, because: "participants to load");
            await Settle(100);
            return page;
        }

        private static List<Participant> Listed(ParticipantsPage page) =>
            [.. Find<DataGrid>(page, "ParticipantsList").ItemsSource!.Cast<Participant>()];

        private static List<BibStats> BibStatRows(ParticipantsPage page) =>
            [.. Find<DataGrid>(page, "StatsListView").ItemsSource!.Cast<BibStats>()];

        private (Event Event, Distance FiveK, Distance TenK) SeedTwoDistances()
        {
            Event theEvent = AddEvent(makeCurrent: true);
            Distance fiveK = AddDistance(theEvent.Identifier, "5K");
            Distance tenK = AddDistance(theEvent.Identifier, "10K");
            AddParticipant(theEvent.Identifier, fiveK, "1", first: "Ann", last: "Alpha");
            AddParticipant(theEvent.Identifier, fiveK, "", first: "Ben", last: "Bravo");
            AddParticipant(theEvent.Identifier, tenK, "3", first: "Cat", last: "Charlie");
            return (Db.GetCurrentEvent()!, fiveK, tenK);
        }

        private int LinkToFakeApi(Event theEvent)
        {
            int apiId = Db.AddApi(new ApiObject(0, CHRONOKEEP_RESULTS_SELF, server.Url, "Local", "tok", ""));
            theEvent.ApiId = apiId;
            theEvent.ApiEventId = "spring-fling,2026";
            Db.UpdateEvent(theEvent);
            return apiId;
        }

        [AvaloniaFact]
        public async Task Lists_every_participant_in_the_current_event()
        {
            SeedTwoDistances();

            ParticipantsPage page = await OpenPage();

            Assert.Equal(["Alpha", "Bravo", "Charlie"], Listed(page).Select(p => p.LastName).OrderBy(n => n));
        }

        [AvaloniaFact]
        public async Task Distance_filter_shows_only_that_distance()
        {
            (_, _, Distance tenK) = SeedTwoDistances();
            ParticipantsPage page = await OpenPage();
            ComboBox distances = Find<ComboBox>(page, "DistanceBox");
            Assert.Equal(["All", "5K", "10K"], distances.Items.OfType<ComboBoxItem>().Select(i => (string)i.Content!).Order().Reverse().OrderBy(n => n == "All" ? 0 : 1));

            distances.SelectedItem = distances.Items.OfType<ComboBoxItem>().Single(i => (string)i.Tag! == tenK.Identifier.ToString());
            await WaitUntil(() => Listed(page).Count == 1, because: "the 10K filter to apply");

            Assert.Equal("Charlie", Assert.Single(Listed(page)).LastName);
        }

        [AvaloniaFact]
        public async Task Search_box_filters_by_name()
        {
            SeedTwoDistances();
            ParticipantsPage page = await OpenPage();

            Find<TextBox>(page, "SearchBox").Text = "bravo";
            await WaitUntil(() => Listed(page).Count == 1, because: "the search to apply");

            Assert.Equal("Ben", Assert.Single(Listed(page)).FirstName);
        }

        [AvaloniaFact]
        public async Task Bib_stats_count_participants_with_and_without_bibs_per_distance()
        {
            SeedTwoDistances();

            ParticipantsPage page = await OpenPage();
            Dictionary<string, BibStats> stats = BibStatRows(page).ToDictionary(s => s.DistanceName!);

            Assert.Equal((2, 1), (stats["All"].With, stats["All"].Without));
            Assert.Equal((1, 1), (stats["5K"].With, stats["5K"].Without));
            Assert.Equal((1, 0), (stats["10K"].With, stats["10K"].Without));
            Assert.True(Find<Expander>(page, "StatsExpander").IsVisible, "Stats show while someone lacks a bib.");
        }

        [AvaloniaFact]
        public async Task Bib_stats_hide_when_everyone_has_a_bib()
        {
            Event theEvent = AddEvent(makeCurrent: true);
            Distance d = AddDistance(theEvent.Identifier);
            AddParticipant(theEvent.Identifier, d, "1");

            ParticipantsPage page = await OpenPage();

            Assert.False(Find<Expander>(page, "StatsExpander").IsVisible);
        }

        [AvaloniaFact]
        public async Task Api_panel_shows_only_when_the_event_is_linked()
        {
            (Event theEvent, _, _) = SeedTwoDistances();
            ParticipantsPage unlinked = await OpenPage();
            Assert.False(Find<Panel>(unlinked, "ApiPanel").IsVisible);

            LinkToFakeApi(theEvent);
            ParticipantsPage linked = await OpenPage();
            Assert.True(Find<Panel>(linked, "ApiPanel").IsVisible);
        }

        private static string PersonJson(string id, string bib, string first, string last, string distance, string birthdate = "01/02/1990")
            => $$"""{"id":"{{id}}","bib":"{{bib}}","first":"{{first}}","last":"{{last}}","birthdate":"{{birthdate}}","gender":"F","age_group":"","distance":"{{distance}}","anonymous":false,"sms_enabled":false,"mobile":"","apparel":"M"}""";

        private static string ParticipantsJson(IEnumerable<string> people)
            => $$"""{"event":{"slug":"spring-fling"},"year":{"year":"2026"},"participants":[{{string.Join(",", people)}}]}""";

        private async Task Download(ParticipantsPage page)
        {
            Button download = Find<Button>(page, "Download");
            Click(download);
            await WaitUntil(() => ContentText(download) == "Download", because: "the download to finish");
            await Settle(200);
        }

        [AvaloniaFact]
        public async Task Download_adds_new_people_and_creates_missing_distances()
        {
            (Event theEvent, _, _) = SeedTwoDistances();
            LinkToFakeApi(theEvent);
            server.Responder = _ => (HttpStatusCode.OK, ParticipantsJson([
                PersonJson("a1", "50", "Dana", "Delta", "5K"),
                PersonJson("a2", "51", "Eli", "Echo", "Half Marathon"),
            ]));
            ParticipantsPage page = await OpenPage();

            await Download(page);

            List<Participant> all = Db.GetParticipants(theEvent.Identifier);
            Assert.Equal(5, all.Count);
            Assert.Equal("5K", all.Single(p => p.LastName == "Delta").Distance);
            Assert.Equal("Half Marathon", all.Single(p => p.LastName == "Echo").Distance);
            Assert.Contains(Db.GetDistances(theEvent.Identifier), d => d.Name == "Half Marathon");
            Assert.Equal(5, Listed(page).Count);
        }

        [AvaloniaFact]
        public async Task Download_updates_the_bib_of_a_matching_person_instead_of_duplicating()
        {
            (Event theEvent, Distance fiveK, _) = SeedTwoDistances();
            LinkToFakeApi(theEvent);
            AddParticipant(theEvent.Identifier, fiveK, "", first: "Dee", last: "Delta", birthday: "1985/3/4");
            server.Responder = _ => (HttpStatusCode.OK, ParticipantsJson([
                PersonJson("d1", "77", "Dee", "Delta", "5K", "1985-03-04"),
            ]));
            ParticipantsPage page = await OpenPage();

            await Download(page);

            List<Participant> dees = [.. Db.GetParticipants(theEvent.Identifier).Where(p => p.LastName == "Delta")];
            Assert.Equal("77", Assert.Single(dees).Bib);
        }

        // Regression: before birthdates were stored as yyyy/MM/dd, a local 01/02/1990 never matched the
        // downloaded 1/2/1990, so the download added a duplicate instead of updating the bib.
        [AvaloniaFact]
        public async Task Download_matches_a_person_whose_local_birthdate_is_zero_padded()
        {
            (Event theEvent, _, _) = SeedTwoDistances();
            LinkToFakeApi(theEvent);
            // Ben was entered locally as 01/02/1990.
            server.Responder = _ => (HttpStatusCode.OK, ParticipantsJson([
                PersonJson("b1", "77", "Ben", "Bravo", "5K", "01/02/1990"),
            ]));
            ParticipantsPage page = await OpenPage();

            await Download(page);

            List<Participant> bens = [.. Db.GetParticipants(theEvent.Identifier).Where(p => p.LastName == "Bravo")];
            Assert.Equal("77", Assert.Single(bens).Bib);
        }

        [AvaloniaFact]
        public async Task Download_pages_through_results_fifty_at_a_time()
        {
            (Event theEvent, _, _) = SeedTwoDistances();
            LinkToFakeApi(theEvent);
            server.Responder = request =>
            {
                int pageNumber = JsonDocument.Parse(request.Body).RootElement.GetProperty("page").GetInt32();
                int count = pageNumber == 1 ? 50 : 3;
                return (HttpStatusCode.OK, ParticipantsJson(Enumerable.Range(0, count)
                    .Select(i => PersonJson($"p{pageNumber}-{i}", $"{pageNumber * 1000 + i}", $"First{i}", $"Page{pageNumber}", "5K"))));
            };
            ParticipantsPage page = await OpenPage();

            await Download(page);

            Assert.Equal(2, server.Requests.Count(r => r.Path == "participants"));
            Assert.Equal(3 + 53, Db.GetParticipants(theEvent.Identifier).Count);
        }

        [AvaloniaFact]
        public async Task Download_error_leaves_participants_unchanged_and_resets_the_button()
        {
            (Event theEvent, _, _) = SeedTwoDistances();
            LinkToFakeApi(theEvent);
            server.Responder = _ => (HttpStatusCode.InternalServerError, """{"message":"boom"}""");
            ParticipantsPage page = await OpenPage();

            await Download(page);

            Assert.Equal(3, Db.GetParticipants(theEvent.Identifier).Count);
        }

        [AvaloniaFact]
        public async Task Upload_sends_every_participant_to_the_linked_event()
        {
            (Event theEvent, _, _) = SeedTwoDistances();
            LinkToFakeApi(theEvent);
            server.Responder = _ => (HttpStatusCode.OK, """{"count":3}""");
            ParticipantsPage page = await OpenPage();
            Button upload = Find<Button>(page, "Upload");

            Click(upload);
            await WaitUntil(() => server.Requests.Any(r => r.Path == "participants/add"), because: "the upload request");
            await WaitUntil(() => ContentText(upload) != "Working", because: "the upload to finish");

            JsonElement body = JsonDocument.Parse(server.Requests.First(r => r.Path == "participants/add").Body).RootElement;
            Assert.Equal("spring-fling", body.GetProperty("slug").GetString());
            Assert.Equal("2026", body.GetProperty("year").GetString());
            Assert.Equal(3, body.GetProperty("participants").GetArrayLength());
        }
    }
}
