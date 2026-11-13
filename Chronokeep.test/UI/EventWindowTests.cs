using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Objects;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.EventWindows;

namespace Chronokeep.Test.UI
{
    /// <summary>The New Event and Change Event windows.</summary>
    public class EventWindowTests : UiTestBase
    {
        private NewEventWindow OpenNewEvent() => Show(NewEventWindow.NewWindow(MainWindow, Db));

        private static void FillNewEvent(NewEventWindow window, string name, string year, DateTime date)
        {
            Find<TextBox>(window, "NameBox").Text = name;
            Find<TextBox>(window, "YearCodeBox").Text = year;
            Find<CalendarDatePicker>(window, "DatePicker").SelectedDate = date;
            Pump();
        }

        [AvaloniaFact]
        public void New_event_is_created_with_a_default_distance_and_becomes_current()
        {
            NewEventWindow window = OpenNewEvent();
            FillNewEvent(window, "Harvest Half", "2026", new DateTime(2026, 10, 4));

            Click(Find<Button>(window, "SubmitButton"));

            Event created = Assert.Single(Db.GetEvents());
            Assert.Equal(("Harvest Half", "2026"), (created.Name, created.YearCode));
            Assert.Equal(new DateTime(2026, 10, 4), DateTime.Parse(created.Date));
            Assert.Equal(created.Identifier, Db.GetCurrentEvent()!.Identifier);
            Assert.Equal("Default Distance", Assert.Single(Db.GetDistances(created.Identifier)).Name);
            Assert.False(window.IsVisible);
            Assert.True(MainWindow.WindowFinalizeCalls > 0);
        }

        [AvaloniaFact]
        public void New_event_without_a_name_is_not_created()
        {
            NewEventWindow window = OpenNewEvent();
            FillNewEvent(window, "   ", "2026", new DateTime(2026, 10, 4));

            Click(Find<Button>(window, "SubmitButton"));

            Assert.Empty(Db.GetEvents());
            Assert.True(window.IsVisible);
        }

        [AvaloniaFact]
        public void New_event_can_copy_distances_locations_and_settings_from_an_old_event()
        {
            Event old = AddEvent("Harvest Half");
            old.FinishMaxOccurrences = 3;
            old.UseMaleFemale = true;
            Db.UpdateEvent(old);
            Distance half = AddDistance(old.Identifier, "Half");
            Distance relay = AddDistance(old.Identifier, "Relay");
            relay.LinkedDistance = half.Identifier;
            Db.UpdateDistance(relay);
            Db.AddTimingLocation(new TimingLocation(old.Identifier, "Mile 6"));
            NewEventWindow window = OpenNewEvent();
            FillNewEvent(window, "Harvest Half", "2027", new DateTime(2027, 10, 3));
            Find<AutoCompleteBox>(window, "OldEvent").Text = $"{old.YearCode} {old.Name}";

            Click(Find<Button>(window, "SubmitButton"));

            Event created = Db.GetCurrentEvent()!;
            Assert.NotEqual(old.Identifier, created.Identifier);
            Assert.Equal(3, created.FinishMaxOccurrences);
            Assert.True(created.UseMaleFemale);
            Dictionary<string, Distance> copied = Db.GetDistances(created.Identifier).ToDictionary(d => d.Name);
            Assert.Equal(["Half", "Relay"], copied.Keys.Order());
            Assert.Equal(copied["Half"].Identifier, copied["Relay"].LinkedDistance);
            Assert.Contains(Db.GetTimingLocations(created.Identifier), l => l.Name == "Mile 6");
            Assert.Equal(2, Db.GetDistances(old.Identifier).Count);
        }

        [AvaloniaFact]
        public void Cancel_closes_without_creating_an_event()
        {
            NewEventWindow window = OpenNewEvent();
            FillNewEvent(window, "Never Made", "2026", new DateTime(2026, 1, 1));

            Click(Find<Button>(window, "CancelButton"));

            Assert.Empty(Db.GetEvents());
            Assert.False(window.IsVisible);
        }

        private ChangeEventWindow OpenChangeEvent() => Show(ChangeEventWindow.NewWindow(MainWindow, Db));

        private static List<Event> Listed(ChangeEventWindow window) =>
            [.. (Find<ListBox>(window, "EventList").ItemsSource ?? Array.Empty<Event>()).Cast<Event>()];

        [AvaloniaFact]
        public async Task Change_event_lists_events_newest_first_and_switches_to_the_selected_one()
        {
            Event older = AddEvent("Older Race", DateString(2025), makeCurrent: true);
            Event newer = AddEvent("Newer Race", DateString(2026));
            ChangeEventWindow window = OpenChangeEvent();
            await WaitUntil(() => Listed(window).Count == 2, because: "events to load");
            Assert.Equal(["Newer Race", "Older Race"], Listed(window).Select(ev => ev.Name));

            Find<ListBox>(window, "EventList").SelectedItem = Listed(window).Single(ev => ev.Identifier == newer.Identifier);
            Click(Find<Button>(window, "ChangeButton"));

            Assert.Equal(newer.Identifier, Db.GetCurrentEvent()!.Identifier);
            Assert.False(window.IsVisible);
            Assert.NotEqual(older.Identifier, Db.GetCurrentEvent()!.Identifier);
        }

        [AvaloniaFact]
        public async Task Search_filters_the_event_list_by_name()
        {
            AddEvent("Spring Fling", DateString(2026, 4));
            AddEvent("Fall Classic", DateString(2026, 10));
            ChangeEventWindow window = OpenChangeEvent();
            await WaitUntil(() => Listed(window).Count == 2);

            Find<TextBox>(window, "SearchBox").Text = "fall";
            await WaitUntil(() => Listed(window).Count == 1, because: "the search to apply");

            Assert.Equal("Fall Classic", Listed(window).Single().Name);
        }

        [AvaloniaFact]
        public async Task Change_button_comes_back_after_a_search_with_no_matches_is_cleared()
        {
            AddEvent("Spring Fling");
            ChangeEventWindow window = OpenChangeEvent();
            TextBox search = Find<TextBox>(window, "SearchBox");
            Button change = Find<Button>(window, "ChangeButton");
            await WaitUntil(() => Listed(window).Count == 1);

            search.Text = "no such race";
            await WaitUntil(() => Listed(window).Count == 0, because: "the empty search");
            Assert.False(change.IsEnabled);

            search.Text = "";
            await WaitUntil(() => Listed(window).Count == 1, because: "the search to clear");
            Assert.True(change.IsEnabled);
        }

        [AvaloniaFact]
        public async Task Delete_removes_the_selected_event_and_refreshes_the_list()
        {
            Event keep = AddEvent("Keep Me", DateString(2026, 4));
            Event drop = AddEvent("Drop Me", DateString(2026, 10));
            ChangeEventWindow window = OpenChangeEvent();
            await WaitUntil(() => Listed(window).Count == 2);

            Find<ListBox>(window, "EventList").SelectedItem = Listed(window).Single(ev => ev.Identifier == drop.Identifier);
            Click(Find<Button>(window, "DeleteButton"));
            await WaitUntil(() => Listed(window).Count == 1, because: "the list to refresh");

            Assert.Equal(keep.Identifier, Assert.Single(Db.GetEvents()).Identifier);
            Assert.True(window.IsVisible);
        }
    }
}
