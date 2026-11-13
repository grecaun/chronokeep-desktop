using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Objects;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.MainPages.Dashboard;
using Chronokeep.UI.Parts;
using static Chronokeep.Constants.ApiConstants;

namespace Chronokeep.Test.UI
{
    /// <summary>
    /// Dashboard → Manage API: one editable row per stored API. Runsignup adds a new type to this
    /// page and reuses the URL box for its secret, so the current row behaviour is pinned here.
    /// </summary>
    public class ManageApiPageTests : UiTestBase
    {
        private ApiPage OpenPage()
        {
            ApiPage page = new(MainWindow, Db);
            Host(page);
            return page;
        }

        private static List<ApiPart> Rows(ApiPage page) =>
            [.. Find<ListBox>(page, "ApiBox").Items.OfType<ApiPart>()];

        private static void SelectType(ApiPart row, string type)
        {
            ComboBox box = Find<ComboBox>(row, "ApiType");
            box.SelectedItem = box.Items.OfType<ComboBoxItem>().Single(i => (string)i.Tag! == type);
            Pump();
        }

        private int AddApi(string type, string url, string nickname = "Mine", string token = "tok", string webUrl = "")
            => Db.AddApi(new ApiObject(0, type, url, nickname, token, webUrl));

        [AvaloniaFact]
        public void Shows_one_row_per_stored_api_with_its_values()
        {
            AddApi(CHRONOKEEP_RESULTS, API_URL[CHRONOKEEP_RESULTS], "Hosted", "abc", "https://results.example/");
            AddApi(CHRONOKEEP_REMOTE_SELF, "https://remote.example/", "Remote box", "xyz");

            ApiPage page = OpenPage();
            List<ApiPart> rows = Rows(page);

            Assert.Equal(2, rows.Count);
            ApiPart hosted = rows.Single(r => r.TheApi.Nickname == "Hosted");
            Assert.Equal("Hosted", Find<TextBox>(hosted, "ApiNickname").Text);
            Assert.Equal("abc", Find<TextBox>(hosted, "ApiToken").Text);
            Assert.Equal(API_URL[CHRONOKEEP_RESULTS], Find<TextBox>(hosted, "ApiUrl").Text);
            Assert.Equal("https://results.example/", Find<TextBox>(hosted, "ApiWebUrl").Text);
            Assert.Equal(CHRONOKEEP_RESULTS, ((ComboBoxItem)Find<ComboBox>(hosted, "ApiType").SelectedItem!).Tag);
        }

        [AvaloniaFact]
        public void Type_dropdown_lists_every_api_type_by_display_name()
        {
            AddApi(CHRONOKEEP_RESULTS, API_URL[CHRONOKEEP_RESULTS]);
            ApiPart row = Assert.Single(Rows(OpenPage()));

            List<ComboBoxItem> items = [.. Find<ComboBox>(row, "ApiType").Items.OfType<ComboBoxItem>()];

            Assert.Equal(API_TYPE_NAMES.Keys.OrderBy(k => k), items.Select(i => (string)i.Tag!).OrderBy(k => k));
            Assert.All(items, i => Assert.Equal(API_TYPE_NAMES[(string)i.Tag!], i.Content));
        }

        [AvaloniaFact]
        public void Hosted_types_lock_the_url_and_self_hosted_types_unlock_it()
        {
            AddApi(CHRONOKEEP_RESULTS_SELF, "https://mine.example/");
            ApiPart row = Assert.Single(Rows(OpenPage()));
            TextBox url = Find<TextBox>(row, "ApiUrl");
            Assert.True(url.IsEnabled);

            SelectType(row, CHRONOKEEP_REMOTE);
            Assert.False(url.IsEnabled);
            Assert.Equal(API_URL[CHRONOKEEP_REMOTE], url.Text);

            SelectType(row, CHRONOKEEP_RESULTS_SELF);
            Assert.True(url.IsEnabled);
        }

        [AvaloniaFact]
        public void Web_url_is_only_editable_for_results_types()
        {
            AddApi(CHRONOKEEP_RESULTS, API_URL[CHRONOKEEP_RESULTS], webUrl: "https://results.example/");
            ApiPart row = Assert.Single(Rows(OpenPage()));
            TextBox webUrl = Find<TextBox>(row, "ApiWebUrl");

            SelectType(row, CHRONOKEEP_REMOTE);
            Assert.False(webUrl.IsEnabled);
            Assert.Equal("", webUrl.Text);

            SelectType(row, CHRONOKEEP_RESULTS);
            Assert.True(webUrl.IsEnabled);
        }

        [AvaloniaFact]
        public void Add_creates_a_hosted_chronokeep_results_row()
        {
            ApiPage page = OpenPage();
            Assert.Empty(Rows(page));

            Click(Find<Button>(page, "Add"));

            ApiPart row = Assert.Single(Rows(page));
            Assert.Equal(CHRONOKEEP_RESULTS, row.TheApi.Type);
            Assert.Equal(API_URL[CHRONOKEEP_RESULTS], Find<TextBox>(row, "ApiUrl").Text);
            Assert.False(Find<TextBox>(row, "ApiUrl").IsEnabled);
            Assert.Single(Db.GetAllApi());
        }

        [AvaloniaFact]
        public void Update_saves_edits_and_adds_trailing_slashes_to_urls()
        {
            int id = AddApi(CHRONOKEEP_RESULTS_SELF, "https://old.example/");
            ApiPage page = OpenPage();
            ApiPart row = Assert.Single(Rows(page));

            Find<TextBox>(row, "ApiNickname").Text = "Renamed";
            Find<TextBox>(row, "ApiToken").Text = "new-token";
            Find<TextBox>(row, "ApiUrl").Text = "https://new.example";
            Find<TextBox>(row, "ApiWebUrl").Text = "https://web.example";
            Click(Find<Button>(page, "UpdateBtn"));

            ApiObject saved = Db.GetApi(id)!;
            Assert.Equal("Renamed", saved.Nickname);
            Assert.Equal("new-token", saved.AuthToken);
            Assert.Equal("https://new.example/", saved.Url);
            Assert.Equal("https://web.example/", saved.WebUrl);
        }

        [AvaloniaFact]
        public void Reset_discards_unsaved_edits()
        {
            int id = AddApi(CHRONOKEEP_RESULTS, API_URL[CHRONOKEEP_RESULTS], "Original");
            ApiPage page = OpenPage();
            Find<TextBox>(Assert.Single(Rows(page)), "ApiNickname").Text = "Typed but not saved";

            Click(Find<Button>(page, "ResetBtn"));

            Assert.Equal("Original", Find<TextBox>(Assert.Single(Rows(page)), "ApiNickname").Text);
            Assert.Equal("Original", Db.GetApi(id)!.Nickname);
        }

        [AvaloniaFact]
        public void Remove_deletes_the_api_and_unlinks_events_that_used_it()
        {
            (Event theEvent, _) = SeedEvent();
            int id = AddApi(CHRONOKEEP_RESULTS, API_URL[CHRONOKEEP_RESULTS]);
            theEvent.ApiId = id;
            theEvent.ApiEventId = "slug,2026";
            Db.UpdateEvent(theEvent);
            ApiPage page = OpenPage();

            Click(Find<Button>(Assert.Single(Rows(page)), "RemoveButton"));

            Assert.Empty(Rows(page));
            Assert.Empty(Db.GetAllApi());
            Event after = Db.GetCurrentEvent()!;
            Assert.Equal(NULL_ID, after.ApiId);
            Assert.Equal(NULL_EVENT_ID, after.ApiEventId);
        }

        [AvaloniaFact]
        public void Done_returns_to_the_dashboard()
        {
            SeedEvent();
            Click(Find<Button>(OpenPage(), "DoneBtn"));

            Assert.IsType<DashboardPage>(Assert.Single(MainWindow.SwitchedPages));
        }

        [AvaloniaFact]
        public void Runsignup_rows_ask_for_a_key_and_secret()
        {
            AddApi(CHRONOKEEP_RESULTS, API_URL[CHRONOKEEP_RESULTS], webUrl: "https://results.example/");
            ApiPart row = Assert.Single(Rows(OpenPage()));

            SelectType(row, RUNSIGNUP);

            Assert.Equal(("API Secret", "API Key"), (Find<TextBlock>(row, "ApiUrlLabel").Text, Find<TextBlock>(row, "ApiTokenLabel").Text));
            TextBox secret = Find<TextBox>(row, "ApiUrl");
            Assert.True(secret.IsEnabled);
            Assert.Equal("", secret.Text);
            Assert.False(Find<TextBox>(row, "ApiWebUrl").IsEnabled);

            SelectType(row, CHRONOKEEP_RESULTS_SELF);
            Assert.Equal("API URL", Find<TextBlock>(row, "ApiUrlLabel").Text);
            Assert.Equal("", Find<TextBox>(row, "ApiUrl").Text);
        }

        [AvaloniaFact]
        public void A_runsignup_secret_is_saved_exactly_as_typed()
        {
            AddApi(CHRONOKEEP_RESULTS, API_URL[CHRONOKEEP_RESULTS]);
            ApiPage page = OpenPage();
            ApiPart row = Assert.Single(Rows(page));
            SelectType(row, RUNSIGNUP);
            Find<TextBox>(row, "ApiUrl").Text = " s3cret ";
            Find<TextBox>(row, "ApiToken").Text = "key123";

            Click(Find<Button>(page, "UpdateBtn"));

            ApiObject saved = Assert.Single(Db.GetAllApi());
            Assert.Equal((RUNSIGNUP, "s3cret", "key123"), (saved.Type, saved.Secret, saved.AuthToken));
            ApiPart reloaded = Assert.Single(Rows(page));
            Assert.Equal("s3cret", Find<TextBox>(reloaded, "ApiUrl").Text);
            Assert.True(Find<TextBox>(reloaded, "ApiUrl").IsEnabled);
        }

        [AvaloniaFact]
        public async Task Test_says_who_the_runsignup_key_belongs_to()
        {
            using Network.FakeApiServer server = new() { ResponseBody = """{"entity_type":"partner","entity_name":"Fast Timing"}""" };
            string savedBase = Chronokeep.Network.Runsignup.RunsignupHandlers.BaseUrl;
            Chronokeep.Network.Runsignup.RunsignupHandlers.BaseUrl = server.Url;
            try
            {
                AllowDialogs();
                AddApi(RUNSIGNUP, "s3cret", token: "key123");
                ApiPart row = Assert.Single(Rows(OpenPage()));

                Click(Find<Button>(row, "TestButton"));

                Assert.Contains("Partner: Fast Timing", DialogText(await WaitForDialog("Fast Timing")));
                Assert.Equal("s3cret", server.Single().Headers["X-RSU-API-SECRET"]);
            }
            finally
            {
                Chronokeep.Network.Runsignup.RunsignupHandlers.BaseUrl = savedBase;
            }
        }

        [AvaloniaFact]
        public void Every_api_row_has_a_test_button()
        {
            AddApi(CHRONOKEEP_RESULTS, API_URL[CHRONOKEEP_RESULTS], "Results");
            AddApi(CHRONOKEEP_REMOTE_SELF, "https://remote.example/", "Remote", token: "r");
            AddApi(RUNSIGNUP, "s3cret", "Runsignup", token: "key123");

            List<ApiPart> rows = Rows(OpenPage());

            Assert.Equal(3, rows.Count);
            Assert.All(rows, row => Assert.True(Find<Button>(row, "TestButton").IsVisible));
        }

        [AvaloniaFact]
        public async Task Test_checks_a_results_api_with_the_typed_values()
        {
            using Network.FakeApiServer server = new() { ResponseBody = """{"events":[{"slug":"a","name":"A"},{"slug":"b","name":"B"}]}""" };
            AllowDialogs();
            AddApi(CHRONOKEEP_RESULTS_SELF, "https://old.example/", token: "old");
            ApiPart row = Assert.Single(Rows(OpenPage()));
            Find<TextBox>(row, "ApiUrl").Text = server.Url.TrimEnd('/');
            Find<TextBox>(row, "ApiToken").Text = "typed-token";

            Click(Find<Button>(row, "TestButton"));

            Assert.Contains("2 events found.", DialogText(await WaitForDialog("events found")));
            Network.FakeApiServer.Request request = server.Single();
            Assert.Equal(("event/my", "Bearer typed-token"), (request.Path, request.Authorization));
        }

        [AvaloniaFact]
        public async Task Test_checks_a_remote_api()
        {
            using Network.FakeApiServer server = new() { ResponseBody = """{"readers":[{"name":"Finish"}]}""" };
            AllowDialogs();
            AddApi(CHRONOKEEP_REMOTE_SELF, server.Url, token: "r");
            ApiPart row = Assert.Single(Rows(OpenPage()));

            Click(Find<Button>(row, "TestButton"));

            await WaitForDialog("1 remote reader found.");
        }

        [AvaloniaFact]
        public async Task Test_says_why_an_api_failed()
        {
            using Network.FakeApiServer server = new() { Status = System.Net.HttpStatusCode.Unauthorized, ResponseBody = """{"message":"Bad token."}""" };
            AllowDialogs();
            AddApi(CHRONOKEEP_RESULTS_SELF, server.Url, token: "bad");
            ApiPart row = Assert.Single(Rows(OpenPage()));

            Click(Find<Button>(row, "TestButton"));

            await WaitForDialog("Bad token.");
        }

        [AvaloniaFact]
        public async Task Test_shows_why_a_runsignup_key_failed()
        {
            using Network.FakeApiServer server = new() { ResponseBody = """{"error":{"error_code":6,"error_msg":"Invalid API key."}}""" };
            string savedBase = Chronokeep.Network.Runsignup.RunsignupHandlers.BaseUrl;
            Chronokeep.Network.Runsignup.RunsignupHandlers.BaseUrl = server.Url;
            try
            {
                AllowDialogs();
                AddApi(RUNSIGNUP, "bad", token: "nope");
                ApiPart row = Assert.Single(Rows(OpenPage()));

                Click(Find<Button>(row, "TestButton"));

                await WaitForDialog("Invalid API key.");
            }
            finally
            {
                Chronokeep.Network.Runsignup.RunsignupHandlers.BaseUrl = savedBase;
            }
        }
    }
}
