using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Objects;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.Participants;
using Chronokeep.UI.Parts;
using Chronokeep.UI.Timing.Windows;

namespace Chronokeep.Test.UI
{
    /// <summary>
    /// The Wave, Manual Entry, Change Multiple Participants, and Participant Conflicts windows.
    /// </summary>
    public class TimingWindowsTests : UiTestBase
    {
        /// <summary>An event that started at 8:00:00.000 with a 5K in wave 1 and a 10K in wave 2.</summary>
        private (Event Event, Distance FiveK, Distance TenK) SeedRace(long startSeconds = 8 * 3600, int startMilliseconds = 0)
        {
            Event e = AddEvent(makeCurrent: true);
            e.StartSeconds = startSeconds;
            e.StartMilliseconds = startMilliseconds;
            Db.UpdateEvent(e);
            Distance fiveK = AddDistance(e.Identifier, "5K");
            Distance tenK = AddDistance(e.Identifier, "10K");
            fiveK.Wave = 1;
            tenK.Wave = 2;
            Db.UpdateDistance(fiveK);
            Db.UpdateDistance(tenK);
            Db.SetWaveTimes(e.Identifier, 2, 300, 0);
            return (Db.GetCurrentEvent()!, Db.GetDistance(fiveK.Identifier)!, Db.GetDistance(tenK.Identifier)!);
        }

        private WaveWindow OpenWaves() => Show(new WaveWindow(MainWindow, Db));

        private static WavePart WaveRow(WaveWindow window, int wave) =>
            Find<ListBox>(window, "WaveList").Items.OfType<WavePart>().Single(w => w.GetWave() == wave);

        private static string WaveText(WaveWindow window, int wave) => Find<MaskedTextBox>(WaveRow(window, wave), "StartOffset").Text!;

        private static void SetWaveText(WaveWindow window, int wave, string text) =>
            Find<MaskedTextBox>(WaveRow(window, wave), "StartOffset").Text = text;

        private (long, int) Offset(Distance d)
        {
            Distance saved = Db.GetDistance(d.Identifier)!;
            return (saved.StartOffsetSeconds, saved.StartOffsetMilliseconds);
        }

        [AvaloniaFact]
        public void Waves_show_their_offsets_from_the_race_start()
        {
            SeedRace();

            WaveWindow window = OpenWaves();

            Assert.Equal("00:00:00.000", WaveText(window, 1));
            Assert.Equal("00:05:00.000", WaveText(window, 2));
        }

        [AvaloniaFact]
        public void Setting_a_new_offset_saves_it_and_recalculates()
        {
            (_, _, Distance tenK) = SeedRace();
            WaveWindow window = OpenWaves();

            SetWaveText(window, 2, "00:07:30.250");
            Click(Find<Button>(window, "SetButton"));

            Assert.Equal((450L, 250), Offset(tenK));
            Assert.Equal(1, MainWindow.UpdateTimingCalls);
            Assert.False(window.IsVisible);
        }

        [AvaloniaFact]
        public void Setting_without_changes_does_not_recalculate()
        {
            SeedRace();
            WaveWindow window = OpenWaves();

            Click(Find<Button>(window, "SetButton"));

            Assert.Equal(0, MainWindow.UpdateTimingCalls);
        }

        [AvaloniaFact]
        public void A_minus_wave_starts_before_the_race_start()
        {
            (_, _, Distance tenK) = SeedRace();
            WaveWindow window = OpenWaves();

            Click(Find<Button>(WaveRow(window, 2), "WavePlus"));
            Click(Find<Button>(window, "SetButton"));

            Assert.Equal((-300L, 0), Offset(tenK));
        }

        [AvaloniaFact]
        public void Time_of_day_shows_and_saves_clock_times()
        {
            (_, _, Distance tenK) = SeedRace();
            WaveWindow window = OpenWaves();

            Find<RadioButton>(window, "TimeOfDayButton").IsChecked = true;
            Pump();
            Assert.Equal("08:05:00.000", WaveText(window, 2));

            SetWaveText(window, 2, "08:10:00.500");
            Click(Find<Button>(window, "SetButton"));

            Assert.Equal((600L, 500), Offset(tenK));
        }

        [AvaloniaFact]
        public void Time_of_day_carries_milliseconds_into_seconds()
        {
            // The race started at 7:59:59.500; a wave at 8:00:00.200 starts 0.7 seconds later.
            (_, _, Distance tenK) = SeedRace(startSeconds: (7 * 3600) + (59 * 60) + 59, startMilliseconds: 500);
            WaveWindow window = OpenWaves();
            Find<RadioButton>(window, "TimeOfDayButton").IsChecked = true;
            Pump();

            SetWaveText(window, 2, "08:00:00.200");
            Click(Find<Button>(window, "SetButton"));

            Assert.Equal((0L, 700), Offset(tenK));
        }

        [AvaloniaFact]
        public void Time_of_day_shows_a_wave_half_a_second_before_the_start()
        {
            (Event e, _, _) = SeedRace(startMilliseconds: 200);
            Db.SetWaveTimes(e.Identifier, 2, 0, -500);
            WaveWindow window = OpenWaves();

            Find<RadioButton>(window, "TimeOfDayButton").IsChecked = true;
            Pump();

            Assert.Equal("07:59:59.700", WaveText(window, 2));
        }

        [AvaloniaFact]
        public void Done_closes_without_saving()
        {
            (_, _, Distance tenK) = SeedRace();
            WaveWindow window = OpenWaves();
            SetWaveText(window, 2, "00:09:00.000");

            Click(Find<Button>(window, "DoneButton"));

            Assert.Equal((300L, 0), Offset(tenK));
            Assert.False(window.IsVisible);
        }

        private ManualEntryWindow OpenManualEntry(Event e, bool dnf = false)
        {
            List<TimingLocation> locations = [
                new TimingLocation(Constants.Timing.LOCATION_FINISH, e.Identifier, "Finish", 1, 0),
                .. Db.GetTimingLocations(e.Identifier)];
            return Show(dnf ? ManualEntryWindow.NewWindow(MainWindow, Db) : ManualEntryWindow.NewWindow(MainWindow, Db, locations));
        }

        private static void Enter(ManualEntryWindow window, string bib, string time, string mode = "NetTimeButton")
        {
            Find<TextBox>(window, "BibBox").Text = bib;
            Find<MaskedTextBox>(window, "TimeBox").Text = time;
            Find<RadioButton>(window, mode).IsChecked = true;
            Pump();
            Click(Find<Button>(window, "Add"));
        }

        private DateTime RaceDay(Event e) => DateTime.Parse(e.Date).Date;

        [AvaloniaFact]
        public void Net_time_adds_the_runners_wave_offset_to_the_race_start()
        {
            (Event e, _, Distance tenK) = SeedRace();
            AddParticipant(e.Identifier, tenK, "200");
            ManualEntryWindow window = OpenManualEntry(e);

            Enter(window, "200", "00:45:00.000");

            ChipRead read = Assert.Single(Db.GetChipReads(e.Identifier));
            Assert.Equal("200", read.Bib);
            Assert.Equal(RaceDay(e).AddHours(8).AddMinutes(5 + 45), read.Time);
            Assert.Equal(Constants.Timing.LOCATION_FINISH, read.LocationId);
            Assert.Equal("", Find<TextBox>(window, "BibBox").Text);
        }

        [AvaloniaFact]
        public void Clock_time_ignores_the_wave_offset()
        {
            (Event e, _, Distance tenK) = SeedRace();
            AddParticipant(e.Identifier, tenK, "200");
            ManualEntryWindow window = OpenManualEntry(e);

            Enter(window, "200", "00:45:00.000", "ClockTimeButton");

            Assert.Equal(RaceDay(e).AddHours(8).AddMinutes(45), Assert.Single(Db.GetChipReads(e.Identifier)).Time);
        }

        [AvaloniaFact]
        public void Time_of_day_uses_the_time_as_entered()
        {
            (Event e, _, _) = SeedRace();
            ManualEntryWindow window = OpenManualEntry(e);

            Enter(window, "7", "09:12:34.500", "TimeOfDayButton");

            Assert.Equal(RaceDay(e).AddHours(9).AddMinutes(12).AddSeconds(34).AddMilliseconds(500),
                Assert.Single(Db.GetChipReads(e.Identifier)).Time);
        }

        [AvaloniaFact]
        public void A_blank_time_or_blank_bib_adds_nothing()
        {
            (Event e, _, _) = SeedRace();
            ManualEntryWindow window = OpenManualEntry(e);

            Enter(window, "7", "00:00:00.000");
            Enter(window, "   ", "00:30:00.000");

            Assert.Empty(Db.GetChipReads(e.Identifier));
        }

        [AvaloniaFact]
        public void Entries_go_to_the_chosen_location()
        {
            (Event e, _, _) = SeedRace();
            int mile = Db.AddTimingLocation(new TimingLocation(e.Identifier, "Mile 6"));
            ManualEntryWindow window = OpenManualEntry(e);
            ComboBox locations = Find<ComboBox>(window, "LocationBox");
            locations.SelectedItem = locations.Items.OfType<ComboBoxItem>().Single(i => (string)i.Content! == "Mile 6");

            Enter(window, "7", "00:20:00.000");

            Assert.Equal(mile, Assert.Single(Db.GetChipReads(e.Identifier)).LocationId);
        }

        [AvaloniaFact]
        public void Closing_after_adding_recalculates_and_closing_without_adding_does_not()
        {
            (Event e, _, _) = SeedRace();
            ManualEntryWindow untouched = OpenManualEntry(e);
            untouched.Close();
            Pump();
            Assert.Equal(0, MainWindow.NotifyTimingWorkerCalls);

            ManualEntryWindow window = OpenManualEntry(e);
            Enter(window, "7", "00:20:00.000");
            window.Close();
            Pump();

            Assert.Equal(1, MainWindow.NotifyTimingWorkerCalls);
        }

        [AvaloniaFact]
        public void Dnf_entries_need_a_bib_and_are_marked_dnf()
        {
            (Event e, _, _) = SeedRace();
            ManualEntryWindow window = OpenManualEntry(e, dnf: true);

            Enter(window, "", "00:00:00.000");
            Assert.Empty(Db.GetChipReads(e.Identifier));

            Enter(window, "9", "00:00:00.000");
            ChipRead dnf = Assert.Single(Db.GetChipReads(e.Identifier));
            Assert.Equal(Constants.Timing.CHIPREAD_STATUS_DNF, dnf.Status);
            Assert.Equal("9", dnf.Bib);
        }

        [AvaloniaFact]
        public void Change_multiple_moves_everyone_to_the_chosen_distance()
        {
            (Event e, Distance fiveK, Distance tenK) = SeedRace();
            AddParticipant(e.Identifier, fiveK, "1", first: "Ann");
            AddParticipant(e.Identifier, tenK, "2", first: "Ben");
            List<Participant> everyone = Db.GetParticipants(e.Identifier);
            Dictionary<string, int> versions = everyone.ToDictionary(p => p.FirstName, p => p.EventSpecific.Version);
            ChangeMultiParticipantWindow window = Show(new ChangeMultiParticipantWindow(MainWindow, Db, everyone));
            ComboBox distances = Find<ComboBox>(window, "DistanceBox");
            distances.SelectedItem = distances.Items.OfType<ComboBoxItem>().Single(i => (string)i.Content! == "10K");

            Click(Find<Button>(window, "Change"));

            Dictionary<string, Participant> saved = Db.GetParticipants(e.Identifier).ToDictionary(p => p.FirstName);
            Assert.All(saved.Values, p => Assert.Equal("10K", p.Distance));
            Assert.Equal(versions["Ann"] + 1, saved["Ann"].EventSpecific.Version);
            Assert.Equal(versions["Ben"], saved["Ben"].EventSpecific.Version);
            Assert.Equal(1, MainWindow.NotifyTimingWorkerCalls);
            Assert.False(window.IsVisible);
        }

        [AvaloniaFact]
        public void Change_multiple_cancel_changes_nothing()
        {
            (Event e, Distance fiveK, _) = SeedRace();
            AddParticipant(e.Identifier, fiveK, "1");
            ChangeMultiParticipantWindow window = Show(new ChangeMultiParticipantWindow(MainWindow, Db, Db.GetParticipants(e.Identifier)));
            Find<ComboBox>(window, "DistanceBox").SelectedIndex = 1;

            Click(Find<Button>(window, "Cancel"));

            Assert.Equal("5K", Assert.Single(Db.GetParticipants(e.Identifier)).Distance);
            Assert.Equal(0, MainWindow.NotifyTimingWorkerCalls);
        }

        [AvaloniaFact]
        public void Conflicts_window_lists_the_conflicting_participants()
        {
            (Event e, Distance fiveK, _) = SeedRace();
            AddParticipant(e.Identifier, fiveK, "1", first: "Ann");
            AddParticipant(e.Identifier, fiveK, "1", first: "Annie");
            List<Participant> conflicts = Db.GetParticipants(e.Identifier);

            ParticipantConflicts window = Show(ParticipantConflicts.NewWindow(MainWindow, conflicts));

            Assert.Equal(["Ann", "Annie"], Find<DataGrid>(window, "ParticipantsList").ItemsSource!.Cast<Participant>().Select(p => p.FirstName).Order());
        }
    }
}
