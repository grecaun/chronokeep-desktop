using System.Net;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronoKeepAPI;
using Chronokeep.Test.Network;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.API.Pages;
using Chronokeep.UI.API.Windows;
using Chronokeep.UI.MainPages;
using static Chronokeep.Constants.ApiConstants;

namespace Chronokeep.Test.UI
{
    /// <summary>
    /// The Link to API Event wizard (pick API → event → year), the edit/unlink window, and the
    /// Dashboard buttons that open them. The Runsignup link wizard is planned as a copy of this flow.
    /// API calls go to a local <see cref="FakeApiServer"/> registered as a self-hosted results API.
    /// </summary>
    public class ApiLinkWindowTests : UiTestBase
    {
        private readonly FakeApiServer server = new();

        private const string EventsJson =
            """{"events":[{"name":"Spring Fling","slug":"spring-fling","recent_time":"2026/06/15 08:00:00 -07:00"},{"name":"Fall Classic","slug":"fall-classic","recent_time":"2025/10/01 08:00:00 -07:00"}]}""";

        private const string YearsJson =
            """{"years":[{"year":"2025","date_time":"2025/06/15 08:00:00 -07:00","live":false,"days_allowed":1,"ranking_type":"gun"},{"year":"2026","date_time":"2026/06/15 08:00:00 -07:00","live":false,"days_allowed":1,"ranking_type":"gun"}]}""";

        public ApiLinkWindowTests()
        {
            server.Responder = request => request.Path switch
            {
                "event/my" => (HttpStatusCode.OK, EventsJson),
                "event-year/event" => (HttpStatusCode.OK, YearsJson),
                "event/add" => (HttpStatusCode.OK, """{"event":{"name":"Spring Fling","slug":"brand-new"}}"""),
                "event-year/add" => (HttpStatusCode.OK, """{"event":{"slug":"brand-new"},"event_year":{"year":"2026"}}"""),
                _ => (HttpStatusCode.NotFound, """{"message":"not found"}"""),
            };
        }

        public override void Dispose()
        {
            base.Dispose();
            server.Dispose();
        }

        // results_api is UNIQUE(api_url, api_auth_token) ON CONFLICT REPLACE, so a second API with the
        // same URL and token silently replaces the first. Tests that need two APIs pass distinct tokens.
        private int AddResultsApi(string nickname = "Local results", string token = "tok")
            => Db.AddApi(new ApiObject(0, CHRONOKEEP_RESULTS_SELF, server.Url, nickname, token, ""));

        private static Control Frame(Window window) => (Control)Find<ContentControl>(window, "ApiFrame").Content!;

        private ApiWindow OpenWizard() => Show(ApiWindow.NewWindow(MainWindow, Db));

        private async Task<ApiPage2> GoToEvents(ApiWindow window)
        {
            Click(Find<Button>(Assert.IsType<ApiPage1>(Frame(window)), "NextButton"));
            ApiPage2 page2 = Assert.IsType<ApiPage2>(Frame(window));
            await WaitUntil(() => Find<Panel>(page2, "EventPanel").IsVisible, because: "events to load");
            return page2;
        }

        private static async Task<ApiPage3> GoToYears(ApiWindow window, ApiPage2 page2)
        {
            Click(Find<Button>(page2, "NextButton"));
            await WaitUntil(() => Frame(window) is ApiPage3, because: "the year page");
            ApiPage3 page3 = (ApiPage3)Frame(window);
            await WaitUntil(() => Find<Panel>(page3, "YearPanel").IsVisible, because: "years to load");
            return page3;
        }

        [AvaloniaFact]
        public void Shows_the_error_page_when_no_results_api_exists()
        {
            SeedEvent();
            Db.AddApi(new ApiObject(0, CHRONOKEEP_REMOTE_SELF, server.Url, "Remote only", "tok", ""));

            ApiWindow window = OpenWizard();

            Assert.IsType<ApiErrorPage>(Frame(window));
        }

        [AvaloniaFact]
        public void Shows_the_error_page_when_no_event_is_selected()
        {
            AddResultsApi();

            Assert.IsType<ApiErrorPage>(Frame(OpenWizard()));
        }

        [AvaloniaFact]
        public void First_page_lists_only_results_apis_and_preselects_the_last_one_used()
        {
            SeedEvent();
            AddResultsApi("First", "tok-1");
            int second = AddResultsApi("Second", "tok-2");
            Db.AddApi(new ApiObject(0, CHRONOKEEP_REMOTE_SELF, "https://remote.example/", "Remote", "r", ""));
            Db.SetAppSetting(Constants.Settings.LAST_USED_API_ID, second.ToString());

            ApiPage1 page1 = Assert.IsType<ApiPage1>(Frame(OpenWizard()));
            ComboBox box = Find<ComboBox>(page1, "ApiBox");

            Assert.Equal(["First", "Second"], box.Items.OfType<ComboBoxItem>().Select(i => (string)i.Content!));
            Assert.Equal("Second", ((ComboBoxItem)box.SelectedItem!).Content);
        }

        [AvaloniaFact]
        public async Task Event_page_lists_new_event_first_and_preselects_the_event_with_the_same_name()
        {
            SeedEvent("Spring Fling");
            AddResultsApi();
            ApiWindow window = OpenWizard();

            ApiPage2 page2 = await GoToEvents(window);
            ListBox events = Find<ListBox>(page2, "EventList");
            await WaitUntil(() => events.SelectedItem != null, because: "the matching event to be selected");

            List<ApiEvent> listed = [.. events.ItemsSource!.Cast<ApiEvent>()];
            Assert.Equal("New Event", listed[0].Name);
            Assert.Equal("spring-fling", ((ApiEvent)events.SelectedItem!).Slug);
            Assert.Equal("spring-fling", Find<TextBox>(page2, "SlugBox").Text);
            Assert.Equal("Bearer tok", server.Requests.Single(r => r.Path == "event/my").Authorization);
        }

        [AvaloniaFact]
        public async Task Search_box_filters_events_but_keeps_new_event()
        {
            SeedEvent("Spring Fling");
            AddResultsApi();
            ApiPage2 page2 = await GoToEvents(OpenWizard());

            Find<TextBox>(page2, "SearchBox").Text = "fall";
            Pump();

            List<ApiEvent> listed = [.. Find<ListBox>(page2, "EventList").ItemsSource!.Cast<ApiEvent>()];
            Assert.Equal(["New Event", "Fall Classic"], listed.Select(e => e.Name));
        }

        [AvaloniaFact]
        public async Task Linking_an_existing_event_and_year_saves_the_link_and_closes()
        {
            SeedEvent("Spring Fling");
            int apiId = AddResultsApi();
            ApiWindow window = OpenWizard();
            ApiPage2 page2 = await GoToEvents(window);
            await WaitUntil(() => Find<ListBox>(page2, "EventList").SelectedItem != null);

            ApiPage3 page3 = await GoToYears(window, page2);
            ComboBox years = Find<ComboBox>(page3, "YearCopyBox");
            Assert.Equal("2026", ((ComboBoxItem)years.SelectedItem!).Tag);
            Assert.False(Find<Panel>(page3, "NewPanel").IsVisible);

            Click(Find<Button>(page3, "FinishButton"));
            await WaitUntil(() => !window.IsVisible, because: "the wizard to close");

            Event linked = Db.GetCurrentEvent()!;
            Assert.Equal(apiId, linked.ApiId);
            Assert.Equal("spring-fling,2026", linked.ApiEventId);
            Assert.Equal(1, MainWindow.NetworkUpdateResultsCalls);
            Assert.Equal(apiId.ToString(), Db.GetAppSetting(Constants.Settings.LAST_USED_API_ID)!.Value);
        }

        [AvaloniaFact]
        public async Task Choosing_new_event_and_new_year_creates_both_on_the_server()
        {
            SeedEvent("Unlisted Race");
            AddResultsApi();
            ApiWindow window = OpenWizard();
            ApiPage2 page2 = await GoToEvents(window);
            ListBox events = Find<ListBox>(page2, "EventList");
            await WaitUntil(() => events.SelectedIndex == 0, because: "New Event to be selected");
            Assert.True(Find<Panel>(page2, "NewPanel").IsVisible);
            Assert.Equal("unlisted-race", Find<TextBox>(page2, "SlugBox").Text);

            ApiPage3 page3 = await GoToYears(window, page2);
            Find<ComboBox>(page3, "YearCopyBox").SelectedIndex = 0;
            Pump();
            Assert.True(Find<Panel>(page3, "NewPanel").IsVisible);
            Click(Find<Button>(page3, "FinishButton"));
            await WaitUntil(() => !window.IsVisible, because: "the wizard to close");

            JsonElement added = JsonDocument.Parse(server.Requests.Single(r => r.Path == "event/add").Body).RootElement;
            Assert.Equal("unlisted-race", added.GetProperty("event").GetProperty("slug").GetString());
            Assert.Contains(server.Requests, r => r.Path == "event-year/add");
            Assert.Equal("brand-new,2026", Db.GetCurrentEvent()!.ApiEventId);
        }

        [AvaloniaFact]
        public async Task A_server_error_while_loading_events_closes_the_wizard_without_linking()
        {
            SeedEvent("Spring Fling");
            AddResultsApi();
            server.Responder = _ => (HttpStatusCode.Unauthorized, """{"message":"bad token"}""");
            ApiWindow window = OpenWizard();

            Click(Find<Button>(Assert.IsType<ApiPage1>(Frame(window)), "NextButton"));
            await WaitUntil(() => !window.IsVisible, because: "the wizard to close on error");

            Assert.Equal(NULL_ID, Db.GetCurrentEvent()!.ApiId);
        }

        private Event LinkCurrentEvent()
        {
            Event theEvent = Db.GetCurrentEvent()!;
            theEvent.ApiId = AddResultsApi();
            theEvent.ApiEventId = "spring-fling,2026";
            Db.UpdateEvent(theEvent);
            return theEvent;
        }

        [AvaloniaFact]
        public void Edit_window_unlink_clears_the_link_and_notifies_the_main_window()
        {
            SeedEvent();
            LinkCurrentEvent();
            EditApiWindow window = Show(EditApiWindow.NewWindow(MainWindow, Db));
            EditApiPage1 page = Assert.IsType<EditApiPage1>((Control)Find<ContentControl>(window, "EditApiFrame").Content!);

            Click(Find<Button>(page, "UnlinkButton"));

            Event after = Db.GetCurrentEvent()!;
            Assert.Equal(NULL_ID, after.ApiId);
            Assert.Equal(NULL_EVENT_ID, after.ApiEventId);
            Assert.Equal(1, MainWindow.NetworkUpdateResultsCalls);
            Assert.False(window.IsVisible);
        }

        [AvaloniaFact]
        public void Edit_window_shows_the_error_page_for_an_unlinked_event()
        {
            SeedEvent();
            EditApiWindow window = Show(EditApiWindow.NewWindow(MainWindow, Db));

            Assert.IsType<EditApiErrorPage>(Find<ContentControl>(window, "EditApiFrame").Content);
        }

        [AvaloniaFact]
        public void Dashboard_link_button_reflects_whether_the_event_is_linked()
        {
            SeedEvent();
            DashboardPage unlinked = new(MainWindow, Db);
            Host(unlinked);
            Assert.Equal("Link to API Event", ContentText(Find<Button>(unlinked, "ApiLinkButton")));

            LinkCurrentEvent();
            DashboardPage linked = new(MainWindow, Db);
            Host(linked);
            Assert.Equal("Event Linked", ContentText(Find<Button>(linked, "ApiLinkButton")));
        }

        [AvaloniaFact]
        public void Dashboard_link_button_opens_the_wizard_or_the_edit_window()
        {
            SeedEvent();
            AddResultsApi();
            ShowMainWindow();
            DashboardPage page = new(MainWindow, Db);
            Host(page);

            Click(Find<Button>(page, "ApiLinkButton"));
            AddedWindow<ApiWindow>().Close();

            // The Dashboard keeps the event it loaded, so a newly linked event needs a fresh page.
            LinkCurrentEvent();
            DashboardPage linkedPage = new(MainWindow, Db);
            Host(linkedPage);
            Click(Find<Button>(linkedPage, "ApiLinkButton"));
            AddedWindow<EditApiWindow>().Close();
            Pump();
        }

        [AvaloniaFact]
        public void Dashboard_manage_api_button_switches_to_the_api_page()
        {
            SeedEvent();
            DashboardPage page = new(MainWindow, Db);
            Host(page);

            Click(Find<Button>(page, "ApiPageButton"));

            Assert.IsType<Chronokeep.UI.MainPages.Dashboard.ApiPage>(Assert.Single(MainWindow.SwitchedPages));
        }

        [AvaloniaFact]
        public async Task Double_clicking_an_event_moves_on_to_its_years()
        {
            SeedEvent("Spring Fling");
            AddResultsApi();
            ApiWindow window = OpenWizard();
            ApiPage2 page2 = await GoToEvents(window);
            ListBox events = Find<ListBox>(page2, "EventList");
            ApiEvent springFling = events.ItemsSource!.Cast<ApiEvent>().Single(e => e.Slug == "spring-fling");

            DoubleClick(RowOf(events, springFling));

            await WaitUntil(() => Frame(window) is ApiPage3, because: "the year page");
        }
    }
}
