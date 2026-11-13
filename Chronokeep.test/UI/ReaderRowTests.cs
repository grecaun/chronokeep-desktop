using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Chronokeep.Objects;
using Chronokeep.Timing.Interfaces;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.Parts;
using Chronokeep.UI.Timing.ReaderSettings;
using Chronokeep.UI.Timing.Windows;
using Readers = Chronokeep.Constants.Readers;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.UI
{
    /// <summary>One reader row on the Timing page: its fields, its state while connecting, and its buttons.</summary>
    public class ReaderPartTests : ReaderUiTestBase
    {
        private List<TimingLocation> Locations() =>
        [
            new(TimingConstants.LOCATION_START, 1, "Start", 0, 0),
            new(TimingConstants.LOCATION_FINISH, 1, "Finish", 0, 0),
            new(7, 1, "Mile 3", 0, 0),
        ];

        private ReaderPart Row(TimingSystem system)
        {
            ReaderPart part = new(Parent, system, Locations());
            Host(part);
            return part;
        }

        private static string? SelectedTag(ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Tag?.ToString();

        [AvaloniaFact]
        public void Shows_the_readers_type_address_and_location()
        {
            ReaderPart part = Row(new TimingSystem(0, "10.0.0.5", 23, 7, Readers.SYSTEM_RFID));

            Assert.Equal(Readers.SYSTEM_RFID, SelectedTag(Find<ComboBox>(part, "ReaderType")));
            Assert.Equal("10.0.0.5", Find<TextBox>(part, "ReaderIp").Text);
            Assert.Equal("23", Find<TextBox>(part, "ReaderPort").Text);
            Assert.Equal("7", SelectedTag(Find<ComboBox>(part, "ReaderLocation")));
        }

        [AvaloniaFact]
        public void Unknown_location_falls_back_to_the_first()
        {
            ReaderPart part = Row(new TimingSystem(0, "10.0.0.5", 23, 99, Readers.SYSTEM_RFID));

            Assert.Equal(TimingConstants.LOCATION_START.ToString(), SelectedTag(Find<ComboBox>(part, "ReaderLocation")));
        }

        [AvaloniaTheory]
        [InlineData(Readers.SYSTEM_RFID, "23", true)]
        [InlineData(Readers.SYSTEM_CHRONOKEEP_PORTAL, "4488", false)]
        public void Changing_type_sets_its_port(string type, string port, bool portEditable)
        {
            ReaderPart part = Row(new TimingSystem("10.0.0.5", Readers.SYSTEM_IPICO));
            ComboBox types = Find<ComboBox>(part, "ReaderType");

            types.SelectedItem = types.Items.Cast<ComboBoxItem>().Single(i => (string)i.Tag! == type);
            Pump();

            Assert.Equal(port, Find<TextBox>(part, "ReaderPort").Text);
            Assert.Equal(portEditable, Find<TextBox>(part, "ReaderPort").IsEnabled);
            Assert.Equal(type, part.Reader.Type);
        }

        [AvaloniaFact]
        public void Disconnected_reader_can_be_edited_and_connected_but_not_used()
        {
            ReaderPart part = Row(new TimingSystem("10.0.0.5", Readers.SYSTEM_RFID));

            Assert.True(Find<TextBox>(part, "ReaderIp").IsEnabled);
            Assert.True(Find<Button>(part, "ConnectButton").IsEnabled);
            Assert.Equal("connect", Find<Button>(part, "ConnectButton").Tag);
            Assert.False(Find<Button>(part, "ClockButton").IsEnabled);
            Assert.False(Find<Button>(part, "RewindButton").IsEnabled);
            Assert.False(Find<Button>(part, "SettingsButton").IsEnabled);
            Assert.False(Find<Button>(part, "ReaderButton").IsEnabled);
        }

        [AvaloniaFact]
        public void Connect_sends_the_typed_address_and_location_to_the_timing_page()
        {
            ReaderPart part = Row(new TimingSystem("0.0.0.0", Readers.SYSTEM_RFID));
            Find<TextBox>(part, "ReaderIp").Text = " 192.168.1.40 ";
            Find<TextBox>(part, "ReaderPort").Text = "10000";
            ComboBox location = Find<ComboBox>(part, "ReaderLocation");
            location.SelectedIndex = 2;

            Click(Find<Button>(part, "ConnectButton"));

            TimingSystem sent = Assert.Single(Parent.Connected);
            Assert.Equal("192.168.1.40", sent.IpAddress);
            Assert.Equal(10000, sent.Port);
            Assert.Equal(7, sent.LocationId);
            Assert.Equal("Mile 3", sent.LocationName);
            // While connecting nothing can be changed or pressed.
            Assert.Equal(SYSTEM_STATUS.WORKING, sent.Status);
            Assert.False(Find<TextBox>(part, "ReaderIp").IsEnabled);
            Assert.False(Find<Button>(part, "ConnectButton").IsEnabled);
            Assert.False(Find<Button>(part, "RemoveButton").IsEnabled);
        }

        [AvaloniaTheory]
        [InlineData("192.168.1", "23")]
        [InlineData("192.168.1.256", "23")]
        [InlineData("192.168.1.40", "70000")]
        public void Connect_refuses_a_bad_address_or_port(string ip, string port)
        {
            ReaderPart part = Row(new TimingSystem("0.0.0.0", Readers.SYSTEM_RFID));
            Find<TextBox>(part, "ReaderIp").Text = ip;
            Find<TextBox>(part, "ReaderPort").Text = port;

            Click(Find<Button>(part, "ConnectButton"));

            Assert.Empty(Parent.Connected);
            Assert.Equal(SYSTEM_STATUS.DISCONNECTED, part.Reader.Status);
            Assert.True(Find<TextBox>(part, "ReaderIp").IsEnabled);
        }

        [AvaloniaFact]
        public void Connected_reader_locks_its_fields_and_unlocks_its_tools()
        {
            ReaderPart part = Row(Ultra().System);

            Assert.False(Find<TextBox>(part, "ReaderIp").IsEnabled);
            Assert.False(Find<ComboBox>(part, "ReaderType").IsEnabled);
            Assert.False(Find<ComboBox>(part, "ReaderLocation").IsEnabled);
            Assert.False(Find<Button>(part, "RemoveButton").IsEnabled);
            Assert.True(Find<Button>(part, "ClockButton").IsEnabled);
            Assert.True(Find<Button>(part, "RewindButton").IsEnabled);
            Assert.True(Find<Button>(part, "SettingsButton").IsEnabled);
            Assert.True(Find<Button>(part, "ReaderButton").IsEnabled);
            Assert.Equal("disconnect", Find<Button>(part, "ConnectButton").Tag);
        }

        [AvaloniaFact]
        public void Ipico_lite_has_no_clock_rewind_or_settings()
        {
            TimingSystem lite = new("10.0.0.8", Readers.SYSTEM_IPICO_LITE) { Status = SYSTEM_STATUS.CONNECTED };

            ReaderPart part = Row(lite);

            Assert.False(Find<Button>(part, "ClockButton").IsEnabled);
            Assert.False(Find<Button>(part, "RewindButton").IsEnabled);
            Assert.False(Find<Button>(part, "SettingsButton").IsEnabled);
            Assert.True(Find<Button>(part, "ConnectButton").IsEnabled);
        }

        [AvaloniaFact]
        public void Disconnect_asks_the_timing_page_and_closes_the_settings_window()
        {
            (TimingSystem system, RfidUltraInterface ultra) = Ultra();
            ReaderPart part = Row(system);
            Click(Find<Button>(part, "SettingsButton"));
            RfidSettings settings = AddedWindow<RfidSettings>();

            Click(Find<Button>(part, "ConnectButton"));

            Assert.Same(system, Assert.Single(Parent.Disconnected));
            Assert.Equal(SYSTEM_STATUS.WORKING, system.Status);
            Assert.False(settings.IsVisible);
        }

        [AvaloniaFact]
        public void Clock_and_rewind_open_their_windows_for_this_reader()
        {
            TimingSystem system = Ultra().System;
            ReaderPart part = Row(system);

            Click(Find<Button>(part, "ClockButton"));
            Click(Find<Button>(part, "RewindButton"));

            Assert.Same(system, Assert.Single(Parent.TimeOpened));
            Assert.Same(system, Assert.Single(Parent.RewindOpened));
        }

        [AvaloniaTheory]
        [InlineData(TimingSystem.READING_STATUS_READING, "S\n")]
        [InlineData(TimingSystem.READING_STATUS_PARTIAL, "S\n")]
        [InlineData(TimingSystem.READING_STATUS_STOPPED, "R\n")]
        public void Reader_button_toggles_reading(string status, string command)
        {
            TimingSystem system = Ultra().System;
            system.SystemStatus = status;
            ReaderPart part = Row(system);

            Click(Find<Button>(part, "ReaderButton"));

            Assert.Equal(command, Pair.Line());
        }

        [AvaloniaFact]
        public void Reader_button_does_nothing_until_the_reading_state_is_known()
        {
            ReaderPart part = Row(Ultra().System);

            Click(Find<Button>(part, "ReaderButton"));

            Assert.True(Pair.NothingSent());
        }

        [AvaloniaTheory]
        [InlineData(TimingSystem.READING_STATUS_READING, "#FF32CD32")]
        [InlineData(TimingSystem.READING_STATUS_STOPPED, "#FFFF0000")]
        [InlineData(TimingSystem.READING_STATUS_PARTIAL, "#FFEE82EE")]
        public void Reader_button_colour_shows_the_reading_state(string status, string colour)
        {
            TimingSystem system = Ultra().System;
            ReaderPart part = Row(system);

            system.SystemStatus = status;
            part.UpdateStatus();

            Assert.Equal(Color.Parse(colour), ((ISolidColorBrush)Find<Button>(part, "ReaderButton").Foreground!).Color);
        }

        [AvaloniaFact]
        public void Settings_opens_the_readers_settings_window()
        {
            ReaderPart part = Row(Ultra().System);

            Click(Find<Button>(part, "SettingsButton"));

            Assert.IsType<RfidSettings>(AddedWindow<RfidSettings>());
        }

        [AvaloniaFact]
        public void Only_saved_readers_can_be_removed()
        {
            ReaderPart unsaved = Row(new TimingSystem("10.0.0.5", Readers.SYSTEM_RFID));
            ReaderPart saved = Row(new TimingSystem(3, "10.0.0.6", 23, TimingConstants.LOCATION_FINISH, Readers.SYSTEM_RFID));

            Assert.False(Find<Button>(unsaved, "RemoveButton").IsVisible);
            Assert.True(Find<Button>(saved, "RemoveButton").IsVisible);
            Click(Find<Button>(saved, "RemoveButton"));

            Assert.Same(saved.Reader, Assert.Single(Parent.Removed));
        }

        [AvaloniaFact]
        public void Update_reader_blanks_a_bad_address_and_port()
        {
            ReaderPart part = Row(new TimingSystem("10.0.0.5", Readers.SYSTEM_RFID));
            Find<TextBox>(part, "ReaderIp").Text = "not an ip";
            Find<TextBox>(part, "ReaderPort").Text = "99999";

            part.UpdateReader();

            Assert.Equal("", part.Reader.IpAddress);
            Assert.Equal(-1, part.Reader.Port);
        }

        [AvaloniaFact]
        public void New_locations_keep_the_chosen_one()
        {
            ReaderPart part = Row(new TimingSystem(0, "10.0.0.5", 23, 7, Readers.SYSTEM_RFID));

            part.UpdateLocations([new(TimingConstants.LOCATION_FINISH, 1, "Finish", 0, 0), new(7, 1, "Mile 3", 0, 0), new(8, 1, "Mile 5", 0, 0)]);

            ComboBox location = Find<ComboBox>(part, "ReaderLocation");
            Assert.Equal(3, location.Items.Count);
            Assert.Equal("7", SelectedTag(location));
        }
    }

    /// <summary>The Timing page's list of reader rows.</summary>
    public class TimingPageReaderTests : ReaderUiTestBase
    {
        private TimingPage OpenPage()
        {
            TimingPage page = new(MainWindow, Db);
            Host(page);
            return page;
        }

        private static List<ReaderPart> Rows(TimingPage page) =>
            [.. Find<ListBox>(page, "ReadersBox").Items.Cast<ReaderPart>()];

        [AvaloniaFact]
        public void Starts_with_blank_rows_of_the_default_reader_type()
        {
            TimingPage page = OpenPage();

            List<ReaderPart> rows = Rows(page);
            Assert.Equal(4, rows.Count);
            Assert.All(rows, r => Assert.Equal("0.0.0.0", r.Reader.IpAddress));
            Assert.All(rows, r => Assert.Equal(Readers.DEFAULT_TIMING_SYSTEM, r.Reader.Type));
        }

        [AvaloniaFact]
        public void Connected_readers_are_listed_first()
        {
            TimingSystem system = Ultra().System;
            MainWindow.ConnectedSystems.Add(system);

            TimingPage page = OpenPage();

            List<ReaderPart> rows = Rows(page);
            Assert.Same(system, rows[0].Reader);
            Assert.Equal(4, rows.Count);
        }

        [AvaloniaFact]
        public void A_row_is_added_once_every_row_has_a_reader()
        {
            TimingPage page = OpenPage();
            foreach (ReaderPart row in Rows(page))
            {
                row.Reader.IpAddress = $"10.0.0.{Rows(page).IndexOf(row) + 1}";
            }

            page.UpdateView();

            Assert.Equal(5, Rows(page).Count);
            Assert.Equal("0.0.0.0", Rows(page)[^1].Reader.IpAddress);
        }

        [AvaloniaFact]
        public void Connect_from_a_row_reaches_the_main_window()
        {
            TimingPage page = OpenPage();
            ReaderPart row = Rows(page)[0];
            Find<TextBox>(row, "ReaderIp").Text = "192.168.1.40";

            Click(Find<Button>(row, "ConnectButton"));

            Assert.Equal("192.168.1.40", Assert.Single(MainWindow.ConnectRequests).IpAddress);
        }

        [AvaloniaFact]
        public void Closing_saves_typed_readers_and_skips_blank_rows()
        {
            TimingPage page = OpenPage();
            ReaderPart row = Rows(page)[1];
            Find<TextBox>(row, "ReaderIp").Text = "192.168.1.41";
            Find<TextBox>(row, "ReaderPort").Text = "4488";

            page.Closing();

            TimingSystem saved = Assert.Single(Db.GetTimingSystems());
            Assert.Equal("192.168.1.41", saved.IpAddress);
            Assert.Equal(4488, saved.Port);
        }

        [AvaloniaFact]
        public void Closing_drops_readers_that_were_cleared()
        {
            Db.AddTimingSystem(new TimingSystem("192.168.1.50", Readers.SYSTEM_RFID));
            TimingSystem stored = Assert.Single(Db.GetTimingSystems());
            MainWindow.ConnectedSystems.Add(stored);
            TimingPage page = OpenPage();
            Find<TextBox>(Rows(page).Single(r => r.Reader == stored), "ReaderIp").Text = "";

            page.Closing();

            Assert.Empty(Db.GetTimingSystems());
        }

        [AvaloniaFact]
        public void Remove_deletes_the_saved_reader_and_its_row()
        {
            Db.AddTimingSystem(new TimingSystem("192.168.1.50", Readers.SYSTEM_RFID));
            TimingSystem stored = Assert.Single(Db.GetTimingSystems());
            MainWindow.ConnectedSystems.Add(stored);
            TimingPage page = OpenPage();
            ReaderPart row = Rows(page).Single(r => r.Reader == stored);

            Click(Find<Button>(row, "RemoveButton"));

            Assert.Empty(Db.GetTimingSystems());
            Assert.DoesNotContain(row, Rows(page));
        }

        [AvaloniaFact]
        public void Clock_opens_set_time_and_reader_answers_show_in_it()
        {
            ShowMainWindow();
            TimingSystem system = Ultra().System;
            MainWindow.ConnectedSystems.Add(system);
            TimingPage page = OpenPage();

            Click(Find<Button>(Rows(page)[0], "ClockButton"));
            SetTimeWindow window = MainWindow.OwnedWindows.OfType<SetTimeWindow>().Single();
            system.SystemTime = "15 Jun 2026 08:30:05";
            page.NewMessage();

            Assert.Equal("Reader time is 15 Jun 2026 08:30:05", Find<TextBlock>(window, "TimeLabel").Text);
            window.Close();
        }

        [AvaloniaFact]
        public void Reader_dropping_closes_its_rewind_window()
        {
            ShowMainWindow();
            TimingSystem system = Ultra().System;
            MainWindow.ConnectedSystems.Add(system);
            TimingPage page = OpenPage();
            Click(Find<Button>(Rows(page)[0], "RewindButton"));
            RewindWindow window = MainWindow.OwnedWindows.OfType<RewindWindow>().Single();

            system.Status = SYSTEM_STATUS.DISCONNECTED;
            page.UpdateView();
            Pump();

            Assert.False(window.IsVisible);
        }

        [AvaloniaFact]
        public void Set_all_only_sets_connected_readers()
        {
            TimingSystem connected = Ultra().System;
            using SocketPairScope other = new();
            TimingSystem dropped = new("10.0.0.9", Readers.SYSTEM_RFID) { Status = SYSTEM_STATUS.DISCONNECTED };
            RfidUltraInterface droppedUltra = new(Db, TimingConstants.LOCATION_FINISH, MainWindow);
            droppedUltra.SetMainSocket(other.Pair.Client);
            dropped.SystemInterface = droppedUltra;
            MainWindow.ConnectedSystems.AddRange([connected, dropped]);
            TimingPage page = OpenPage();

            page.SetAllTimingSystemsToTime(new DateTime(2026, 6, 15, 8, 30, 5), false);

            Assert.Equal("t 08:30:05 15-06-2026\n", Pair.Line());
            Assert.True(other.Pair.NothingSent());
        }

        private sealed class SocketPairScope : IDisposable
        {
            internal Timing.SocketPair Pair { get; } = new();
            public void Dispose() => Pair.Dispose();
        }
    }
}
