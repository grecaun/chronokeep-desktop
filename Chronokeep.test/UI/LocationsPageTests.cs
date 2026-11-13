using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Objects;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.Parts;

namespace Chronokeep.Test.UI
{
    /// <summary>
    /// The Locations page: the fixed Start and Finish rows, custom locations, and when leaving the
    /// page recalculates (and clears uploaded) results.
    /// </summary>
    public class LocationsPageTests : UiTestBase
    {
        private Event SeedEventWithLocation(string location = "Mile 6", bool commonStartFinish = false, int eventType = Constants.Timing.EVENT_TYPE_DISTANCE)
        {
            Event e = AddEvent(makeCurrent: true);
            e.CommonStartFinish = commonStartFinish;
            e.EventType = eventType;
            Db.UpdateEvent(e);
            Db.AddTimingLocation(new TimingLocation(e.Identifier, location));
            return Db.GetCurrentEvent()!;
        }

        private LocationsPage Open()
        {
            LocationsPage page = new(MainWindow, Db);
            Host(page);
            return page;
        }

        private static List<LocationPart> Rows(LocationsPage page) =>
            [.. Find<ListBox>(page, "LocationsBox").Items.OfType<LocationPart>()];

        private static LocationPart Row(LocationsPage page, string name) =>
            Rows(page).Single(r => r.MyLocation.Name == name);

        [AvaloniaFact]
        public void Start_and_finish_come_first_and_cannot_be_renamed_or_removed()
        {
            SeedEventWithLocation();

            List<LocationPart> rows = Rows(Open());

            Assert.Equal(["Start", "Finish", "Mile 6"], rows.Select(r => r.MyLocation.Name));
            foreach (LocationPart fixedRow in rows.Take(2))
            {
                Assert.False(Find<TextBox>(fixedRow, "LocationName").IsEnabled);
                Assert.False(Find<Button>(fixedRow, "Remove").IsVisible);
            }
            Assert.True(Find<TextBox>(rows[2], "LocationName").IsEnabled);
            Assert.True(Find<Button>(rows[2], "Remove").IsVisible);
        }

        [AvaloniaFact]
        public void Start_row_is_labelled_start_window()
        {
            SeedEventWithLocation();

            LocationsPage page = Open();

            Assert.Equal("Start Window", Find<TextBlock>(Row(page, "Start"), "IgnoreWithinLabel").Text);
            Assert.Equal("Ignore Within", Find<TextBlock>(Row(page, "Mile 6"), "IgnoreWithinLabel").Text);
        }

        [AvaloniaFact]
        public void Occurrences_are_hidden_for_time_based_events_and_a_shared_start()
        {
            SeedEventWithLocation(eventType: Constants.Timing.EVENT_TYPE_TIME);
            Assert.All(Rows(Open()), r => Assert.False(Find<Panel>(r, "OccPanel").IsVisible));
        }

        [AvaloniaFact]
        public void A_shared_start_and_finish_hides_occurrences_on_the_start_row_only()
        {
            SeedEventWithLocation(commonStartFinish: true);
            LocationsPage page = Open();

            Assert.False(Find<Panel>(Row(page, "Start"), "OccPanel").IsVisible);
            Assert.True(Find<Panel>(Row(page, "Finish"), "OccPanel").IsVisible);
        }

        [AvaloniaFact]
        public void Add_creates_a_numbered_location()
        {
            Event e = SeedEventWithLocation();
            LocationsPage page = Open();

            Click(Find<Button>(page, "Add"));

            Assert.Equal(2, Db.GetTimingLocations(e.Identifier).Count);
            Assert.Contains(Rows(page), r => r.MyLocation.Name.StartsWith("Location "));
        }

        [AvaloniaFact]
        public void New_locations_ignore_repeat_reads_for_ten_minutes()
        {
            Event e = SeedEventWithLocation();
            LocationsPage page = Open();

            Click(Find<Button>(page, "Add"));

            TimingLocation added = Db.GetTimingLocations(e.Identifier).Single(l => l.Name.StartsWith("Location "));
            Assert.Equal(600, added.IgnoreWithin);
            Assert.Equal(1, added.MaxOccurrences);
            Assert.Equal("00:10:00", Find<MaskedTextBox>(Row(page, added.Name), "IgnoreWithin").Text);
        }

        [AvaloniaFact]
        public void Older_locations_without_an_ignore_time_show_zero()
        {
            Event e = AddEvent(makeCurrent: true);
            Db.AddTimingLocation(new TimingLocation(-1, e.Identifier, "Old Mat", 1, -1));
            LocationsPage page = Open();

            Assert.Equal("00:00:00", Find<MaskedTextBox>(Row(page, "Old Mat"), "IgnoreWithin").Text);

            Click(Find<Button>(page, "UpdateBtn"));
            page.Closing();
            Assert.Equal(0, MainWindow.NetworkClearResultsCalls);
        }

        [AvaloniaFact]
        public void Remove_deletes_a_custom_location()
        {
            Event e = SeedEventWithLocation();
            LocationsPage page = Open();

            Click(Find<Button>(Row(page, "Mile 6"), "Remove"));

            Assert.Empty(Db.GetTimingLocations(e.Identifier));
            Assert.Equal(2, Rows(page).Count);
        }

        [AvaloniaFact]
        public void Finish_and_start_rows_save_to_the_event()
        {
            Event e = SeedEventWithLocation();
            LocationsPage page = Open();

            Find<TextBox>(Row(page, "Finish"), "MaxOccurrences").Text = "3";
            Find<MaskedTextBox>(Row(page, "Finish"), "IgnoreWithin").Text = "00:05:00";
            Find<MaskedTextBox>(Row(page, "Start"), "IgnoreWithin").Text = "00:15:00";
            Click(Find<Button>(page, "UpdateBtn"));

            Event saved = Db.GetEvent(e.Identifier)!;
            Assert.Equal(3, saved.FinishMaxOccurrences);
            Assert.Equal(300, saved.FinishIgnoreWithin);
            Assert.Equal(900, saved.StartWindow);
        }

        [AvaloniaFact]
        public void Renaming_a_location_saves_without_recalculating_results()
        {
            Event e = SeedEventWithLocation();
            LocationsPage page = Open();

            Find<TextBox>(Row(page, "Mile 6"), "LocationName").Text = "Halfway";
            Click(Find<Button>(page, "UpdateBtn"));
            page.Closing();

            Assert.Equal("Halfway", Assert.Single(Db.GetTimingLocations(e.Identifier)).Name);
            Assert.Equal(0, MainWindow.NetworkClearResultsCalls);
        }

        [AvaloniaFact]
        public void Changing_a_locations_occurrences_recalculates_results_when_leaving()
        {
            Event e = SeedEventWithLocation();
            LocationsPage page = Open();

            Find<TextBox>(Row(page, "Mile 6"), "MaxOccurrences").Text = "4";
            Find<MaskedTextBox>(Row(page, "Mile 6"), "IgnoreWithin").Text = "00:02:00";
            Click(Find<Button>(page, "UpdateBtn"));
            page.Closing();

            TimingLocation saved = Assert.Single(Db.GetTimingLocations(e.Identifier));
            Assert.Equal((4, 120), (saved.MaxOccurrences, saved.IgnoreWithin));
            Assert.Equal(1, MainWindow.NetworkClearResultsCalls);
            Assert.Equal(1, MainWindow.NotifyTimingWorkerCalls);
        }

        [AvaloniaFact]
        public void Updating_without_changes_does_not_clear_results()
        {
            SeedEventWithLocation();
            LocationsPage page = Open();

            Click(Find<Button>(page, "UpdateBtn"));
            page.Closing();

            Assert.Equal(0, MainWindow.NetworkClearResultsCalls);
            Assert.Equal(0, MainWindow.NotifyTimingWorkerCalls);
        }

        [AvaloniaFact]
        public void Reset_discards_unsaved_edits()
        {
            Event e = SeedEventWithLocation();
            LocationsPage page = Open();
            Find<TextBox>(Row(page, "Mile 6"), "LocationName").Text = "Typed but reset";

            Click(Find<Button>(page, "ResetBtn"));

            Assert.Equal("Mile 6", Find<TextBox>(Row(page, "Mile 6"), "LocationName").Text);
            Assert.Equal("Mile 6", Assert.Single(Db.GetTimingLocations(e.Identifier)).Name);
        }
    }
}
