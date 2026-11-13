using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Chronokeep.Network.Runsignup;
using Chronokeep.Objects;
using Chronokeep.Test.Network;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.Participants;
using System.Net;
using RegistrationState = Chronokeep.Constants.Registration;

namespace Chronokeep.Test.UI
{
    /// <summary>
    /// The Participants page with a Runsignup link: downloading, and tinting people Runsignup removed
    /// the way the timing page tints their results.
    /// </summary>
    public class RunsignupParticipantsPageTests : UiTestBase
    {
        private readonly FakeApiServer server = new();
        private readonly string savedBase = RunsignupHandlers.BaseUrl;
        private readonly Event theEvent;
        private readonly Distance fiveK;

        public RunsignupParticipantsPageTests()
        {
            RunsignupHandlers.BaseUrl = server.Url;
            theEvent = AddEvent(makeCurrent: true);
            fiveK = AddDistance(theEvent.Identifier, "5K");
        }

        public override void Dispose()
        {
            RunsignupHandlers.BaseUrl = savedBase;
            server.Dispose();
            base.Dispose();
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

        private static DataGridRow RowFor(ParticipantsPage page, string first) =>
            Find<DataGrid>(page, "ParticipantsList").GetVisualDescendants().OfType<DataGridRow>()
                .Single(r => r.IsVisible && r.DataContext is Participant p && p.FirstName == first);

        private Participant Runner(string bib, string first, string removed = "")
        {
            Participant p = AddParticipant(theEvent.Identifier, fiveK, bib, first: first, last: "Lee");
            if (removed.Length > 0)
            {
                p.EventSpecific.Removed = removed;
                Db.UpdateParticipant(p);
            }
            return p;
        }

        private void Link()
        {
            theEvent.RegistrationApiId = Db.AddApi(new ApiObject(0, Constants.ApiConstants.RUNSIGNUP, "s3cret", "Timer", "key123", ""));
            theEvent.RegistrationRaceId = "42";
            Db.UpdateEvent(theEvent);
            Db.SetRegistrationDistances(theEvent.Identifier, [new(theEvent.Identifier, "101", "5K Run", fiveK.Identifier)]);
        }

        [AvaloniaFact]
        public async Task Removed_people_are_listed_and_tinted_with_the_reason()
        {
            Runner("1", "Ann");
            Runner("2", "Bo", removed: "removed_cleared");
            Runner("3", "Cy", removed: "reactivated_transferred");
            Runner("4", "Di", removed: RegistrationState.ACTIVE);

            ParticipantsPage page = await OpenPage();

            Assert.Equal(["Ann", "Bo", "Cy", "Di"], Listed(page).Select(p => p.FirstName).Order());
            DataGridRow bo = RowFor(page, "Bo");
            Assert.True(bo.Classes.Contains("removed"));
            Assert.Equal("Removed in Runsignup: Cleared", ToolTip.GetTip(bo));
            foreach (string inRace in new[] { "Ann", "Cy", "Di" })
            {
                Assert.False(RowFor(page, inRace).Classes.Contains("removed"));
                Assert.Null(ToolTip.GetTip(RowFor(page, inRace)));
            }
        }

        [AvaloniaFact]
        public async Task A_reactivated_person_loses_the_tint()
        {
            Participant bo = Runner("2", "Bo", removed: "removed_transferred");
            ParticipantsPage page = await OpenPage();
            Assert.True(RowFor(page, "Bo").Classes.Contains("removed"));

            bo.EventSpecific.Removed = "reactivated_transferred";
            Db.UpdateParticipant(bo);
            page.UpdateView();
            await Settle(200);

            Assert.False(RowFor(page, "Bo").Classes.Contains("removed"));
        }

        [AvaloniaFact]
        public async Task Bib_stats_count_removed_bibs_in_their_own_column()
        {
            Runner("1", "Ann");
            Runner("", "Bo");
            Runner("3", "Cy", removed: "removed_cleared");
            Runner("", "Di", removed: "removed_removed");

            ParticipantsPage page = await OpenPage();

            BibStats stats = Assert.Single(Find<DataGrid>(page, "StatsListView").ItemsSource!.Cast<BibStats>());
            Assert.Equal((1, 1, 1), (stats.With, stats.Without, stats.Removed));
            Assert.True(Find<DataGrid>(page, "StatsListView").Columns[3].IsVisible);
        }

        [AvaloniaFact]
        public async Task A_removed_entry_doesnt_conflict_with_the_person_who_has_its_bib_now()
        {
            Runner("7", "Ann", removed: "removed_transferred");
            AddParticipant(theEvent.Identifier, fiveK, "7", first: "Bo", last: "Diaz");

            ParticipantsPage page = await OpenPage();

            Assert.False(Find<Button>(page, "ConflictsBtn").IsVisible);
        }

        [AvaloniaFact]
        public async Task The_runsignup_panel_shows_only_when_linked()
        {
            ParticipantsPage unlinked = await OpenPage();
            Assert.False(Find<StackPanel>(unlinked, "RunsignupPanel").IsVisible);

            Link();
            ParticipantsPage linked = await OpenPage();

            Assert.True(Find<StackPanel>(linked, "RunsignupPanel").IsVisible);
        }

        [AvaloniaFact]
        public async Task Download_from_runsignup_adds_people_and_shows_a_summary()
        {
            Link();
            AllowDialogs();
            server.Responder = request => request.Path switch
            {
                "rest/race/42" => (HttpStatusCode.OK, """{"race":{"race_id":42,"events":[{"event_id":101,"name":"5K Run"}]}}"""),
                "rest/race/42/participants" => (HttpStatusCode.OK, """
                    [{"event":{"event_id":101},"participants":[
                      {"registration_id":1,"bib_num":"12","user":{"first_name":"Ann","last_name":"Lee","dob":"1990-01-02","gender":"F"}},
                      {"registration_id":2,"user":{"first_name":"Bo","last_name":"Diaz","dob":"1985-05-06","gender":"M"}}]}]
                    """),
                "rest/v2/race-store/get-race-store-purchases.json" => (HttpStatusCode.OK, """{"purchases":[],"next_url":null}"""),
                "rest/race/42/removed-participants" => (HttpStatusCode.OK, """{"event":{"event_id":101,"participants":[]}}"""),
                _ => (HttpStatusCode.NotFound, """{"error":{"error_msg":"Not found"}}"""),
            };
            ParticipantsPage page = await OpenPage();
            List<string> labels = LabelsOf(Find<Button>(page, "RunsignupDownload"));

            await page.DownloadFromRunsignup();
            await WaitUntil(() => Listed(page).Count == 2, because: "the list to refresh");
            Pump();

            Assert.Equal(["rsu-1", "rsu-2"], Db.GetParticipants(theEvent.Identifier).Select(p => p.EventSpecific.ExternalId).Order());
            TextBlock status = Find<TextBlock>(page, "RunsignupStatus");
            Assert.True(status.IsVisible);
            Assert.Equal("2 participants downloaded", status.Text);
            Assert.StartsWith("Added 2, updated 0, marked removed 0.", (string)ToolTip.GetTip(status)!);
            Assert.Empty(OpenDialogs());
            Assert.Contains("Race", labels);
            Assert.Equal("Download", ContentText(Find<Button>(page, "RunsignupDownload")));
            Assert.True(MainWindow.NotifyTimingWorkerCalls > 0);
        }

        [AvaloniaFact]
        public async Task Downloads_save_off_the_ui_thread_so_the_window_stays_usable()
        {
            Link();
            server.Responder = request => request.Path switch
            {
                "rest/race/42" => (HttpStatusCode.OK, """{"race":{"race_id":42,"events":[{"event_id":101,"name":"5K Run"}]}}"""),
                "rest/race/42/participants" => (HttpStatusCode.OK, """
                    [{"event":{"event_id":101},"participants":[{"registration_id":1,"user":{"first_name":"Ann","last_name":"Lee","dob":"1990-01-02"}}]}]
                    """),
                "rest/v2/race-store/get-race-store-purchases.json" => (HttpStatusCode.OK, """{"purchases":[]}"""),
                _ => (HttpStatusCode.OK, "[]"),
            };
            List<bool> onUiThread = [];
            Chronokeep.Database.IdbInterface watched = ThreadWatchingDatabase.Wrap(Db, nameof(Chronokeep.Database.IdbInterface.AddParticipants),
                () => onUiThread.Add(Avalonia.Threading.Dispatcher.UIThread.CheckAccess()));
            ParticipantsPage page = new(MainWindow, watched);
            Host(page);

            await page.DownloadFromRunsignup();

            Assert.Equal([false], onUiThread);
            Assert.Single(Db.GetParticipants(theEvent.Identifier));
        }

        [AvaloniaFact]
        public async Task A_failed_runsignup_download_says_why_and_changes_nothing()
        {
            Link();
            AllowDialogs();
            server.ResponseBody = """{"error":{"error_code":6,"error_msg":"Invalid API key."}}""";
            ParticipantsPage page = await OpenPage();

            await page.DownloadFromRunsignup();

            await WaitForDialog("Invalid API key.");
            Assert.Empty(Db.GetParticipants(theEvent.Identifier));
            Assert.Equal("Download", ContentText(Find<Button>(page, "RunsignupDownload")));
        }
    }

    /// <summary>
    /// The participant window's removed switch, beside the distance box: only for people Runsignup
    /// removed, labelled with the reason, switching between removed and reactivated.
    /// </summary>
    public class ParticipantEditorRegistrationTests : UiTestBase
    {
        private readonly Event theEvent;
        private readonly Distance fiveK;

        public ParticipantEditorRegistrationTests()
        {
            theEvent = AddEvent(makeCurrent: true);
            fiveK = AddDistance(theEvent.Identifier, "5K");
        }

        private Participant Person(string state, string externalId = "rsu-1")
        {
            Participant p = AddParticipant(theEvent.Identifier, fiveK, "7", first: "Grace", last: "Hopper", birthday: "1990/01/02");
            p.EventSpecific.ExternalId = externalId;
            p.EventSpecific.Removed = state;
            Db.UpdateParticipant(p);
            return Db.GetParticipantEventSpecific(theEvent.Identifier, p.EventSpecific.Identifier)!;
        }

        private ModifyParticipantWindow Open(Participant? person) => Show(ModifyParticipantWindow.NewWindow(MainWindow, Db, person));

        private Participant Saved() => Assert.Single(Db.GetParticipants(theEvent.Identifier));

        [AvaloniaTheory]
        [InlineData("removed_transferred", "Transferred", true)]
        [InlineData("removed_deferred", "Deferred", true)]
        [InlineData("removed_cleared", "Cleared", true)]
        [InlineData("removed_removed", "Removed", true)]
        [InlineData("removed_unknown", "Removed", true)]
        [InlineData("removed_bib_changed", "Bib Changed", true)]
        [InlineData("reactivated_transferred", "Transferred", false)]
        public void Shows_the_reason_and_whether_they_are_removed(string state, string label, bool removed)
        {
            ModifyParticipantWindow window = Open(Person(state));

            Assert.True(Find<StackPanel>(window, "RemovedPanel").IsVisible);
            Assert.Equal(label, Find<TextBlock>(window, "RemovedLabel").Text);
            Assert.Equal(removed, Find<ToggleSwitch>(window, "RemovedSwitch").IsChecked);
        }

        [AvaloniaTheory]
        [InlineData("")]
        [InlineData("active")]
        public void Nothing_shows_for_people_in_the_race(string state)
        {
            ModifyParticipantWindow window = Open(Person(state));

            Assert.False(Find<StackPanel>(window, "RemovedPanel").IsVisible);
        }

        [AvaloniaFact]
        public void The_row_looks_as_it_did_before_when_nothing_shows()
        {
            ModifyParticipantWindow window = Open(Person("active"));
            Pump();

            // The hidden switch takes no room: the distance box still runs to the row's right margin.
            ComboBox distance = Find<ComboBox>(window, "DistanceBox");
            Assert.Equal(((Control)distance.Parent!).Bounds.Width - 15, distance.Bounds.Right, 1);
        }

        [AvaloniaFact]
        public void Switching_off_reactivates_them_and_keeps_the_reason_without_counting_as_an_edit()
        {
            Participant grace = Person("removed_transferred");
            int version = grace.EventSpecific.Version;
            ModifyParticipantWindow window = Open(grace);

            Find<ToggleSwitch>(window, "RemovedSwitch").IsChecked = false;
            Assert.Contains("Switch on to remove them again", (string)ToolTip.GetTip(Find<StackPanel>(window, "RemovedPanel"))!);
            Click(Find<Button>(window, "Add"));

            Assert.Equal("reactivated_transferred", Saved().EventSpecific.Removed);
            Assert.False(Saved().EventSpecific.IsRemoved);
            Assert.Equal(version, Saved().EventSpecific.Version);
            Assert.Equal(1, MainWindow.NotifyTimingWorkerCalls);
        }

        [AvaloniaFact]
        public void Switching_back_on_removes_them_again_for_the_same_reason()
        {
            ModifyParticipantWindow window = Open(Person("reactivated_deferred"));

            Find<ToggleSwitch>(window, "RemovedSwitch").IsChecked = true;
            Click(Find<Button>(window, "Add"));

            Assert.Equal("removed_deferred", Saved().EventSpecific.Removed);
        }

        [AvaloniaFact]
        public void Saving_without_touching_it_keeps_the_state()
        {
            ModifyParticipantWindow window = Open(Person("reactivated_cleared"));

            Click(Find<Button>(window, "Add"));

            Assert.Equal("reactivated_cleared", Saved().EventSpecific.Removed);
        }

        [AvaloniaFact]
        public void Saving_someone_in_the_race_leaves_their_state_alone()
        {
            ModifyParticipantWindow window = Open(Person("active"));

            Click(Find<Button>(window, "Add"));

            Assert.Equal("active", Saved().EventSpecific.Removed);
        }
    }

    /// <summary>A database that runs a check each time one method is called, then carries on as normal.</summary>
    public class ThreadWatchingDatabase : System.Reflection.DispatchProxy
    {
        private Chronokeep.Database.IdbInterface inner = null!;
        private string watched = "";
        private Action check = () => { };

        internal static Chronokeep.Database.IdbInterface Wrap(Chronokeep.Database.IdbInterface inner, string method, Action check)
        {
            Chronokeep.Database.IdbInterface proxy = Create<Chronokeep.Database.IdbInterface, ThreadWatchingDatabase>();
            ThreadWatchingDatabase self = (ThreadWatchingDatabase)(object)proxy;
            self.inner = inner;
            self.watched = method;
            self.check = check;
            return proxy;
        }

        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name == watched) check();
            try
            {
                return targetMethod.Invoke(inner, args);
            }
            catch (System.Reflection.TargetInvocationException e) when (e.InnerException != null)
            {
                throw e.InnerException;
            }
        }
    }
}
