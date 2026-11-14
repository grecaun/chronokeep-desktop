using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Database;
using Chronokeep.Objects;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.EventWindows;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.MainPages.Timing;
using Chronokeep.UI.Participants;
using static Chronokeep.Constants.ApiConstants;

namespace Chronokeep.Test.UI
{
    /// <summary>The buttons and filters on the Timing page, the Dashboard, and the Participants page.</summary>
    public class PageButtonTests : UiTestBase
    {
        private TimingPage OpenTiming()
        {
            TimingPage page = new(MainWindow, Db);
            Host(page);
            return page;
        }

        private static object? Frame(TimingPage page) => Find<ContentControl>(page, "TimingFrame").Content;

        [AvaloniaFact]
        public void Start_race_sets_the_start_to_now_and_recalculates()
        {
            AddEvent(makeCurrent: true);
            TimingPage page = OpenTiming();
            DateTime before = DateTime.Now;

            Click(Find<Button>(page, "StartRace"));

            Event e = Db.GetCurrentEvent()!;
            DateTime start = before.Date.AddSeconds(e.StartSeconds).AddMilliseconds(e.StartMilliseconds);
            Assert.InRange(start, before.AddSeconds(-1), DateTime.Now.AddSeconds(1));
            Assert.False(Find<Button>(page, "StartRace").IsEnabled);
            Assert.Equal(1, MainWindow.NetworkClearResultsCalls);
            Assert.Equal(1, MainWindow.NotifyTimingWorkerCalls);
        }

        [AvaloniaFact]
        public void A_typed_start_time_is_saved_when_leaving_the_box()
        {
            AddEvent(makeCurrent: true);
            TimingPage page = OpenTiming();
            MaskedTextBox startTime = Find<MaskedTextBox>(page, "StartTime");

            startTime.Focus();
            startTime.Text = "07:30:15.250";
            Find<TextBox>(page, "SearchBox").Focus();
            Pump();

            Event e = Db.GetCurrentEvent()!;
            Assert.Equal(((7 * 3600) + (30 * 60) + 15, 250), (e.StartSeconds, e.StartMilliseconds));
            Assert.Equal(1, MainWindow.NetworkClearResultsCalls);
        }

        [AvaloniaFact]
        public void Re_entering_the_same_start_time_does_not_recalculate()
        {
            Event e = AddEvent(makeCurrent: true);
            e.StartSeconds = 8 * 3600;
            Db.UpdateEvent(e);
            TimingPage page = OpenTiming();
            MaskedTextBox startTime = Find<MaskedTextBox>(page, "StartTime");
            Assert.Equal("08:00:00.000", startTime.Text);
            Assert.False(Find<Button>(page, "StartRace").IsEnabled);

            startTime.Focus();
            Find<TextBox>(page, "SearchBox").Focus();
            Pump();

            Assert.Equal(0, MainWindow.NetworkClearResultsCalls);
        }

        [AvaloniaFact]
        public void Dns_mode_button_switches_between_start_and_stop()
        {
            AddEvent(makeCurrent: true);
            TimingPage page = OpenTiming();
            Button dns = Find<Button>(page, "DnsMode");
            Assert.Equal("Start DNS Mode", ContentText(dns));

            Click(dns);
            Assert.Equal("Stop DNS Mode", ContentText(dns));
            Assert.True(MainWindow.DnsMode);

            Click(dns);
            Assert.Equal("Start DNS Mode", ContentText(dns));
            Assert.False(MainWindow.DnsMode);
        }

        [AvaloniaFact]
        public void Raw_data_alarms_print_and_awards_buttons_switch_the_view()
        {
            AddEvent(makeCurrent: true);
            TimingPage page = OpenTiming();
            Button raw = Find<Button>(page, "RawButton");

            Click(raw);
            Assert.IsType<TimingRawReadsPage>(Frame(page));
            Assert.Equal("Refresh Data", ContentText(raw));

            Click(Find<Button>(page, "AlarmButton"));
            Assert.IsType<AlarmsPage>(Frame(page));
            Assert.Equal("Raw Data", ContentText(raw));

            Click(Find<Button>(page, "PrintButton"));
            Assert.IsType<PrintPage>(Frame(page));

            Click(Find<Button>(page, "AwardButton"));
            Assert.IsType<AwardPage>(Frame(page));
        }

        [AvaloniaFact]
        public void Auto_upload_button_starts_the_uploader()
        {
            AddEvent(makeCurrent: true);
            TimingPage page = OpenTiming();

            Click(Find<Button>(page, "AutoApiButton"));

            Assert.Equal(1, MainWindow.StartApiControllerCalls);
            Assert.Equal("Starting...", ContentText(Find<Button>(page, "AutoApiButton")));
        }

        [AvaloniaFact]
        public async Task Manual_upload_shows_how_many_results_have_gone()
        {
            using Network.FakeApiServer server = new();
            server.Responder = request => (System.Net.HttpStatusCode.OK,
                $$"""{"count":{{System.Text.Json.JsonDocument.Parse(request.Body).RootElement.GetProperty("results").GetArrayLength()}}}""");
            Event theEvent = AddEvent(makeCurrent: true);
            Db.SetAppSetting(Constants.Settings.PROGRAM_UNIQUE_MODIFIER, "XYZ");
            theEvent.ApiId = Db.AddApi(new ApiObject(0, Constants.ApiConstants.CHRONOKEEP_RESULTS_SELF, server.Url, "Local", "tok", ""));
            theEvent.ApiEventId = "race,2026";
            Db.UpdateEvent(theEvent);
            Distance d = AddDistance(theEvent.Identifier);
            int count = Constants.Timing.API_LOOP_COUNT + 5;
            for (int i = 1; i <= count; i++)
            {
                Participant p = AddParticipant(theEvent.Identifier, d, i.ToString(), first: $"R{i}");
                Db.AddTimingResult(new TimeResult(theEvent.Identifier, Constants.Timing.TIMERESULT_DUMMYREAD, p.EventSpecific.Identifier,
                    Constants.Timing.LOCATION_FINISH, Constants.Timing.SEGMENT_FINISH, 1, 1500 + i, 0, TimeResult.BibToIdentifier(i.ToString()),
                    1500 + i, 0, DateTime.Now, i.ToString(), Constants.Timing.TIMERESULT_STATUS_PROCESSED, "") { Place = i });
            }
            Chronokeep.Timing.API.ApiController.SetUploadableTrue(1000);
            TimingPage page = OpenTiming();
            Button manual = Find<Button>(page, "ManualApiButton");
            List<string> labels = LabelsOf(manual);

            Click(manual);
            await WaitUntil(() => labels.Count > 0 && ContentText(manual) == "Manual Upload", 10000, because: "the upload to finish");

            Assert.Equal(["Uploading", $"0 / {count}", $"{Constants.Timing.API_LOOP_COUNT} / {count}", $"{count} / {count}", "Manual Upload"], labels.Distinct());
            Assert.Empty(Db.GetNonUploadedResults(theEvent.Identifier));
        }

        [AvaloniaFact]
        public async Task Manual_upload_button_comes_back_when_there_is_nothing_to_upload_to()
        {
            AddEvent(makeCurrent: true);
            TimingPage page = OpenTiming();
            Button manual = Find<Button>(page, "ManualApiButton");

            Click(manual);
            await WaitUntil(() => ContentText(manual) == "Manual Upload", because: "the button to reset");
        }

        [AvaloniaFact]
        public async Task Location_filter_narrows_the_results()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance d = AddDistance(e.Identifier);
            Participant a = AddParticipant(e.Identifier, d, "1", first: "Ann");
            Participant b = AddParticipant(e.Identifier, d, "2", first: "Ben");
            int mile = Db.AddTimingLocation(new TimingLocation(e.Identifier, "Mile 6"));
            Db.AddTimingResults([
                new TimeResult(e.Identifier, Constants.Timing.TIMERESULT_DUMMYREAD, a.EventSpecific.Identifier,
                    Constants.Timing.LOCATION_FINISH, Constants.Timing.SEGMENT_FINISH, 1, 1800, 0, "", 1800, 0, DateTime.Now, "1", Constants.Timing.TIMERESULT_STATUS_NONE, ""),
                new TimeResult(e.Identifier, Constants.Timing.TIMERESULT_DUMMYREAD, b.EventSpecific.Identifier,
                    mile, Constants.Timing.SEGMENT_NONE, 1, 900, 0, "", 900, 0, DateTime.Now, "2", Constants.Timing.TIMERESULT_STATUS_NONE, ""),
            ]);
            TimingPage page = OpenTiming();
            ListBox results = Find<ListBox>((Control)Frame(page)!, "UpdateListView");
            await WaitUntil(() => results.ItemsSource!.Cast<TimeResult>().Count() == 2, because: "results to load");

            ComboBox locations = Find<ComboBox>(page, "LocationBox");
            locations.SelectedItem = locations.Items.OfType<ComboBoxItem>().Single(i => (string)i.Content! == "Mile 6");
            await WaitUntil(() => results.ItemsSource!.Cast<TimeResult>().Count() == 1, because: "the location filter");

            Assert.Equal("2", results.ItemsSource!.Cast<TimeResult>().Single().Bib);
        }

        private DashboardPage OpenDashboard()
        {
            DashboardPage page = new(MainWindow, Db);
            Host(page);
            return page;
        }

        [AvaloniaFact]
        public void Registration_button_shows_whether_registration_really_started()
        {
            SeedEvent();
            MainWindow.RegistrationCanStart = false;
            DashboardPage page = OpenDashboard();
            Button registration = Find<Button>(page, "RegistrationButton");

            Click(registration);
            Assert.Equal("Start Registration", ContentText(registration));

            MainWindow.RegistrationCanStart = true;
            Click(registration);
            Assert.Equal("Stop Registration", ContentText(registration));

            Click(registration);
            Assert.Equal("Start Registration", ContentText(registration));
            Assert.False(MainWindow.RegistrationRunning);
        }

        [AvaloniaFact]
        public void New_and_change_event_buttons_open_their_windows()
        {
            SeedEvent();
            ShowMainWindow();
            DashboardPage page = OpenDashboard();

            Click(Find<Button>(page, "NewEvent"));
            AddedWindow<NewEventWindow>().Close();
            Click(Find<Button>(page, "ChangeEvent"));
            AddedWindow<ChangeEventWindow>().Close();
            Pump();
        }

        [AvaloniaFact]
        public void Event_buttons_ask_first_while_background_work_is_running()
        {
            SeedEvent();
            ShowMainWindow();
            MainWindow.BackgroundRunning = true;
            DashboardPage page = OpenDashboard();

            // The confirmation needs the real main window, so nothing opens and nothing is deleted.
            Click(Find<Button>(page, "NewEvent"));
            Click(Find<Button>(page, "DeleteEvent"));

            Assert.Empty(MainWindow.AddedWindows);
            Assert.Single(Db.GetEvents());
        }

        [AvaloniaFact]
        public void Saving_an_event_to_another_database_copies_it_without_its_api_links()
        {
            (Event e, Distance half) = SeedEvent("Harvest Half");
            Distance relay = AddDistance(e.Identifier, "Relay");
            relay.LinkedDistance = half.Identifier;
            Db.UpdateDistance(relay);
            Db.AddTimingLocation(new TimingLocation(e.Identifier, "Mile 6"));
            Db.AddBibChipAssociation(e.Identifier, [new BibChipAssociation { EventId = e.Identifier, Bib = "100", Chip = "5100" }]);
            int api = Db.AddApi(new ApiObject(0, CHRONOKEEP_RESULTS_SELF, "https://results.example/", "Results", "tok", ""));
            e.ApiId = api;
            e.ApiEventId = "harvest,2026";
            e.RegistrationApiId = api;
            e.RegistrationRaceId = "123456";
            Db.UpdateEvent(e);
            string otherPath = Path.Combine(Path.GetDirectoryName(DbPath)!, "saved.sqlite");
            SqLiteInterface other = new(otherPath);
            other.Initialize();

            int copiedId = DashboardPage.Save_Event(Db.GetEvent(e.Identifier)!, Db, other);

            Event copied = other.GetEvent(copiedId)!;
            Assert.Equal("Harvest Half", copied.Name);
            Assert.Equal(NULL_ID, copied.ApiId);
            Assert.Equal(NULL_EVENT_ID, copied.ApiEventId);
            Assert.False(copied.IsRegistrationLinked);
            Dictionary<string, Distance> distances = other.GetDistances(copiedId).ToDictionary(d => d.Name);
            Assert.Equal(distances["5K"].Identifier, distances["Relay"].LinkedDistance);
            Assert.Contains(other.GetTimingLocations(copiedId), l => l.Name == "Mile 6");
            Assert.Equal("100", Assert.Single(other.GetBibChips(copiedId)).Bib);
            Assert.Equal("100", Assert.Single(other.GetParticipants(copiedId)).Bib);
        }

        private async Task<ParticipantsPage> OpenParticipants()
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

        private (Event Event, Distance FiveK, Distance TenK) SeedRunners()
        {
            Event e = AddEvent(makeCurrent: true);
            Distance fiveK = AddDistance(e.Identifier, "5K");
            Distance tenK = AddDistance(e.Identifier, "10K");
            AddParticipant(e.Identifier, fiveK, "30", first: "Cara", last: "Charlie");
            AddParticipant(e.Identifier, tenK, "10", first: "Abe", last: "Alpha");
            AddParticipant(e.Identifier, tenK, "20", first: "Bea", last: "Bravo");
            return (Db.GetCurrentEvent()!, fiveK, tenK);
        }

        private static async Task ShowDistance(ParticipantsPage page, Distance distance)
        {
            ComboBox box = Find<ComboBox>(page, "DistanceBox");
            box.SelectedItem = box.Items.OfType<ComboBoxItem>().Single(i => (string)i.Tag! == distance.Identifier.ToString());
            await WaitUntil(() => Listed(page).All(p => p.Distance == distance.Name), because: "the distance filter");
        }

        [AvaloniaFact]
        public async Task Sorting_keeps_the_distance_filter()
        {
            (_, _, Distance tenK) = SeedRunners();
            ParticipantsPage page = await OpenParticipants();
            await ShowDistance(page, tenK);

            Find<ComboBox>(page, "SortBox").SelectedItem = Find<ComboBox>(page, "SortBox").Items.OfType<ComboBoxItem>().Single(i => (string)i.Content! == "Bib");
            await Settle(200);

            Assert.Equal(["10", "20"], Listed(page).Select(p => p.Bib));
        }

        [AvaloniaFact]
        public async Task Searching_keeps_the_distance_filter()
        {
            (_, _, Distance tenK) = SeedRunners();
            ParticipantsPage page = await OpenParticipants();
            await ShowDistance(page, tenK);

            Find<TextBox>(page, "SearchBox").Text = "a";
            await Settle(200);

            Assert.All(Listed(page), p => Assert.Equal("10K", p.Distance));
            Assert.Equal(2, Listed(page).Count);
        }

        [AvaloniaFact]
        public async Task Remove_deletes_the_selected_participants()
        {
            (Event e, _, _) = SeedRunners();
            ParticipantsPage page = await OpenParticipants();
            DataGrid list = Find<DataGrid>(page, "ParticipantsList");
            list.SelectedItems.Add(Listed(page).Single(p => p.Bib == "20"));

            Click(Find<Button>(page, "Remove"));
            await WaitUntil(() => Listed(page).Count == 2, because: "the list to refresh");

            Assert.DoesNotContain(Db.GetParticipants(e.Identifier), p => p.Bib == "20");
        }

        [AvaloniaFact]
        public async Task Add_modify_and_conflicts_open_the_right_windows()
        {
            SeedRunners();
            ShowMainWindow();
            ParticipantsPage page = await OpenParticipants();
            DataGrid list = Find<DataGrid>(page, "ParticipantsList");

            Click(Find<Button>(page, "Add"));
            AddedWindow<ModifyParticipantWindow>().Close();

            list.SelectedItems.Add(Listed(page)[0]);
            Click(Find<Button>(page, "Modify"));
            AddedWindow<ModifyParticipantWindow>().Close();

            list.SelectedItems.Add(Listed(page)[1]);
            Click(Find<Button>(page, "Modify"));
            AddedWindow<ChangeMultiParticipantWindow>().Close();

            Click(Find<Button>(page, "ConflictsBtn"));
            AddedWindow<ParticipantConflicts>().Close();
            Pump();
            Assert.Equal(4, MainWindow.AddedWindows.Count);
        }
    }
}
