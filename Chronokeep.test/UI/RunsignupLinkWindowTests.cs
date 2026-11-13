using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Network.Runsignup;
using Chronokeep.Objects;
using Chronokeep.Objects.Runsignup;
using Chronokeep.Test.Network;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.Runsignup;
using System.Net;

namespace Chronokeep.Test.UI
{
    /// <summary>Dashboard → Link to Runsignup: account, race, distance map and comment fields.</summary>
    public class RunsignupLinkWindowTests : UiTestBase
    {
        private readonly FakeApiServer server = new();
        private readonly string savedBase = RunsignupHandlers.BaseUrl;
        private const string RaceJson = """
            {"race":{"race_id":42,"name":"Spring Fling","next_date":"06/15/2026","events":[
              {"event_id":101,"name":"5K Run","distance":"5K"},
              {"event_id":102,"name":"10K Run","distance":"10K"},
              {"event_id":103,"name":"Kids Mile","distance":"1M"}]}}
            """;

        public RunsignupLinkWindowTests()
        {
            RunsignupHandlers.BaseUrl = server.Url;
            server.Responder = request => request.Path switch
            {
                "rest/races" => (HttpStatusCode.OK, """{"races":[""" + RaceJson + """,{"race":{"race_id":7,"name":"Turkey Trot","events":[]}}]}"""),
                "rest/race/42" => (HttpStatusCode.OK, RaceJson),
                "rest/race/42/participants" => (HttpStatusCode.OK, """
                    [{"event":{"event_id":101},"participants":[
                      {"registration_id":1,"team_name":"Hill Hoppers","user":{"first_name":"Ann","last_name":"Lee"},
                       "question_responses":[{"question_id":77,"question_text":"Hometown","response":"Eugene"}]}]}]
                    """),
                _ => (HttpStatusCode.NotFound, """{"error":{"error_msg":"Not found"}}"""),
            };
        }

        public override void Dispose()
        {
            RunsignupHandlers.BaseUrl = savedBase;
            server.Dispose();
            base.Dispose();
        }

        private (Event Event, Distance FiveK) Seed(bool withApi = true, string name = "Test Race")
        {
            (Event e, Distance d) = SeedEvent(name);
            if (withApi)
            {
                Db.AddApi(new ApiObject(0, Constants.ApiConstants.RUNSIGNUP, "s3cret", "Timer account", "key123", ""));
            }
            return (e, d);
        }

        private RunsignupLinkWindow Open() => Show(RunsignupLinkWindow.NewWindow(MainWindow, Db)!);

        private static async Task Next(RunsignupLinkWindow window)
        {
            await window.Next();
            await window.Loading;
            Pump();
        }

        private static ComboBox ChoiceFor(RunsignupLinkWindow window, string eventName) =>
            Find<StackPanel>(window, "DistanceRows").Children.OfType<Grid>()
                .Single(row => row.Children.OfType<TextBlock>().Single().Text!.StartsWith(eventName))
                .Children.OfType<ComboBox>().Single();

        private static void Choose(ComboBox box, string option) =>
            box.SelectedItem = box.Items.OfType<ComboBoxItem>().Single(i => (string)i.Content! == option);

        private static string Chosen(ComboBox box) => (string)((ComboBoxItem)box.SelectedItem!).Content!;

        private static CheckBox Field(RunsignupLinkWindow window, string name) =>
            Find<StackPanel>(window, "CommentFieldList").Children.OfType<CheckBox>().Single(c => (string)c.Content! == name);

        [AvaloniaFact]
        public void Without_a_runsignup_api_it_says_to_add_one()
        {
            Seed(withApi: false);
            Db.AddApi(new ApiObject(0, Constants.ApiConstants.CHRONOKEEP_RESULTS, Constants.ApiConstants.API_URL[Constants.ApiConstants.CHRONOKEEP_RESULTS], "Results", "tok", ""));

            RunsignupLinkWindow window = Open();

            Assert.True(Find<TextBlock>(window, "NoApiText").IsVisible);
            Assert.False(Find<Button>(window, "NextButton").IsEnabled);
        }

        [AvaloniaFact]
        public async Task Linking_maps_distances_and_comment_fields()
        {
            (Event theEvent, Distance fiveK) = Seed();
            RunsignupLinkWindow window = Open();
            Assert.Equal("Timer account", ((ComboBoxItem)Find<ComboBox>(window, "ApiList").SelectedItem!).Content);

            await Next(window);
            ListBox raceList = Find<ListBox>(window, "RaceList");
            Assert.Equal(["Spring Fling (06/15/2026)", "Turkey Trot"], raceList.ItemsSource!.Cast<RsuRace>().Select(r => r.ToString()));
            raceList.SelectedItem = raceList.ItemsSource!.Cast<RsuRace>().First();
            Assert.Equal("42", Find<TextBox>(window, "RaceIdBox").Text);

            await Next(window);
            Assert.Equal("5K", Chosen(ChoiceFor(window, "5K Run")));
            Assert.Equal("Create new", Chosen(ChoiceFor(window, "10K Run")));
            Choose(ChoiceFor(window, "Kids Mile"), "Skip");

            await Next(window);
            Assert.Equal(["Team name", "Age", "City, State", "Hometown"], Find<StackPanel>(window, "CommentFieldList").Children.OfType<CheckBox>().Select(c => c.Content));
            Field(window, "Hometown").IsChecked = true;
            Field(window, "Team name").IsChecked = true;
            Assert.Equal("Ann Lee: Eugene | Hill Hoppers", Find<TextBlock>(window, "CommentPreview").Text);

            await Next(window);

            Event saved = Db.GetEvent(theEvent.Identifier)!;
            Assert.Equal(("42", "q:77,team_name"), (saved.RegistrationRaceId, saved.RegistrationCommentFields));
            Assert.True(saved.IsRegistrationLinked);
            Distance tenK = Db.GetDistances(theEvent.Identifier).Single(d => d.Name == "10K Run");
            Assert.Equal(
                [("101", fiveK.Identifier), ("102", tenK.Identifier), ("103", RegistrationDistance.SkipDistance)],
                Db.GetRegistrationDistances(theEvent.Identifier).OrderBy(m => m.RsuEventId).Select(m => (m.RsuEventId, m.DistanceId)));
            Assert.False(window.IsVisible);
        }

        [AvaloniaFact]
        public async Task Only_the_events_nearest_the_event_date_are_mapped()
        {
            Seed();
            server.Responder = request => request.Path switch
            {
                "rest/races" => (HttpStatusCode.OK, """{"races":[]}"""),
                "rest/race/42" => (HttpStatusCode.OK, """
                    {"race":{"race_id":42,"name":"Spring Fling","events":[
                      {"event_id":1,"name":"5K","start_time":"6/14/2025 8:00"},
                      {"event_id":2,"name":"5K","start_time":"6/13/2026 8:00"},
                      {"event_id":3,"name":"Half","start_time":"6/14/2026 7:00"},
                      {"event_id":4,"name":"5K","start_time":"6/12/2027 8:00"}]}}
                    """),
                _ => (HttpStatusCode.OK, "[]"),
            };
            RunsignupLinkWindow window = Open();
            await Next(window);
            Find<TextBox>(window, "RaceIdBox").Text = "42";

            await Next(window);

            Assert.Equal(["2", "3"], window.DistanceChoices().Select(c => c.Event.EventId));
            TextBlock note = Find<TextBlock>(window, "DistanceNote");
            Assert.True(note.IsVisible);
            Assert.Contains("2 from other years", note.Text);
        }

        [AvaloniaFact]
        public async Task The_race_matching_the_events_name_and_date_is_chosen_for_you()
        {
            Seed(name: "Spring Fling");
            RunsignupLinkWindow window = Open();

            await Next(window);

            Assert.Equal("Spring Fling (06/15/2026)", Find<ListBox>(window, "RaceList").SelectedItem!.ToString());
            Assert.Equal("42", Find<TextBox>(window, "RaceIdBox").Text);
            Assert.Contains("looks like a match", Find<TextBlock>(window, "RaceStatus").Text);
            Assert.True(Find<StackPanel>(window, "RaceStep").IsVisible);
            Assert.Contains(server.Requests, r => r.Path == "rest/races" && r.Query.ContainsKey("start_date"));
        }

        [AvaloniaFact]
        public async Task Nothing_is_chosen_when_no_race_is_close()
        {
            Seed(name: "Harvest Half");
            RunsignupLinkWindow window = Open();

            await Next(window);

            Assert.Null(Find<ListBox>(window, "RaceList").SelectedItem);
            Assert.Equal("", Find<TextBox>(window, "RaceIdBox").Text ?? "");
            Assert.DoesNotContain("match", Find<TextBlock>(window, "RaceStatus").Text);
        }

        [AvaloniaFact]
        public async Task A_race_id_can_be_typed_when_the_race_isnt_listed()
        {
            Seed();
            server.Responder = request => request.Path switch
            {
                "rest/races" => (HttpStatusCode.OK, """{"races":[]}"""),
                "rest/race/42" => (HttpStatusCode.OK, RaceJson),
                _ => (HttpStatusCode.OK, "[]"),
            };
            RunsignupLinkWindow window = Open();
            await Next(window);
            Assert.Contains("No races", Find<TextBlock>(window, "RaceStatus").Text);

            Find<TextBox>(window, "RaceIdBox").Text = "42";
            await Next(window);

            Assert.True(Find<StackPanel>(window, "DistanceStep").IsVisible);
            Assert.Equal(3, Find<StackPanel>(window, "DistanceRows").Children.Count);
        }

        [AvaloniaFact]
        public async Task Reopening_keeps_the_saved_choices_and_flags_new_events()
        {
            (Event theEvent, Distance fiveK) = Seed();
            theEvent.RegistrationApiId = Db.GetAllApi().Single().Identifier;
            theEvent.RegistrationRaceId = "42";
            theEvent.RegistrationCommentFields = "team_name";
            Db.UpdateEvent(theEvent);
            Db.SetRegistrationDistances(theEvent.Identifier,
            [
                new(theEvent.Identifier, "101", "5K Run", RegistrationDistance.SkipDistance),
                new(theEvent.Identifier, "102", "10K Run", fiveK.Identifier),
            ]);

            RunsignupLinkWindow window = Open();
            Assert.True(Find<Button>(window, "UnlinkButton").IsVisible);
            await Next(window);
            Assert.Equal("42", Find<TextBox>(window, "RaceIdBox").Text);
            await Next(window);

            Assert.Equal("Skip", Chosen(ChoiceFor(window, "5K Run")));
            Assert.Equal("5K", Chosen(ChoiceFor(window, "10K Run")));
            Assert.Equal("Kids Mile (new)", Find<StackPanel>(window, "DistanceRows").Children.OfType<Grid>().Last().Children.OfType<TextBlock>().Single().Text);
            await Next(window);
            Assert.True(Field(window, "Team name").IsChecked);
        }

        [AvaloniaFact]
        public void Unlink_removes_the_link_and_map_but_keeps_participants()
        {
            (Event theEvent, Distance fiveK) = Seed();
            theEvent.RegistrationApiId = Db.GetAllApi().Single().Identifier;
            theEvent.RegistrationRaceId = "42";
            Db.UpdateEvent(theEvent);
            Db.SetRegistrationDistances(theEvent.Identifier, [new(theEvent.Identifier, "101", "5K Run", fiveK.Identifier)]);
            int people = Db.GetParticipants(theEvent.Identifier).Count;

            Open().Unlink();

            Event saved = Db.GetEvent(theEvent.Identifier)!;
            Assert.False(saved.IsRegistrationLinked);
            Assert.Empty(Db.GetRegistrationDistances(theEvent.Identifier));
            Assert.Equal(people, Db.GetParticipants(theEvent.Identifier).Count);
        }

        [AvaloniaFact]
        public void The_dashboard_button_says_whether_the_event_is_linked()
        {
            (Event theEvent, _) = Seed();
            DashboardPage page = new(MainWindow, Db);
            Host(page);
            Assert.Equal("Link to Runsignup", ContentText(Find<Button>(page, "RunsignupLinkButton")));

            theEvent.RegistrationApiId = Db.GetAllApi().Single().Identifier;
            theEvent.RegistrationRaceId = "42";
            Db.UpdateEvent(theEvent);
            DashboardPage linked = new(MainWindow, Db);
            Host(linked);

            Assert.Equal("Edit Runsignup Link", ContentText(Find<Button>(linked, "RunsignupLinkButton")));
        }
    }
}
