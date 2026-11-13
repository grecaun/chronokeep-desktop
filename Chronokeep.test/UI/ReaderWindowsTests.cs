using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.Objects.RFID;
using Chronokeep.Test.Timing;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.Timing.Interfaces;
using Chronokeep.UI.Timing.ReaderSettings;
using Chronokeep.UI.Timing.Windows;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Readers = Chronokeep.Constants.Readers;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.UI
{
    /// <summary>An <see cref="ITimingPage"/> that records what the reader windows ask of it.</summary>
    internal sealed class FakeTimingPage : ITimingPage
    {
        public List<(DateTime Time, bool Now)> SetAllCalls { get; } = [];
        public List<TimingSystem> RewindOpened { get; } = [];
        public List<TimingSystem> TimeOpened { get; } = [];
        public List<TimingSystem> Connected { get; } = [];
        public List<TimingSystem> Disconnected { get; } = [];
        public List<TimingSystem> Removed { get; } = [];
        public int CloseRewindCalls { get; private set; }
        public int CloseTimeCalls { get; private set; }

        public string GetSearchValue() => "";
        public string GetLocation() => "";
        public string GetReader() => "";
        public SortType GetSortType() => SortType.SYSTIME;
        public PeopleType GetPeopleType() => PeopleType.ALL;
        public void LoadMainDisplay() { }
        public void NotifyTimingWorker() { }
        public void UpdateView() { }
        public void SetAllTimingSystemsToTime(DateTime date, bool now) => SetAllCalls.Add((date, now));
        public void OpenRewindWindow(TimingSystem reader) => RewindOpened.Add(reader);
        public void CloseRewindWindow() => CloseRewindCalls++;
        public void OpenTimeWindow(TimingSystem reader) => TimeOpened.Add(reader);
        public void CloseTimeWindow() => CloseTimeCalls++;
        public bool ConnectSystem(TimingSystem reader) { Connected.Add(reader); return true; }
        public bool DisconnectSystem(TimingSystem reader) { Disconnected.Add(reader); return true; }
        public void RemoveSystem(TimingSystem reader) => Removed.Add(reader);
        public void SetReaders(string[] readers, bool visible) { }
    }

    /// <summary>
    /// Shared setup for windows that talk to a reader: an Ultra whose commands land on a loopback
    /// socket the test reads, the same way the real reader would receive them.
    /// </summary>
    public abstract class ReaderUiTestBase : UiTestBase
    {
        internal SocketPair Pair { get; } = new();
        internal FakeTimingPage Parent { get; } = new();
        protected const char Term = (char)0xFF;

        protected ReaderUiTestBase()
        {
            AddEvent(makeCurrent: true);
        }

        /// <summary>An Ultra reader with its interface plugged into the test socket.</summary>
        internal (TimingSystem System, RfidUltraInterface Ultra) Ultra(SYSTEM_STATUS status = SYSTEM_STATUS.CONNECTED)
        {
            TimingSystem system = new("10.0.0.5", Readers.SYSTEM_RFID) { Status = status };
            RfidUltraInterface ultra = new(Db, TimingConstants.LOCATION_FINISH, MainWindow);
            ultra.SetMainSocket(Pair.Client);
            system.SystemInterface = ultra;
            return (system, ultra);
        }

        /// <summary>A Chronokeep portal with its interface plugged into the test socket.</summary>
        internal (TimingSystem System, ChronokeepInterface Portal) Portal(SYSTEM_STATUS status = SYSTEM_STATUS.CONNECTED)
        {
            TimingSystem system = new("10.0.0.6", Readers.SYSTEM_CHRONOKEEP_PORTAL) { Status = status };
            ChronokeepInterface portal = new(Db, TimingConstants.LOCATION_FINISH, MainWindow);
            // Connect() finds the portal over UDP first, so plug the socket in directly.
            typeof(ChronokeepInterface).GetField("sock", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(portal, Pair.Client);
            system.SystemInterface = portal;
            return (system, portal);
        }

        /// <summary>The next portal command, parsed.</summary>
        internal JsonElement PortalSent()
        {
            string line = Encoding.UTF8.GetString(Pair.ReadLines());
            return JsonDocument.Parse(line).RootElement;
        }

        public override void Dispose()
        {
            Pair.Dispose();
            base.Dispose();
        }
    }

    /// <summary>The Rewind window asks a reader to resend its reads for a time range.</summary>
    public class RewindWindowTests : ReaderUiTestBase
    {
        private RewindWindow Open(TimingSystem system) => Show(new RewindWindow(system, Parent));

        private static string UltraRewind(DateTime from, DateTime to) =>
            $"800{TimingConstants.RfidDateToEpoch(from)}\r{TimingConstants.RfidDateToEpoch(to)}\n";

        [AvaloniaFact]
        public void Opens_on_the_whole_of_today()
        {
            RewindWindow window = Open(Ultra().System);

            Assert.Equal(DateTime.Today, Find<CalendarDatePicker>(window, "FromDate").SelectedDate?.Date);
            Assert.Equal(DateTime.Today, Find<CalendarDatePicker>(window, "ToDate").SelectedDate?.Date);
            Assert.Equal("00:00:00", Find<MaskedTextBox>(window, "FromTime").Text);
            Assert.Equal("23:59:59", Find<MaskedTextBox>(window, "ToTime").Text);
        }

        [AvaloniaFact]
        public void Rewind_sends_the_whole_day_to_an_ultra_and_closes()
        {
            RewindWindow window = Open(Ultra().System);

            Click(Find<Button>(window, "Rewind"));

            Assert.Equal(UltraRewind(DateTime.Today, DateTime.Today.AddSeconds(86399)), Pair.Line());
            Assert.False(window.IsVisible);
            Assert.Equal(1, Parent.CloseRewindCalls);
        }

        [AvaloniaTheory]
        [InlineData("SetYesterday", -1)]
        [InlineData("SetTomorrow", 1)]
        [InlineData("SetToday", 0)]
        public void Day_buttons_rewind_that_whole_day(string button, int days)
        {
            RewindWindow window = Open(Ultra().System);
            Find<MaskedTextBox>(window, "FromTime").Text = "08:00:00";

            Click(Find<Button>(window, button));
            Click(Find<Button>(window, "Rewind"));

            DateTime day = DateTime.Today.AddDays(days);
            Assert.Equal(UltraRewind(day, day.AddSeconds(86399)), Pair.Line());
        }

        [AvaloniaFact]
        public void Rewind_uses_the_typed_dates_and_times()
        {
            RewindWindow window = Open(Ultra().System);
            Find<CalendarDatePicker>(window, "FromDate").SelectedDate = new DateTime(2026, 6, 15);
            Find<CalendarDatePicker>(window, "ToDate").SelectedDate = new DateTime(2026, 6, 16);
            Find<MaskedTextBox>(window, "FromTime").Text = "07:30:15";
            Find<MaskedTextBox>(window, "ToTime").Text = "13:05:00";

            Click(Find<Button>(window, "Rewind"));

            Assert.Equal(UltraRewind(new DateTime(2026, 6, 15, 7, 30, 15), new DateTime(2026, 6, 16, 13, 5, 0)), Pair.Line());
        }

        [AvaloniaTheory]
        [InlineData("de-DE")]
        [InlineData("fi-FI")]
        [InlineData("en-GB")]
        public void Rewind_range_is_the_same_in_any_culture(string culture)
        {
            RewindWindow window = Open(Ultra().System);
            Find<CalendarDatePicker>(window, "FromDate").SelectedDate = new DateTime(2026, 6, 5);
            Find<CalendarDatePicker>(window, "ToDate").SelectedDate = new DateTime(2026, 6, 5);
            Find<MaskedTextBox>(window, "FromTime").Text = "07:30:15";

            CultureScope.In(culture, () => Click(Find<Button>(window, "Rewind")));

            Assert.Equal(UltraRewind(new DateTime(2026, 6, 5, 7, 30, 15), new DateTime(2026, 6, 5, 23, 59, 59)), Pair.Line());
        }

        [AvaloniaFact]
        public void Partly_typed_times_fill_the_blanks_with_zeros()
        {
            RewindWindow window = Open(Ultra().System);
            Find<CalendarDatePicker>(window, "FromDate").SelectedDate = new DateTime(2026, 6, 15);
            Find<CalendarDatePicker>(window, "ToDate").SelectedDate = new DateTime(2026, 6, 15);
            Find<MaskedTextBox>(window, "FromTime").Text = "07:3_:__";

            Click(Find<Button>(window, "Rewind"));

            Assert.Equal(UltraRewind(new DateTime(2026, 6, 15, 7, 30, 0), new DateTime(2026, 6, 15, 23, 59, 59)), Pair.Line());
        }

        [AvaloniaFact]
        public void Portal_rewind_sends_unix_seconds()
        {
            RewindWindow window = Open(Portal().System);
            Find<CalendarDatePicker>(window, "FromDate").SelectedDate = new DateTime(2026, 6, 15);
            Find<CalendarDatePicker>(window, "ToDate").SelectedDate = new DateTime(2026, 6, 15);

            Click(Find<Button>(window, "Rewind"));

            JsonElement sent = PortalSent();
            Assert.Equal("reads_get", sent.GetProperty("command").GetString());
            Assert.Equal(new DateTimeOffset(new DateTime(2026, 6, 15)).ToUnixTimeSeconds(), sent.GetProperty("start_seconds").GetInt64());
            Assert.Equal(new DateTimeOffset(new DateTime(2026, 6, 15, 23, 59, 59)).ToUnixTimeSeconds(), sent.GetProperty("end_seconds").GetInt64());
        }

        [AvaloniaFact]
        public void Reader_choice_only_shows_for_ipico()
        {
            RewindWindow ultra = Open(Ultra().System);
            RewindWindow ipico = Open(new TimingSystem("10.0.0.7", Readers.SYSTEM_IPICO));

            Assert.False(Find<RadioButton>(ultra, "Reader1").IsVisible);
            Assert.False(Find<RadioButton>(ultra, "Reader2").IsVisible);
            Assert.True(Find<RadioButton>(ipico, "Reader1").IsVisible);
            Assert.True(Find<RadioButton>(ipico, "Reader2").IsVisible);
        }

        [AvaloniaFact]
        public void Done_closes_without_asking_the_reader_for_anything()
        {
            RewindWindow window = Open(Ultra().System);

            Click(Find<Button>(window, "Done"));

            Assert.False(window.IsVisible);
            Assert.True(Pair.NothingSent());
            Assert.Equal(1, Parent.CloseRewindCalls);
        }

        [AvaloniaFact]
        public void Knows_which_reader_it_belongs_to()
        {
            TimingSystem system = Ultra().System;
            RewindWindow window = Open(system);

            Assert.True(window.IsTimingSystem(system));
            Assert.False(window.IsTimingSystem(new TimingSystem("10.0.0.99", Readers.SYSTEM_RFID)));
        }
    }

    /// <summary>The Set Time window reads and sets a reader's clock.</summary>
    public class SetTimeWindowTests : ReaderUiTestBase
    {
        private SetTimeWindow Open(TimingSystem system) => Show(new SetTimeWindow(Parent, system));

        private static DateTime UltraTimeSent(string line)
        {
            Assert.StartsWith("t ", line);
            return DateTime.ParseExact(line[2..^1], "HH:mm:ss dd-MM-yyyy", CultureInfo.InvariantCulture);
        }

        [AvaloniaFact]
        public void Get_time_asks_the_reader_for_its_clock()
        {
            SetTimeWindow window = Open(Ultra().System);

            Click(Find<Button>(window, "Check"));

            Assert.Equal("r\n", Pair.Line());
        }

        [AvaloniaFact]
        public void Set_defaults_to_now()
        {
            SetTimeWindow window = Open(Ultra().System);
            Assert.True(Find<RadioButton>(window, "NowTimeRadioButton").IsChecked);

            Click(Find<Button>(window, "Set"));

            DateTime sent = UltraTimeSent(Pair.Line());
            Assert.True(Math.Abs((sent - DateTime.Now).TotalSeconds) < 5, $"Sent {sent}, now {DateTime.Now}");
            Assert.Empty(Parent.SetAllCalls);
        }

        [AvaloniaFact]
        public void Set_sends_a_chosen_date_and_time()
        {
            SetTimeWindow window = Open(Ultra().System);
            Find<RadioButton>(window, "SpecificTimeRadioButton").IsChecked = true;
            Find<CalendarDatePicker>(window, "SpecificDateBox").SelectedDate = new DateTime(2026, 6, 15);
            Find<MaskedTextBox>(window, "SpecificTimeBox").Text = "08:30:05.000";

            Click(Find<Button>(window, "Set"));

            Assert.Equal(new DateTime(2026, 6, 15, 8, 30, 5), UltraTimeSent(Pair.Line()));
        }

        [AvaloniaTheory]
        [InlineData("de-DE")]
        [InlineData("fi-FI")]
        [InlineData("en-GB")]
        public void Chosen_time_is_the_same_in_any_culture(string culture)
        {
            SetTimeWindow window = Open(Ultra().System);
            Find<RadioButton>(window, "SpecificTimeRadioButton").IsChecked = true;
            Find<MaskedTextBox>(window, "SpecificTimeBox").Text = "08:30:05.000";

            CultureScope.In(culture, () =>
            {
                Find<CalendarDatePicker>(window, "SpecificDateBox").SelectedDate = new DateTime(2026, 6, 5);
                Click(Find<Button>(window, "Set"));
            });

            Assert.Equal(new DateTime(2026, 6, 5, 8, 30, 5), UltraTimeSent(Pair.Line()));
        }

        [AvaloniaFact]
        public void Set_all_hands_the_time_to_the_timing_page_instead()
        {
            SetTimeWindow window = Open(Ultra().System);
            Find<ToggleSwitch>(window, "SetAllCheckBox").IsChecked = true;
            Find<RadioButton>(window, "SpecificTimeRadioButton").IsChecked = true;
            Find<CalendarDatePicker>(window, "SpecificDateBox").SelectedDate = new DateTime(2026, 6, 15);
            Find<MaskedTextBox>(window, "SpecificTimeBox").Text = "08:30:05.000";

            Click(Find<Button>(window, "Set"));

            Assert.Equal([(new DateTime(2026, 6, 15, 8, 30, 5), false)], Parent.SetAllCalls);
            Assert.True(Pair.NothingSent());
        }

        [AvaloniaFact]
        public void Set_all_to_now_tells_the_timing_page_to_use_now()
        {
            SetTimeWindow window = Open(Ultra().System);
            Find<ToggleSwitch>(window, "SetAllCheckBox").IsChecked = true;

            Click(Find<Button>(window, "Set"));

            Assert.True(Assert.Single(Parent.SetAllCalls).Now);
        }

        [AvaloniaFact]
        public void Reader_time_shows_once_the_reader_answers()
        {
            (TimingSystem system, _) = Ultra();
            SetTimeWindow window = Open(system);
            Assert.False(Find<TextBlock>(window, "TimeLabel").IsVisible);

            system.SystemTime = "15 Jun 2026 08:30:05";
            window.UpdateTime();

            Assert.True(Find<TextBlock>(window, "TimeLabel").IsVisible);
            Assert.Equal("Reader time is 15 Jun 2026 08:30:05", Find<TextBlock>(window, "TimeLabel").Text);
            Assert.StartsWith("System time is ", Find<TextBlock>(window, "CurrentTimeLabel").Text);
        }

        [AvaloniaFact]
        public void Done_closes_and_lets_the_timing_page_open_another()
        {
            SetTimeWindow window = Open(Ultra().System);

            Click(Find<Button>(window, "Done"));

            Assert.False(window.IsVisible);
            Assert.Equal(1, Parent.CloseTimeCalls);
        }
    }

    /// <summary>The Ultra's settings window. Each Save sends one setting, coded the way the Ultra expects.</summary>
    public class UltraSettingsWindowTests : ReaderUiTestBase
    {
        private RfidSettings Open(RfidUltraInterface ultra)
        {
            ultra.OpenSettings();
            Pump();
            RfidSettings window = AddedWindow<RfidSettings>();
            // Opening asks for the reader's status and settings (they often arrive in one packet).
            Assert.Equal("?\nU\n", Encoding.Latin1.GetString(Pair.ReadLines(2)));
            return window;
        }

        private RfidSettings Open() => Open(Ultra().Ultra);

        private static string Setting(char code, string value) => $"u{code}{value}{Term}\n";

        [AvaloniaFact]
        public void Opening_asks_the_reader_for_its_settings()
        {
            Open();
        }

        [AvaloniaFact]
        public void Reported_settings_fill_the_window()
        {
            RfidSettings window = Open();

            window.UpdateView(new RfidSettingsHolder
            {
                UltraId = 42,
                ChipType = RfidSettingsHolder.ChipTypeEnum.HEX,
                GatingMode = RfidSettingsHolder.GatingModeEnum.FIRST_TIME_SEEN,
                GatingInterval = 7,
                Beep = RfidSettingsHolder.BeepEnum.ONLY_FIRST_SEEN,
                BeepVolume = RfidSettingsHolder.BeepVolumeEnum.LOUD,
                SetFromGps = RfidSettingsHolder.GpsEnum.SET,
                TimeZone = -6,
                Status = RfidSettingsHolder.StatusEnum.STARTED,
            });
            Pump();

            Assert.Equal(42, Find<Slider>(window, "IdSlider").Value);
            Assert.Equal("42", Find<TextBlock>(window, "IdDisplay").Text);
            Assert.Equal(1, Find<ComboBox>(window, "ChipBox").SelectedIndex);
            Assert.Equal(2, Find<ComboBox>(window, "GatingModeBox").SelectedIndex);
            Assert.Equal(7, Find<Slider>(window, "GatingSlider").Value);
            Assert.Equal(1, Find<ComboBox>(window, "WhenBeepBox").SelectedIndex);
            Assert.Equal(2, Find<ComboBox>(window, "VolumeBox").SelectedIndex);
            Assert.True(Find<ToggleSwitch>(window, "SetGpsSwitch").IsChecked);
            Assert.Equal(-6, Find<Slider>(window, "TimeZoneSlider").Value);
            Assert.Equal("-6", Find<TextBlock>(window, "TimeZoneDisplay").Text);
            Assert.True(Find<ToggleSwitch>(window, "ReadingSwitch").IsChecked);
        }

        [AvaloniaFact]
        public void Out_of_range_values_leave_the_window_alone()
        {
            RfidSettings window = Open();
            Find<Slider>(window, "IdSlider").Value = 9;

            window.UpdateView(new RfidSettingsHolder { UltraId = 300, GatingInterval = 50, TimeZone = 30 });
            Pump();

            Assert.Equal(9, Find<Slider>(window, "IdSlider").Value);
            Assert.Equal(0, Find<Slider>(window, "GatingSlider").Value);
            Assert.Equal(0, Find<Slider>(window, "TimeZoneSlider").Value);
        }

        [AvaloniaTheory]
        [InlineData("ChipBox", "SaveChip", 1, RfidUltraCodes.CHIP_OUT_TYPE, (char)0x01)]
        [InlineData("ChipBox", "SaveChip", 0, RfidUltraCodes.CHIP_OUT_TYPE, (char)0x00)]
        [InlineData("GatingModeBox", "SaveGatingMode", 2, RfidUltraCodes.GATING_MODE, (char)0x02)]
        [InlineData("WhenBeepBox", "SaveWhenBeep", 1, RfidUltraCodes.WHEN_BEEP, (char)0x01)]
        [InlineData("VolumeBox", "SaveVolume", 0, RfidUltraCodes.BEEPER_VOLUME, (char)0x00)]
        [InlineData("VolumeBox", "SaveVolume", 2, RfidUltraCodes.BEEPER_VOLUME, (char)0x02)]
        public void Saving_a_choice_sends_its_code(string box, string button, int index, char code, char value)
        {
            RfidSettings window = Open();
            Find<ComboBox>(window, box).SelectedIndex = index;

            Click(Find<Button>(window, button));

            Assert.Equal(Setting(code, value.ToString()), Pair.Line());
        }

        [AvaloniaTheory]
        [InlineData("SaveChip")]
        [InlineData("SaveGatingMode")]
        [InlineData("SaveWhenBeep")]
        [InlineData("SaveVolume")]
        public void Saving_with_nothing_chosen_sends_nothing(string button)
        {
            RfidSettings window = Open();

            Click(Find<Button>(window, button));

            Assert.True(Pair.NothingSent());
        }

        [AvaloniaFact]
        public void Gating_interval_and_time_zone_send_their_numbers()
        {
            RfidSettings window = Open();
            Find<Slider>(window, "GatingSlider").Value = 5;
            Find<Slider>(window, "TimeZoneSlider").Value = -7;

            Click(Find<Button>(window, "SaveGatingInterval"));
            Assert.Equal(Setting(RfidUltraCodes.GATING_INTERVAL, "5"), Pair.Line());
            Click(Find<Button>(window, "SaveTimezone"));
            Assert.Equal(Setting(RfidUltraCodes.TIME_ZONE, "-7"), Pair.Line());
        }

        [AvaloniaFact]
        public void Sliders_show_their_value()
        {
            RfidSettings window = Open();

            Find<Slider>(window, "GatingSlider").Value = 12;
            Find<Slider>(window, "IdSlider").Value = 200;

            Assert.Equal("12", Find<TextBlock>(window, "GatingDisplay").Text);
            Assert.Equal("200", Find<TextBlock>(window, "IdDisplay").Text);
        }

        [AvaloniaTheory]
        [InlineData(true, (char)0x01)]
        [InlineData(false, (char)0x00)]
        public void Gps_switch_sends_on_or_off(bool on, char value)
        {
            RfidSettings window = Open();
            ToggleSwitch gps = Find<ToggleSwitch>(window, "SetGpsSwitch");
            gps.IsChecked = on;

            Click(gps);

            Assert.Equal(Setting(RfidUltraCodes.AUTO_SET_GPS, value.ToString()), Pair.Line());
        }

        [AvaloniaTheory]
        [InlineData(true, "R\n")]
        [InlineData(false, "S\n")]
        public void Reading_switch_starts_and_stops_reading(bool on, string command)
        {
            RfidSettings window = Open();
            ToggleSwitch reading = Find<ToggleSwitch>(window, "ReadingSwitch");
            reading.IsChecked = on;

            Click(reading);

            Assert.Equal(command, Pair.Line());
        }

        [AvaloniaFact]
        public void Closing_lets_the_reader_open_settings_again()
        {
            (_, RfidUltraInterface ultra) = Ultra();
            RfidSettings first = Open(ultra);

            Click(Find<Button>(first, "DoneButton"));
            Assert.False(first.IsVisible);
            Assert.Equal(1, MainWindow.WindowFinalizeCalls);

            RfidSettings second = Open(ultra);
            Assert.NotSame(first, second);
        }

        [AvaloniaFact]
        public void Disconnecting_closes_the_settings_window()
        {
            (_, RfidUltraInterface ultra) = Ultra();
            RfidSettings window = Open(ultra);

            ultra.CloseSettings();
            Pump();

            Assert.False(window.IsVisible);
        }
    }

    /// <summary>The Chronokeep portal's settings window, filled from and saved to the portal.</summary>
    public class PortalSettingsWindowTests : ReaderUiTestBase
    {
        private ChronokeepInterface portal = null!;

        private ChronokeepSettings Open()
        {
            (_, portal) = Portal();
            portal.OpenSettings();
            Pump();
            ChronokeepSettings window = AddedWindow<ChronokeepSettings>();
            Assert.Equal("settings_get_all", PortalSent().GetProperty("command").GetString());
            return window;
        }

        private static string Setting(string name, string value) => $"{{\"name\":\"{name}\",\"value\":\"{value}\"}}";

        private static string AllSettings(string volume = "0.7", string screen = "PCF8574T", int autoUpload = 1) =>
            "{\"command\":\"settings_all\",\"settings\":[" + string.Join(",",
                Setting("SETTING_PORTAL_NAME", "Finish Portal"),
                Setting("SETTING_READ_WINDOW", "20"),
                Setting("SETTING_CHIP_TYPE", "HEX"),
                Setting("SETTING_PLAY_SOUND", "true"),
                Setting("SETTING_VOLUME", volume),
                Setting("SETTING_VOICE", "michael"),
                Setting("SETTING_UPLOAD_INTERVAL", "30"),
                Setting("SETTING_BEEP_IGNORE", "5"),
                Setting("SETTING_NTFY_URL", "https://ntfy.example"),
                Setting("SETTING_NTFY_TOPIC", "race"),
                Setting("SETTING_NTFY_USER", "user"),
                Setting("SETTING_NTFY_PASS", "pass"),
                Setting("SETTING_ENABLE_NTFY", "true"),
                Setting("SETTING_SCREEN_TYPE", screen)) +
            $"],\"readers\":[],\"apis\":[],\"auto_upload\":{autoUpload},\"portal_version\":\"1.2.3\"}}\n";

        private void Receive(string message)
        {
            portal.ParseMessages(message, Pair.Client);
            Pump();
        }

        private Dictionary<string, string> SentSettings()
        {
            JsonElement sent = PortalSent();
            Assert.Equal("settings_set", sent.GetProperty("command").GetString());
            return sent.GetProperty("settings").EnumerateArray()
                .ToDictionary(s => s.GetProperty("name").GetString()!, s => s.GetProperty("value").GetString()!);
        }

        [AvaloniaFact]
        public void Portal_settings_fill_the_window()
        {
            ChronokeepSettings window = Open();

            Receive(AllSettings());

            Assert.Equal("1.2.3", Find<TextBlock>(window, "VersionBlock").Text);
            Assert.Equal("Finish Portal", Find<TextBox>(window, "NameBox").Text);
            Assert.Equal("20", Find<TextBox>(window, "ReadWindowBox").Text);
            Assert.Equal(1, Find<ComboBox>(window, "ChipTypeBox").SelectedIndex);
            Assert.True(Find<ToggleSwitch>(window, "SoundBox").IsChecked);
            Assert.Equal(7, Find<Slider>(window, "VolumeSlider").Value, 3);
            Assert.Equal(1, Find<ComboBox>(window, "VoiceBox").SelectedIndex);
            Assert.Equal(30, Find<Slider>(window, "UploadSlider").Value);
            Assert.Equal(5, Find<Slider>(window, "BeepSlider").Value);
            Assert.Equal("https://ntfy.example", Find<TextBox>(window, "NtfyUrlBox").Text);
            Assert.Equal("race", Find<TextBox>(window, "NtfyTopicBox").Text);
            Assert.True(Find<ToggleSwitch>(window, "EnableNtfySwitch").IsChecked);
            Assert.True(Find<StackPanel>(window, "ScreenPanel").IsVisible);
            Assert.Equal(1, Find<ComboBox>(window, "ScreenBox").SelectedIndex);
            Assert.True(Find<ToggleSwitch>(window, "AutoResultsSwitch").IsChecked);
        }

        [AvaloniaTheory]
        [InlineData("de-DE")]
        [InlineData("fr-FR")]
        public void Portal_volume_reads_the_same_in_any_culture(string culture)
        {
            ChronokeepSettings window = Open();

            CultureScope.In(culture, () => Receive(AllSettings(volume: "0.7")));

            Assert.Equal(7, Find<Slider>(window, "VolumeSlider").Value, 3);
        }

        [AvaloniaFact]
        public void Screen_choice_is_hidden_when_the_portal_has_no_screen()
        {
            ChronokeepSettings window = Open();

            Receive(AllSettings(screen: ""));

            Assert.False(Find<StackPanel>(window, "ScreenPanel").IsVisible);
        }

        [AvaloniaFact]
        public void Save_sends_every_setting_back_to_the_portal()
        {
            ChronokeepSettings window = Open();
            Receive(AllSettings());
            Find<TextBox>(window, "NameBox").Text = "  Start Portal ";
            Find<ComboBox>(window, "VoiceBox").SelectedIndex = 2;
            Find<ComboBox>(window, "ScreenBox").SelectedIndex = 0;

            Click(Find<Button>(window, "SaveButton"));

            Dictionary<string, string> sent = SentSettings();
            Assert.Equal("Start Portal", sent["SETTING_PORTAL_NAME"]);
            Assert.Equal("20", sent["SETTING_READ_WINDOW"]);
            Assert.Equal("HEX", sent["SETTING_CHIP_TYPE"]);
            Assert.Equal("true", sent["SETTING_PLAY_SOUND"]);
            Assert.Equal("0.7", sent["SETTING_VOLUME"]);
            Assert.Equal("custom", sent["SETTING_VOICE"]);
            Assert.Equal("30", sent["SETTING_UPLOAD_INTERVAL"]);
            Assert.Equal("5", sent["SETTING_BEEP_IGNORE"]);
            Assert.Equal("race", sent["SETTING_NTFY_TOPIC"]);
            Assert.Equal("true", sent["SETTING_ENABLE_NTFY"]);
            Assert.Equal("ADAFRUIT", sent["SETTING_SCREEN_TYPE"]);
        }

        [AvaloniaFact]
        public void Save_leaves_the_screen_alone_when_the_portal_has_none()
        {
            ChronokeepSettings window = Open();
            Receive(AllSettings(screen: ""));

            Click(Find<Button>(window, "SaveButton"));

            Assert.False(SentSettings().ContainsKey("SETTING_SCREEN_TYPE"));
        }

        [AvaloniaFact]
        public void Saved_settings_coming_back_close_the_window()
        {
            ChronokeepSettings window = Open();
            Receive(AllSettings());

            Click(Find<Button>(window, "SaveButton"));
            SentSettings();
            Receive(AllSettings());

            Assert.False(window.IsVisible);
        }

        [AvaloniaFact]
        public void A_bad_read_window_sends_nothing_and_keeps_the_window_open()
        {
            ChronokeepSettings window = Open();
            Receive(AllSettings());
            Find<TextBox>(window, "ReadWindowBox").Text = "soon";

            Click(Find<Button>(window, "SaveButton"));
            Assert.True(Pair.NothingSent());
            // A later update from the portal (another computer changing a setting) must not close it.
            Receive(AllSettings());

            Assert.True(window.IsVisible);
        }

        [AvaloniaTheory]
        [InlineData(true, "start")]
        [InlineData(false, "stop")]
        public void Auto_upload_switch_starts_and_stops_uploads(bool on, string query)
        {
            ChronokeepSettings window = Open();
            Receive(AllSettings(autoUpload: on ? 3 : 1));
            ToggleSwitch auto = Find<ToggleSwitch>(window, "AutoResultsSwitch");
            auto.IsChecked = on;

            Click(auto);

            JsonElement sent = PortalSent();
            Assert.Equal("api_remote_auto_upload", sent.GetProperty("command").GetString());
            Assert.Equal(query, sent.GetProperty("query").GetString());
        }

        [AvaloniaFact]
        public void Auto_upload_switch_is_locked_while_uploads_stop()
        {
            ChronokeepSettings window = Open();

            Receive(AllSettings(autoUpload: 2));

            Assert.False(Find<ToggleSwitch>(window, "AutoResultsSwitch").IsEnabled);
        }

        [AvaloniaFact]
        public void Manual_upload_asks_the_portal_to_upload()
        {
            ChronokeepSettings window = Open();

            Click(Find<Button>(window, "ManualResultsButton"));

            Assert.Equal("api_remote_manual_upload", PortalSent().GetProperty("command").GetString());
        }

        [AvaloniaFact]
        public void Add_reader_and_api_buttons_show_with_their_lists()
        {
            ChronokeepSettings window = Open();
            Expander readers = Find<Expander>(window, "ReaderExpander");
            Expander apis = Find<Expander>(window, "ApiExpander");

            readers.IsExpanded = true;
            apis.IsExpanded = true;
            Pump();

            Assert.True(Find<Button>(window, "AddReaderButton").IsVisible);
            Assert.True(Find<Button>(window, "AddApiButton").IsVisible);
        }

        [AvaloniaFact]
        public void Add_reader_sends_a_new_zebra_reader()
        {
            ChronokeepSettings window = Open();

            Click(Find<Button>(window, "AddReaderButton"));

            JsonElement sent = PortalSent();
            Assert.Equal("reader_add", sent.GetProperty("command").GetString());
            Assert.Equal("New Reader", sent.GetProperty("name").GetString());
        }

        [AvaloniaFact]
        public void Readers_and_apis_from_the_portal_are_listed()
        {
            ChronokeepSettings window = Open();

            Receive("{\"command\":\"readers\",\"readers\":[{\"id\":1,\"name\":\"Zebra A\",\"kind\":\"ZEBRA\",\"ip_address\":\"10.0.0.20\",\"port\":5084,\"auto_connect\":true,\"reading\":false,\"connected\":true}]}\n");
            Receive("{\"command\":\"api_list\",\"apis\":[{\"id\":4,\"nickname\":\"Results\",\"kind\":\"CHRONOKEEP_REMOTE\",\"token\":\"t\",\"uri\":\"https://example\"}]}\n");

            Assert.Single(Find<ListBox>(window, "ReaderListView").Items);
            Assert.Single(Find<ListBox>(window, "ApiListView").Items);

            // A reader the portal stops reporting is dropped from the list.
            Receive("{\"command\":\"readers\",\"readers\":[]}\n");
            Assert.Empty(Find<ListBox>(window, "ReaderListView").Items);
        }

        [AvaloniaFact]
        public void Closing_lets_the_portal_open_settings_again()
        {
            ChronokeepSettings window = Open();

            Click(Find<Button>(window, "CloseButton"));
            Assert.False(window.IsVisible);

            portal.OpenSettings();
            Pump();
            Assert.Equal("settings_get_all", PortalSent().GetProperty("command").GetString());
        }
    }
}
