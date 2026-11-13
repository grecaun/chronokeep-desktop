using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Interfaces.Timing;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronokeepRemote;
using Chronokeep.Test.Network;
using Chronokeep.Test.Timing;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.Timing.Remote;
using Chronokeep.UI.API.Parts;
using Chronokeep.UI.API.Windows;
using Chronokeep.UI.Parts;
using Chronokeep.UI.Timing.Windows;
using System.Globalization;
using System.Net;
using System.Text.Json;
using static Chronokeep.Constants.ApiConstants;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.UI
{
    /// <summary>Requests a fake server received, copied so the test can read them while the server keeps running.</summary>
    internal static class ServerRequests
    {
        public static List<FakeApiServer.Request> Of(FakeApiServer server)
        {
            lock (server.Requests)
            {
                return [.. server.Requests];
            }
        }

        public static Dictionary<string, string> Form(string body) =>
            body.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split('=', 2))
                .ToDictionary(p => WebUtility.UrlDecode(p[0]), p => WebUtility.UrlDecode(p.Length > 1 ? p[1] : ""));
    }

    /// <summary>
    /// The Remote Readers window: readers each remote API reports, which of them download reads
    /// automatically, and rewinding or deleting a reader's reads on the API.
    /// </summary>
    public class RemoteReadersWindowTests : UiTestBase
    {
        private readonly FakeApiServer server = new();
        private readonly FakeApiServer otherServer = new();
        private Event theEvent = null!;

        private sealed class Subscriber : IRemoteReadersChangeSubscriber
        {
            public int Calls;
            public void NotifyRemoteReadersChange() => Calls++;
        }

        public RemoteReadersWindowTests()
        {
            theEvent = AddEvent(makeCurrent: true);
            theEvent.CommonStartFinish = false;
            Db.UpdateEvent(theEvent);
            theEvent = Db.GetCurrentEvent()!;
            server.Responder = request => request.Path switch
            {
                "readers" => (HttpStatusCode.OK, """{"readers":[{"name":"Finish A"},{"name":"Finish B"}]}"""),
                _ => (HttpStatusCode.OK, "{}"),
            };
        }

        public override void Dispose()
        {
            base.Dispose();
            server.Dispose();
            otherServer.Dispose();
        }

        private int AddRemoteApi(FakeApiServer api, string name = "Remote", string type = CHRONOKEEP_REMOTE_SELF) =>
            Db.AddApi(new ApiObject(0, type, api.Url, name, "tok", ""));

        private async Task<RemoteReadersWindow> Open()
        {
            RemoteReadersWindow window = Show(RemoteReadersWindow.CreateWindow(MainWindow, Db));
            await WaitUntil(() => !Find<StackPanel>(window, "LoadingPanel").IsVisible || !window.IsVisible, because: "the readers to load");
            return window;
        }

        private static List<ApiExpanderPart> Apis(RemoteReadersWindow window) =>
            [.. Find<ListBox>(window, "ApiListView").Items.Cast<ApiExpanderPart>()];

        private static List<ReaderListItem> Readers(RemoteReadersWindow window) =>
            [.. Apis(window).SelectMany(a => Find<ListBox>(a, "ReaderListView").Items.Cast<ReaderListItem>())];

        private static ReaderListItem Reader(RemoteReadersWindow window, string name) =>
            Readers(window).Single(r => Find<TextBlock>(r, "NameBlock").Text == name);

        private static void ChooseLocation(ReaderListItem item, string name)
        {
            ComboBox box = Find<ComboBox>(item, "LocationBox");
            box.SelectedItem = box.Items.Cast<ComboBoxItem>().Single(i => i.Content!.ToString() == name);
        }

        [AvaloniaFact]
        public async Task Lists_the_readers_of_each_remote_api_only()
        {
            AddRemoteApi(server, "Remote One");
            Db.AddApi(new ApiObject(0, CHRONOKEEP_RESULTS_SELF, otherServer.Url, "Results", "tok", ""));

            RemoteReadersWindow window = await Open();

            ApiExpanderPart api = Assert.Single(Apis(window));
            Assert.Equal("Remote One", Find<TextBlock>(api, "ApiNameBlock").Text);
            Assert.Equal(["Finish A", "Finish B"], Readers(window).Select(r => Find<TextBlock>(r, "NameBlock").Text));
            Assert.Empty(ServerRequests.Of(otherServer));
            Assert.True(Find<ListBox>(window, "ApiListView").IsVisible);
        }

        [AvaloniaFact]
        public async Task New_readers_start_switched_off_at_the_first_location()
        {
            AddRemoteApi(server);

            RemoteReadersWindow window = await Open();

            ReaderListItem a = Reader(window, "Finish A");
            Assert.False(Find<ToggleSwitch>(a, "AutoFetch").IsChecked);
            Assert.Equal(["Start", "Finish", "Announcer"],
                Find<ComboBox>(a, "LocationBox").Items.Cast<ComboBoxItem>().Select(i => i.Content!.ToString()));
            Assert.Equal("Start", (Find<ComboBox>(a, "LocationBox").SelectedItem as ComboBoxItem)!.Content);
        }

        [AvaloniaFact]
        public async Task Saved_readers_show_switched_on_at_their_location()
        {
            int apiId = AddRemoteApi(server);
            Db.AddRemoteReaders(theEvent.Identifier, [new RemoteReader("Finish B", apiId, TimingConstants.LOCATION_FINISH, theEvent.Identifier)]);

            RemoteReadersWindow window = await Open();

            ReaderListItem b = Reader(window, "Finish B");
            Assert.True(Find<ToggleSwitch>(b, "AutoFetch").IsChecked);
            Assert.Equal("Finish", (Find<ComboBox>(b, "LocationBox").SelectedItem as ComboBoxItem)!.Content);
            Assert.False(Find<ToggleSwitch>(Reader(window, "Finish A"), "AutoFetch").IsChecked);
        }

        [AvaloniaFact]
        public async Task Close_saves_switched_on_readers_and_forgets_switched_off_ones()
        {
            int apiId = AddRemoteApi(server);
            Db.AddRemoteReaders(theEvent.Identifier, [new RemoteReader("Finish B", apiId, TimingConstants.LOCATION_FINISH, theEvent.Identifier)]);
            Subscriber subscriber = new();
            RemoteReadersNotifier.GetRemoteReadersNotifier().Subscribe(subscriber);
            try
            {
                RemoteReadersWindow window = await Open();
                ReaderListItem a = Reader(window, "Finish A");
                Find<ToggleSwitch>(a, "AutoFetch").IsChecked = true;
                ChooseLocation(a, "Announcer");
                Find<ToggleSwitch>(Reader(window, "Finish B"), "AutoFetch").IsChecked = false;

                Click(Find<Button>(window, "DoneButton"));

                RemoteReader saved = Assert.Single(Db.GetRemoteReaders(theEvent.Identifier));
                Assert.Equal("Finish A", saved.Name);
                Assert.Equal(TimingConstants.LOCATION_ANNOUNCER, saved.LocationId);
                Assert.Equal(apiId, saved.ApiiDentifier);
                Assert.Equal(1, subscriber.Calls);
                Assert.False(window.IsVisible);
            }
            finally
            {
                RemoteReadersNotifier.GetRemoteReadersNotifier().Unsubscribe(subscriber);
            }
        }

        [AvaloniaFact]
        public async Task Opening_twice_gives_the_same_window_until_it_closes()
        {
            AddRemoteApi(server);
            RemoteReadersWindow first = await Open();

            Assert.Same(first, RemoteReadersWindow.CreateWindow(MainWindow, Db));
            first.Close();
            Pump();

            RemoteReadersWindow second = RemoteReadersWindow.CreateWindow(MainWindow, Db);
            Assert.NotSame(first, second);
            Show(second);
        }

        [AvaloniaFact]
        public async Task One_unreachable_api_still_lists_the_others()
        {
            AddRemoteApi(otherServer, "Broken");
            AddRemoteApi(server, "Working");
            otherServer.Responder = _ => (HttpStatusCode.InternalServerError, """{"message":"down"}""");

            RemoteReadersWindow window = await Open();

            Assert.True(window.IsVisible);
            Assert.Equal(["Finish A", "Finish B"], Readers(window).Select(r => Find<TextBlock>(r, "NameBlock").Text));
        }

        [AvaloniaFact]
        public async Task Rewind_downloads_the_readers_reads_for_the_chosen_range()
        {
            AddRemoteApi(server);
            long eight = new DateTimeOffset(new DateTime(2026, 6, 15, 8, 0, 0)).ToUnixTimeSeconds();
            server.Responder = request => request.Path switch
            {
                "readers" => (HttpStatusCode.OK, """{"readers":[{"name":"Finish A"}]}"""),
                "reads" => (HttpStatusCode.OK, $$"""{"count":2,"reads":[{"identifier":"1001","seconds":{{eight}},"milliseconds":250,"ident_type":"chip","type":"reader","antenna":1,"reader":"Finish A"},{"identifier":"42","seconds":{{eight + 60}},"milliseconds":0,"ident_type":"bib","type":"manual","antenna":0,"reader":"Finish A"}]}"""),
                _ => (HttpStatusCode.OK, "{}"),
            };
            RemoteReadersWindow window = await Open();
            ReaderListItem a = Reader(window, "Finish A");
            ChooseLocation(a, "Finish");
            Find<CalendarDatePicker>(a, "StartDatePicker").SelectedDate = new DateTime(2026, 6, 15);
            Find<CalendarDatePicker>(a, "EndDatePicker").SelectedDate = new DateTime(2026, 6, 15);
            Find<MaskedTextBox>(a, "StartTimeBox").Text = "07:30:00";
            Find<MaskedTextBox>(a, "EndTimeBox").Text = "09:00:00";

            Click(Find<Button>(a, "RewindButton"));
            await WaitUntil(() => MainWindow.UpdateTimingFromControllerCalls == 1, because: "the rewind to finish");

            JsonElement body = JsonDocument.Parse(ServerRequests.Of(server).Single(r => r.Path == "reads").Body).RootElement;
            Assert.Equal("Finish A", body.GetProperty("reader").GetString());
            Assert.Equal(new DateTimeOffset(new DateTime(2026, 6, 15, 7, 30, 0)).ToUnixTimeSeconds(), body.GetProperty("start").GetInt64());
            Assert.Equal(new DateTimeOffset(new DateTime(2026, 6, 15, 9, 0, 0)).ToUnixTimeSeconds(), body.GetProperty("end").GetInt64());
            List<ChipRead> reads = Db.GetChipReadsSafemode(theEvent.Identifier);
            Assert.Equal(2, reads.Count);
            Assert.All(reads, r => Assert.Equal(TimingConstants.LOCATION_FINISH, r.LocationId));
            Assert.Contains(reads, r => r.ChipNumber == "1001");
            Assert.Contains(reads, r => r.ReadBib == "42");
        }

        [AvaloniaFact]
        public async Task Rewind_error_downloads_nothing()
        {
            AddRemoteApi(server);
            server.Responder = request => request.Path == "readers"
                ? (HttpStatusCode.OK, """{"readers":[{"name":"Finish A"}]}""")
                : (HttpStatusCode.InternalServerError, """{"message":"boom"}""");
            RemoteReadersWindow window = await Open();

            Click(Find<Button>(Reader(window, "Finish A"), "RewindButton"));
            await WaitUntil(() => ServerRequests.Of(server).Any(r => r.Path == "reads"), because: "the rewind request");
            await Settle();

            Assert.Empty(Db.GetChipReadsSafemode(theEvent.Identifier));
            Assert.Equal(0, MainWindow.UpdateTimingFromControllerCalls);
        }

        [AvaloniaFact]
        public async Task Delete_asks_first_then_deletes_the_readers_reads_in_range()
        {
            AddRemoteApi(server);
            server.Responder = request => request.Path switch
            {
                "readers" => (HttpStatusCode.OK, """{"readers":[{"name":"Finish A"}]}"""),
                "reads/delete" => (HttpStatusCode.OK, """{"count":12}"""),
                _ => (HttpStatusCode.OK, "{}"),
            };
            RemoteReadersWindow window = await Open();
            ReaderListItem a = Reader(window, "Finish A");

            Click(Find<Button>(a, "DeleteButton"));
            await Settle();
            Assert.DoesNotContain(ServerRequests.Of(server), r => r.Path == "reads/delete");

            await a.DeleteRemoteReads();

            FakeApiServer.Request delete = ServerRequests.Of(server).Single(r => r.Path == "reads/delete");
            Assert.Equal("DELETE", delete.Method);
            Assert.Equal("Finish A", JsonDocument.Parse(delete.Body).RootElement.GetProperty("reader").GetString());
            Assert.Equal(1, MainWindow.UpdateTimingFromControllerCalls);
        }
    }

    /// <summary>The Chronoclock control window: saved clocks, and the commands each clock row sends.</summary>
    public class ClockControlTests : UiTestBase
    {
        private readonly FakeApiServer clockServer = new();
        private Event theEvent = null!;
        private string ClockUrl => clockServer.Url["http://".Length..].TrimEnd('/');

        public ClockControlTests()
        {
            theEvent = AddEvent(makeCurrent: true);
            Answer();
        }

        public override void Dispose()
        {
            base.Dispose();
            clockServer.Dispose();
        }

        /// <summary>The clock's answers: its config, and what each change returns.</summary>
        private void Answer(uint brightness = 5, bool locked = false, long countdown = 0, HttpStatusCode changes = HttpStatusCode.OK)
        {
            string state = $$"""{"brightness":{{brightness}},"flipDisplay":false,"twelveHour":false,"lockCountUpDown":{{(locked ? "true" : "false")}},"countupdownTimestamp":{{countdown}}}""";
            clockServer.Responder = request => request.Path switch
            {
                "config.json" => (HttpStatusCode.OK, state),
                "get_time" or "set_time" => (HttpStatusCode.OK, """{"time":"2026-06-15 08:30:05"}"""),
                _ when changes != HttpStatusCode.OK => (changes, """{"error":"clock busy"}"""),
                "set_lock" => (HttpStatusCode.OK, state.Replace("\"lockCountUpDown\":false", "\"lockCountUpDown\":true")),
                _ => (HttpStatusCode.OK, state),
            };
        }

        private Chronoclock AddClock(string name = "Finish Clock", string? url = null, bool enabled = true)
        {
            Chronoclock clock = new() { Name = name, Url = url ?? ClockUrl, Enabled = enabled };
            clock.Identifier = Db.AddClock(clock);
            return clock;
        }

        private ClockControl Open() => Show(ClockControl.CreateWindow(MainWindow, Db));

        private static List<ClockPart> Rows(ClockControl window) =>
            [.. Find<ListBox>(window, "ClockListView").Items.Cast<ClockPart>()];

        private async Task<ClockPart> OpenReadyRow()
        {
            AddClock();
            ClockPart row = Rows(Open()).Single();
            await WaitUntil(() => Find<Button>(row, "GetTime").IsEnabled, because: "the clock's config to load");
            return row;
        }

        private List<FakeApiServer.Request> Sent(string path) => [.. ServerRequests.Of(clockServer).Where(r => r.Path == path)];

        [AvaloniaFact]
        public void Lists_saved_clocks()
        {
            AddClock("Start Clock", "start.local", enabled: false);
            AddClock("Finish Clock", "finish.local");

            ClockControl window = Open();

            List<ClockPart> rows = Rows(window);
            Assert.Equal(["Start Clock", "Finish Clock"], rows.Select(r => Find<TextBox>(r, "NameBlock").Text));
            Assert.Equal(["start.local", "finish.local"], rows.Select(r => Find<TextBox>(r, "UrlBlock").Text));
            Assert.Equal([false, true], rows.Select(r => Find<ToggleSwitch>(r, "EnabledSwitch").IsChecked == true));
        }

        [AvaloniaFact]
        public async Task Controls_unlock_once_the_clock_answers()
        {
            Answer(brightness: 7);

            ClockPart row = await OpenReadyRow();

            Assert.Equal(6, Find<ComboBox>(row, "BrightnessBox").SelectedIndex);
            Assert.True(Find<Button>(row, "Start").IsEnabled);
            Assert.True(Find<Button>(row, "SetTime").IsEnabled);
            Assert.Empty(Sent("set_brightness"));
        }

        [AvaloniaFact]
        public async Task Unreachable_clock_keeps_its_controls_locked()
        {
            using FakeApiServer gone = new();
            string url = gone.Url["http://".Length..].TrimEnd('/');
            gone.Dispose();
            AddClock(url: url);

            ClockPart row = Rows(Open()).Single();
            await Settle(1000);

            Assert.False(Find<Button>(row, "Start").IsEnabled);
            Assert.False(Find<Button>(row, "GetTime").IsEnabled);
        }

        [AvaloniaFact]
        public async Task A_locked_clock_shows_locked_and_cant_be_started()
        {
            Answer(locked: true);

            ClockPart row = await OpenReadyRow();

            Assert.True(Find<PathIcon>(row, "LockedImage").IsVisible);
            Assert.False(Find<Button>(row, "Start").IsEnabled);
            Assert.False(Find<Button>(row, "Stop").IsEnabled);
        }

        [AvaloniaFact]
        public async Task Clock_countdown_time_fills_the_date_and_time()
        {
            DateTime countdown = new(2026, 6, 15, 7, 45, 30);
            Answer(countdown: new DateTimeOffset(countdown).ToUnixTimeSeconds());

            ClockPart row = await OpenReadyRow();

            Assert.Equal(countdown.Date, Find<CalendarDatePicker>(row, "CountDatePicker").SelectedDate?.Date);
            Assert.Equal("07:45:30", Find<MaskedTextBox>(row, "CountTimeBox").Text);
        }

        [AvaloniaFact]
        public async Task Event_start_time_fills_in_when_the_clock_has_none()
        {
            theEvent.StartSeconds = 8 * 3600 + 15 * 60;
            Db.UpdateEvent(theEvent);

            ClockPart row = await OpenReadyRow();

            Assert.Equal(DateTime.Parse(theEvent.Date).Date, Find<CalendarDatePicker>(row, "CountDatePicker").SelectedDate?.Date);
            Assert.Equal("08:15:00", Find<MaskedTextBox>(row, "CountTimeBox").Text);
        }

        [AvaloniaFact]
        public async Task Lock_tells_the_clock_and_shows_its_answer()
        {
            ClockPart row = await OpenReadyRow();

            Click(Find<Button>(row, "LockedSwitch"));
            await WaitUntil(() => Sent("set_lock").Count == 1 && Find<Button>(row, "GetTime").IsEnabled, because: "the lock to apply");

            Assert.Equal("1", ServerRequests.Form(Sent("set_lock")[0].Body)["value"]);
            Assert.True(Find<PathIcon>(row, "LockedImage").IsVisible);
            Assert.False(Find<Button>(row, "Start").IsEnabled);
        }

        [AvaloniaFact]
        public async Task Failed_lock_leaves_the_row_usable_and_unlocked()
        {
            ClockPart row = await OpenReadyRow();
            Answer(changes: HttpStatusCode.InternalServerError);

            Click(Find<Button>(row, "LockedSwitch"));
            await WaitUntil(() => Sent("set_lock").Count == 1, because: "the lock request");
            await Settle();

            Assert.True(Find<Button>(row, "GetTime").IsEnabled);
            Assert.True(Find<Button>(row, "Start").IsEnabled);
            Assert.False(Find<PathIcon>(row, "LockedImage").IsVisible);
        }

        [AvaloniaFact]
        public async Task Brightness_change_tells_the_clock()
        {
            ClockPart row = await OpenReadyRow();
            Answer(brightness: 3);

            Find<ComboBox>(row, "BrightnessBox").SelectedIndex = 2;
            await WaitUntil(() => Sent("set_brightness").Count == 1 && Find<Button>(row, "GetTime").IsEnabled, because: "the brightness to apply");

            Assert.Equal("3", ServerRequests.Form(Sent("set_brightness")[0].Body)["value"]);
        }

        [AvaloniaFact]
        public async Task Failed_brightness_change_leaves_the_row_usable()
        {
            ClockPart row = await OpenReadyRow();
            Answer(changes: HttpStatusCode.InternalServerError);

            Find<ComboBox>(row, "BrightnessBox").SelectedIndex = 2;
            await WaitUntil(() => Sent("set_brightness").Count == 1, because: "the brightness request");
            await Settle();

            Assert.True(Find<Button>(row, "GetTime").IsEnabled);
            Assert.True(Find<ComboBox>(row, "BrightnessBox").IsEnabled);
        }

        [AvaloniaFact]
        public async Task Refresh_reads_the_clock_without_changing_it()
        {
            ClockPart row = await OpenReadyRow();
            Answer(brightness: 9);

            Click(Find<Button>(row, "Refresh"));
            await WaitUntil(() => Sent("config.json").Count == 2 && Find<ComboBox>(row, "BrightnessBox").SelectedIndex == 8, because: "the refresh");
            await Settle();

            Assert.Empty(Sent("set_brightness"));
        }

        [AvaloniaTheory]
        [InlineData("en-US")]
        [InlineData("fi-FI")]
        [InlineData("de-DE")]
        public async Task Start_sends_the_countdown_time_in_the_clocks_format(string culture)
        {
            ClockPart row = await OpenReadyRow();
            Find<CalendarDatePicker>(row, "CountDatePicker").SelectedDate = new DateTime(2026, 6, 15);
            Find<MaskedTextBox>(row, "CountTimeBox").Text = "07:45:30";

            CultureInfo old = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            try
            {
                Click(Find<Button>(row, "Start"));
                await WaitUntil(() => Sent("set_countupdown").Count == 1, because: "the start request");
            }
            finally
            {
                CultureInfo.CurrentCulture = old;
            }

            Assert.Equal("2026-06-15 07:45:30", ServerRequests.Form(Sent("set_countupdown")[0].Body)["DateTime"]);
        }

        [AvaloniaTheory]
        [InlineData("en-US")]
        [InlineData("fi-FI")]
        public async Task Set_time_sends_now_and_shows_the_clocks_time(string culture)
        {
            ClockPart row = await OpenReadyRow();
            ClockControl window = (ClockControl)TopLevel.GetTopLevel(row)!;

            CultureInfo old = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            try
            {
                Click(Find<Button>(row, "SetTime"));
                await WaitUntil(() => Find<TextBlock>(window, "TimeLabel").IsVisible, because: "the clock to answer");
            }
            finally
            {
                CultureInfo.CurrentCulture = old;
            }

            DateTime sent = DateTime.ParseExact(ServerRequests.Form(Sent("set_time")[0].Body)["DateTime"], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            Assert.True(Math.Abs((sent - DateTime.Now).TotalSeconds) < 5);
            Assert.Equal("Clock time is 2026-06-15 08:30:05", Find<TextBlock>(window, "TimeLabel").Text);
        }

        [AvaloniaFact]
        public async Task Get_time_and_stop_reach_the_clock()
        {
            ClockPart row = await OpenReadyRow();
            ClockControl window = (ClockControl)TopLevel.GetTopLevel(row)!;

            Click(Find<Button>(row, "GetTime"));
            await WaitUntil(() => Find<TextBlock>(window, "TimeLabel").IsVisible, because: "the clock's time");
            Click(Find<Button>(row, "Stop"));
            await WaitUntil(() => Sent("stop").Count == 1, because: "the stop request");

            Assert.Single(Sent("get_time"));
        }

        [AvaloniaFact]
        public void Add_saves_a_new_disabled_clock()
        {
            ClockControl window = Open();

            Click(Find<Button>(window, "AddButton"));

            Chronoclock saved = Assert.Single(Db.GetClocks());
            Assert.Equal("New Clock", saved.Name);
            Assert.False(saved.Enabled);
            Assert.Equal("New Clock", Find<TextBox>(Assert.Single(Rows(window)), "NameBlock").Text);
        }

        [AvaloniaFact]
        public void Delete_removes_the_clock()
        {
            AddClock("Start Clock", "start.local");
            AddClock("Finish Clock", "finish.local");
            ClockControl window = Open();

            Click(Find<Button>(Rows(window)[0], "Delete"));

            Assert.Equal(["Finish Clock"], Db.GetClocks().Select(c => c.Name));
            Assert.Equal(["Finish Clock"], Rows(window).Select(r => Find<TextBox>(r, "NameBlock").Text));
        }

        [AvaloniaFact]
        public void Edits_kept_when_another_clock_is_deleted_and_saved_on_close()
        {
            AddClock("Start Clock", "start.local");
            AddClock("Finish Clock", "finish.local", enabled: false);
            ClockControl window = Open();
            ClockPart finish = Rows(window)[1];
            Find<TextBox>(finish, "NameBlock").Text = "Line Clock";
            Find<ToggleSwitch>(finish, "EnabledSwitch").IsChecked = true;

            Click(Find<Button>(Rows(window)[0], "Delete"));
            Assert.Equal("Line Clock", Find<TextBox>(Rows(window).Single(), "NameBlock").Text);
            Click(Find<Button>(window, "DoneButton"));

            Chronoclock saved = Assert.Single(Db.GetClocks());
            Assert.Equal("Line Clock", saved.Name);
            Assert.True(saved.Enabled);
            Assert.Equal(1, MainWindow.WindowFinalizeCalls);
        }

        [AvaloniaFact]
        public void Opening_twice_gives_the_same_window_until_it_closes()
        {
            ClockControl first = Open();

            Assert.Same(first, ClockControl.CreateWindow(MainWindow, Db));
            first.Close();
            Pump();

            Assert.NotSame(first, Show(ClockControl.CreateWindow(MainWindow, Db)));
        }
    }
}
