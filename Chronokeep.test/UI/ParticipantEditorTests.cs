using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Objects;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.Participants;

namespace Chronokeep.Test.UI
{
    /// <summary>
    /// The add/edit participant window: what it fills in, what it saves, and the birthdate rules.
    /// </summary>
    public class ParticipantEditorTests : UiTestBase
    {
        private (Event Event, Distance FiveK, Distance TenK) Seed()
        {
            Event theEvent = AddEvent(makeCurrent: true);
            Distance fiveK = AddDistance(theEvent.Identifier, "5K");
            Distance tenK = AddDistance(theEvent.Identifier, "10K");
            return (Db.GetCurrentEvent()!, fiveK, tenK);
        }

        private ModifyParticipantWindow Open(Participant? person = null)
            => Show(ModifyParticipantWindow.NewWindow(MainWindow, Db, person));

        private static void SelectDistance(Window window, Distance distance)
        {
            ComboBox box = Find<ComboBox>(window, "DistanceBox");
            box.SelectedItem = box.Items.OfType<ComboBoxItem>().Single(i => (string)i.Tag! == distance.Identifier.ToString());
        }

        private static void SelectGender(Window window, string gender)
        {
            ComboBox box = Find<ComboBox>(window, "GenderBox");
            box.SelectedItem = box.Items.OfType<ComboBoxItem>().Single(i => (string)i.Content! == gender);
            Pump();
        }

        [AvaloniaFact]
        public void Adding_saves_the_person_with_a_year_month_day_birthdate_and_clears_the_form()
        {
            (Event e, _, Distance tenK) = Seed();
            ModifyParticipantWindow window = Open();
            Find<TextBox>(window, "BibBox").Text = "42";
            Find<TextBox>(window, "FirstBox").Text = "Ada";
            Find<TextBox>(window, "LastBox").Text = "Lovelace";
            Find<CalendarDatePicker>(window, "BirthdayBox").SelectedDate = new DateTime(1990, 1, 2);
            SelectDistance(window, tenK);
            Find<TextBox>(window, "CommentsBox").Text = "Running for charity";
            Find<TextBox>(window, "ApparelBox").Text = "Race Tee (M)";

            Click(Find<Button>(window, "Add"));

            Participant saved = Assert.Single(Db.GetParticipants(e.Identifier));
            Assert.Equal(("42", "Ada", "Lovelace", "1990/01/02"), (saved.Bib, saved.FirstName, saved.LastName, saved.Birthdate));
            Assert.Equal("10K", saved.Distance);
            Assert.Equal("Running for charity", saved.Comments);
            Assert.Equal("Race Tee (M)", saved.EventSpecific.Apparel);
            Assert.Equal("", Find<TextBox>(window, "FirstBox").Text);
            Assert.True(window.IsVisible, "The add window stays open for the next person.");
        }

        [AvaloniaFact]
        public void Adding_with_only_an_age_sets_the_birthdate_to_january_first_of_that_year()
        {
            (Event e, _, _) = Seed();
            int eventYear = DateTime.Parse(e.Date).Year;
            ModifyParticipantWindow window = Open();
            Find<TextBox>(window, "FirstBox").Text = "Age";
            Find<TextBox>(window, "LastBox").Text = "Only";
            Find<TextBox>(window, "AgeBox").Text = "30";

            Click(Find<Button>(window, "Add"));

            Assert.Equal($"{eventYear - 30}/01/01", Assert.Single(Db.GetParticipants(e.Identifier)).Birthdate);
        }

        [AvaloniaFact]
        public void Adding_without_a_name_saves_nothing()
        {
            (Event e, _, _) = Seed();
            ModifyParticipantWindow window = Open();
            Find<TextBox>(window, "BibBox").Text = "7";

            Click(Find<Button>(window, "Add"));

            Assert.Empty(Db.GetParticipants(e.Identifier));
        }

        [AvaloniaFact]
        public void Choosing_other_gender_shows_a_box_and_saves_what_was_typed()
        {
            (Event e, _, _) = Seed();
            ModifyParticipantWindow window = Open();
            TextBox other = Find<TextBox>(window, "OtherGenderBox");
            Assert.False(other.IsVisible);

            SelectGender(window, "Other");
            Assert.True(other.IsVisible);
            other.Text = "Agender";
            Find<TextBox>(window, "FirstBox").Text = "Sam";
            Click(Find<Button>(window, "Add"));

            Assert.Equal("Agender", Assert.Single(Db.GetParticipants(e.Identifier)).Gender);
        }

        private Participant SeedPerson(Event e, Distance d, string birthday = "1990/01/02")
        {
            Participant p = AddParticipant(e.Identifier, d, "100", first: "Grace", last: "Hopper", birthday: birthday);
            return Db.GetParticipantEventSpecific(e.Identifier, p.EventSpecific.Identifier)!;
        }

        [AvaloniaFact]
        public void Editing_fills_the_form_from_the_person()
        {
            (Event e, Distance fiveK, _) = Seed();
            Participant grace = SeedPerson(e, fiveK);

            ModifyParticipantWindow window = Open(grace);

            Assert.Equal("100", Find<TextBox>(window, "BibBox").Text);
            Assert.Equal("Grace", Find<TextBox>(window, "FirstBox").Text);
            Assert.Equal(new DateTime(1990, 1, 2), Find<CalendarDatePicker>(window, "BirthdayBox").SelectedDate);
            Assert.Equal("5K", ((ComboBoxItem)Find<ComboBox>(window, "DistanceBox").SelectedItem!).Content);
            Assert.Equal("Update", ContentText(Find<Button>(window, "Add")));
            Assert.Equal("Cancel", ContentText(Find<Button>(window, "Done")));
        }

        [AvaloniaFact]
        public void Updating_saves_changes_bumps_the_version_and_closes()
        {
            (Event e, Distance fiveK, Distance tenK) = Seed();
            Participant grace = SeedPerson(e, fiveK);
            int version = grace.EventSpecific.Version;
            ModifyParticipantWindow window = Open(grace);

            Find<TextBox>(window, "LastBox").Text = "Hopper-Murray";
            SelectDistance(window, tenK);
            Click(Find<Button>(window, "Add"));

            Participant saved = Assert.Single(Db.GetParticipants(e.Identifier));
            Assert.Equal("Hopper-Murray", saved.LastName);
            Assert.Equal("10K", saved.Distance);
            Assert.Equal(version + 1, saved.EventSpecific.Version);
            Assert.False(window.IsVisible);
            Assert.Equal(1, MainWindow.NotifyTimingWorkerCalls);
        }

        [AvaloniaFact]
        public void Updating_without_changes_keeps_the_version()
        {
            (Event e, Distance fiveK, _) = Seed();
            Participant grace = SeedPerson(e, fiveK);
            int version = grace.EventSpecific.Version;

            Click(Find<Button>(Open(grace), "Add"));

            Assert.Equal(version, Assert.Single(Db.GetParticipants(e.Identifier)).EventSpecific.Version);
        }

        [AvaloniaFact]
        public void Updating_keeps_the_runsignup_id_and_removed_reason()
        {
            (Event e, Distance fiveK, _) = Seed();
            Participant grace = SeedPerson(e, fiveK);
            grace.EventSpecific.ExternalId = "rsu-9001";
            grace.EventSpecific.Removed = "transferred";
            Db.UpdateParticipant(grace);
            grace = Db.GetParticipantEventSpecific(e.Identifier, grace.EventSpecific.Identifier)!;
            ModifyParticipantWindow window = Open(grace);

            Find<TextBox>(window, "CommentsBox").Text = "Edited locally";
            Click(Find<Button>(window, "Add"));

            Participant saved = Assert.Single(Db.GetParticipants(e.Identifier));
            Assert.Equal("Edited locally", saved.Comments);
            Assert.Equal("rsu-9001", saved.EventSpecific.ExternalId);
            Assert.Equal("transferred", saved.EventSpecific.Removed);
        }

        [AvaloniaFact]
        public void Updating_keeps_check_in_payment_and_text_alert_details_the_window_does_not_show()
        {
            (Event e, Distance fiveK, _) = Seed();
            Participant checkedIn = Db.AddParticipant(new Participant(
                "Kat", "Johnson", "1 Main St", "Springfield", "OR", "97477", "1990/01/02",
                new EventSpecific(e.Identifier, fiveK.Identifier, fiveK.Name, "300", 1,
                    "", "$10", "Needs a shirt", false, true, "", ""),
                "", "", "555-0101", "", "US", "", "Woman", "", ""))!;
            checkedIn = Db.GetParticipantEventSpecific(e.Identifier, checkedIn.EventSpecific.Identifier)!;
            ModifyParticipantWindow window = Open(checkedIn);

            Find<TextBox>(window, "LastBox").Text = "Johnson-Goble";
            Click(Find<Button>(window, "Add"));

            Participant saved = Assert.Single(Db.GetParticipants(e.Identifier));
            Assert.Equal("Johnson-Goble", saved.LastName);
            Assert.Equal(1, saved.EventSpecific.CheckedIn);
            Assert.Equal("$10", saved.EventSpecific.Owes);
            Assert.Equal("Needs a shirt", saved.EventSpecific.Other);
            Assert.True(saved.EventSpecific.SmsEnabled);
        }

        [AvaloniaFact]
        public void A_person_without_a_birthdate_can_be_edited()
        {
            (Event e, Distance fiveK, _) = Seed();
            Participant noBirthday = SeedPerson(e, fiveK, birthday: "");

            ModifyParticipantWindow window = Open(noBirthday);
            Assert.Null(Find<CalendarDatePicker>(window, "BirthdayBox").SelectedDate);
            Find<TextBox>(window, "FirstBox").Text = "Gracie";
            Click(Find<Button>(window, "Add"));

            Participant saved = Assert.Single(Db.GetParticipants(e.Identifier));
            Assert.Equal("Gracie", saved.FirstName);
            Assert.Equal("", saved.Birthdate);
        }

        [AvaloniaFact]
        public void Cancel_closes_without_saving()
        {
            (Event e, Distance fiveK, _) = Seed();
            Participant grace = SeedPerson(e, fiveK);
            ModifyParticipantWindow window = Open(grace);

            Find<TextBox>(window, "FirstBox").Text = "Not saved";
            Click(Find<Button>(window, "Done"));

            Assert.Equal("Grace", Assert.Single(Db.GetParticipants(e.Identifier)).FirstName);
            Assert.False(window.IsVisible);
            Assert.Equal(0, MainWindow.NotifyTimingWorkerCalls);
        }
    }
}
