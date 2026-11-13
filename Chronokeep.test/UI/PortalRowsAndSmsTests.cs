using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Chronokeep.Objects;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.Timing.Interfaces;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.Timing.Notifications;
using Chronokeep.UI.Timing.ReaderSettings;
using Chronokeep.UI.Timing.ReaderSettings.Parts;
using System.Text.Json;

namespace Chronokeep.Test.UI
{
    /// <summary>
    /// The reader and API rows inside the Chronokeep Portal settings window: what they show from the
    /// portal, and the commands they send back to it.
    /// </summary>
    public class PortalRowTests : ReaderUiTestBase
    {
        private ChronokeepInterface portal = null!;
        private ChronokeepSettings window = null!;

        private void Open()
        {
            (_, portal) = Portal();
            portal.OpenSettings();
            Pump();
            window = AddedWindow<ChronokeepSettings>();
            Assert.Equal("settings_get_all", PortalSent().GetProperty("command").GetString());
        }

        private void Receive(string message)
        {
            portal.ParseMessages(message + "\n", Pair.Client);
            Pump();
        }

        private static string ReaderJson(long id, string name, string ip = "10.0.0.20", int port = 5084, bool connected = false,
            bool reading = false, bool auto = true, string antennas = "[2,1,0,0]", string kind = "ZEBRA") =>
            $$"""{"id":{{id}},"name":"{{name}}","kind":"{{kind}}","ip_address":"{{ip}}","port":{{port}},"auto_connect":{{(auto ? "true" : "false")}},"reading":{{(reading ? "true" : "false")}},"connected":{{(connected ? "true" : "false")}},"antennas":{{antennas}}}""";

        private ReaderSubPart ShowReader(string readerJson)
        {
            Receive($$"""{"command":"readers","readers":[{{readerJson}}]}""");
            return Find<ListBox>(window, "ReaderListView").Items.Cast<ReaderSubPart>().Single();
        }

        private ApiPart ShowApi(string kind = "CHRONOKEEP_REMOTE_SELF", string uri = "https://self.example/")
        {
            Receive($$"""{"command":"api_list","apis":[{"id":4,"nickname":"Results","kind":"{{kind}}","token":"tok","uri":"{{uri}}","enabled":true,"heartbeat":false,"device_name":"finish"}]}""");
            return Find<ListBox>(window, "ApiListView").Items.Cast<ApiPart>().Single();
        }

        private static List<(string Number, Color? Colour)> Antennas(ReaderSubPart row) =>
            [.. Find<StackPanel>(row, "AntennaPanel").Children.Cast<Border>()
                .Select(b => (((TextBlock)b.Child!).Text!, (b.Background as ISolidColorBrush)?.Color))];

        // ---- Reader rows ----

        [AvaloniaFact]
        public void Reader_row_shows_the_portals_reader()
        {
            Open();

            ReaderSubPart row = ShowReader(ReaderJson(1, "Zebra A", connected: true, auto: false));

            Assert.Equal("Zebra A", Find<TextBox>(row, "NameBox").Text);
            Assert.Equal(0, Find<ComboBox>(row, "KindBox").SelectedIndex);
            Assert.Equal("10.0.0.20", Find<TextBox>(row, "IpBox").Text);
            Assert.Equal("5084", Find<TextBox>(row, "PortBox").Text);
            Assert.True(Find<ToggleSwitch>(row, "ConnectedSwitch").IsChecked);
            Assert.False(Find<ToggleSwitch>(row, "AutoConnectSwitch").IsChecked);
        }

        [AvaloniaFact]
        public void Antennas_in_use_show_green_when_connected_and_red_when_not()
        {
            Open();

            ReaderSubPart row = ShowReader(ReaderJson(1, "Zebra A", antennas: "[2,1,0,2]"));

            Assert.Equal([("1", Colors.Green), ("2", Colors.Red), ("4", Colors.Green)], Antennas(row));
        }

        [AvaloniaFact]
        public void Antenna_updates_redraw_the_readers_antennas()
        {
            Open();
            ReaderSubPart row = ShowReader(ReaderJson(1, "Zebra A", antennas: "[2,1,0,0]"));

            Receive("""{"command":"reader_antennas","reader_name":"zebra a","antennas":[0,2,2,1]}""");

            Assert.Equal([("2", Colors.Green), ("3", Colors.Green), ("4", Colors.Red)], Antennas(row));
        }

        [AvaloniaFact]
        public void Reader_updates_refresh_the_same_row()
        {
            Open();
            ReaderSubPart row = ShowReader(ReaderJson(1, "Zebra A"));

            ReaderSubPart again = ShowReader(ReaderJson(1, "Zebra Renamed", ip: "10.0.0.21", port: 5085, connected: true));

            Assert.Same(row, again);
            Assert.Equal("Zebra Renamed", Find<TextBox>(row, "NameBox").Text);
            Assert.Equal("10.0.0.21", Find<TextBox>(row, "IpBox").Text);
            Assert.Equal("5085", Find<TextBox>(row, "PortBox").Text);
            Assert.Equal("Zebra Renamed", row.GetReaderName());
        }

        [AvaloniaFact]
        public void Save_reader_sends_the_edited_reader()
        {
            Open();
            ReaderSubPart row = ShowReader(ReaderJson(7, "Zebra A"));
            Find<TextBox>(row, "NameBox").Text = "  Finish Zebra ";
            Find<TextBox>(row, "IpBox").Text = "192.168.1.30";
            Find<TextBox>(row, "PortBox").Text = "5090";
            Find<ToggleSwitch>(row, "AutoConnectSwitch").IsChecked = false;

            Click(Find<Button>(row, "SaveReaderButton"));

            JsonElement sent = PortalSent();
            Assert.Equal("reader_add", sent.GetProperty("command").GetString());
            Assert.Equal(7, sent.GetProperty("id").GetInt64());
            Assert.Equal("Finish Zebra", sent.GetProperty("name").GetString());
            Assert.Equal("ZEBRA", sent.GetProperty("kind").GetString());
            Assert.Equal("192.168.1.30", sent.GetProperty("ip_address").GetString());
            Assert.Equal(5090u, sent.GetProperty("port").GetUInt32());
            Assert.False(sent.GetProperty("auto_connect").GetBoolean());
        }

        [AvaloniaTheory]
        [InlineData("192.168.1", "5084")]
        [InlineData("192.168.1.300", "5084")]
        [InlineData("192.168.1.30", "70000")]
        [InlineData("192.168.1.30", "")]
        public void Save_reader_refuses_a_bad_address_or_port(string ip, string port)
        {
            Open();
            ReaderSubPart row = ShowReader(ReaderJson(7, "Zebra A"));
            Find<TextBox>(row, "IpBox").Text = ip;
            Find<TextBox>(row, "PortBox").Text = port;

            Click(Find<Button>(row, "SaveReaderButton"));

            Assert.True(Pair.NothingSent());
        }

        [AvaloniaFact]
        public void Remove_reader_asks_the_portal_to_remove_it()
        {
            Open();
            ReaderSubPart row = ShowReader(ReaderJson(7, "Zebra A"));

            Click(Find<Button>(row, "RemoveReaderButton"));

            JsonElement sent = PortalSent();
            Assert.Equal("reader_remove", sent.GetProperty("command").GetString());
            Assert.Equal(7, sent.GetProperty("id").GetInt64());
        }

        [AvaloniaTheory]
        [InlineData(false, false, true, "reader_start")]
        [InlineData(true, true, false, "reader_stop")]
        // Connected but not reading: switching it off must stop the reader, not start it.
        [InlineData(true, false, false, "reader_stop")]
        public void Connect_switch_starts_or_stops_the_reader_as_switched(bool connected, bool reading, bool switchedTo, string command)
        {
            Open();
            ReaderSubPart row = ShowReader(ReaderJson(7, "Zebra A", connected: connected, reading: reading));
            ToggleSwitch toggle = Find<ToggleSwitch>(row, "ConnectedSwitch");
            toggle.IsChecked = switchedTo;

            Click(toggle);

            JsonElement sent = PortalSent();
            Assert.Equal(command, sent.GetProperty("command").GetString());
            Assert.Equal(7, sent.GetProperty("id").GetInt64());
        }

        // ---- API rows ----

        [AvaloniaFact]
        public void Api_row_shows_the_portals_api()
        {
            Open();

            ApiPart row = ShowApi();

            Assert.Equal("Results", Find<TextBox>(row, "NameBox").Text);
            Assert.Equal(1, Find<ComboBox>(row, "KindBox").SelectedIndex);
            Assert.Equal("tok", Find<TextBox>(row, "TokenBox").Text);
            Assert.True(Find<TextBox>(row, "UriBox").IsVisible);
            Assert.Equal("https://self.example/", Find<TextBox>(row, "UriBox").Text);
            Assert.Equal("finish", Find<TextBox>(row, "DevNameBox").Text);
            Assert.True(Find<ToggleSwitch>(row, "EnabledSwitch").IsChecked);
            Assert.False(Find<ToggleSwitch>(row, "HeartbeatSwitch").IsChecked);
        }

        [AvaloniaFact]
        public void Hosted_remote_api_hides_its_fixed_address()
        {
            Open();

            ApiPart row = ShowApi(kind: "CHRONOKEEP_REMOTE", uri: "https://remote.chronokeep.com/");

            Assert.False(Find<TextBox>(row, "UriBox").IsVisible);
        }

        [AvaloniaFact]
        public void Switching_to_the_hosted_api_uses_its_address_and_back_restores_the_own()
        {
            Open();
            ApiPart row = ShowApi();
            ComboBox kind = Find<ComboBox>(row, "KindBox");

            kind.SelectedIndex = 0;
            Assert.Equal("https://remote.chronokeep.com/", Find<TextBox>(row, "UriBox").Text);
            Assert.False(Find<TextBox>(row, "UriBox").IsVisible);

            kind.SelectedIndex = 1;
            Assert.Equal("https://self.example/", Find<TextBox>(row, "UriBox").Text);
            Assert.True(Find<TextBox>(row, "UriBox").IsVisible);
        }

        [AvaloniaFact]
        public void Save_api_sends_the_edited_api()
        {
            Open();
            ApiPart row = ShowApi();
            Find<TextBox>(row, "NameBox").Text = " Finish Results ";
            Find<TextBox>(row, "TokenBox").Text = " new-token ";
            Find<TextBox>(row, "UriBox").Text = "https://other.example/";
            Find<TextBox>(row, "DevNameBox").Text = " line ";
            Find<ToggleSwitch>(row, "HeartbeatSwitch").IsChecked = true;
            Find<ToggleSwitch>(row, "EnabledSwitch").IsChecked = false;

            Click(Find<Button>(row, "SaveApiButton"));

            JsonElement sent = PortalSent();
            Assert.Equal("api_save", sent.GetProperty("command").GetString());
            Assert.Equal(4, sent.GetProperty("id").GetInt64());
            Assert.Equal("Finish Results", sent.GetProperty("name").GetString());
            Assert.Equal("CHRONOKEEP_REMOTE_SELF", sent.GetProperty("kind").GetString());
            Assert.Equal("new-token", sent.GetProperty("token").GetString());
            Assert.Equal("https://other.example/", sent.GetProperty("uri").GetString());
            Assert.Equal("line", sent.GetProperty("device_name").GetString());
            Assert.True(sent.GetProperty("heartbeat").GetBoolean());
            Assert.False(sent.GetProperty("enabled").GetBoolean());
        }

        [AvaloniaFact]
        public void Remove_api_asks_the_portal_to_remove_it()
        {
            Open();
            ApiPart row = ShowApi();

            Click(Find<Button>(row, "RemoveApiButton"));

            JsonElement sent = PortalSent();
            Assert.Equal("api_remove", sent.GetProperty("command").GetString());
            Assert.Equal(4, sent.GetProperty("id").GetInt64());
        }

        [AvaloniaFact]
        public void Add_api_sends_a_new_hosted_remote_api()
        {
            Open();

            Click(Find<Button>(window, "AddApiButton"));

            JsonElement sent = PortalSent();
            Assert.Equal("api_save", sent.GetProperty("command").GetString());
            Assert.Equal("New API", sent.GetProperty("name").GetString());
            Assert.Equal("CHRONOKEEP_REMOTE", sent.GetProperty("kind").GetString());
            Assert.False(string.IsNullOrEmpty(sent.GetProperty("token").GetString()));
        }
    }

    /// <summary>The Text Message window turns text alerts on or off for each wave's distances.</summary>
    public class SmsWaveWindowTests : UiTestBase
    {
        private Event theEvent = null!;

        private Distance Dist(string name, int wave, bool sms)
        {
            Distance d = AddDistance(theEvent.Identifier, name);
            d.Wave = wave;
            d.SmsEnabled = sms;
            Db.UpdateDistance(d);
            return d;
        }

        private SmsWaveEnabledWindow Open() => Show(new SmsWaveEnabledWindow(MainWindow, Db));

        private static List<WaveSms> Waves(SmsWaveEnabledWindow window) =>
            [.. Find<ListBox>(window, "WaveList").ItemsSource!.Cast<WaveSms>()];

        private bool Sms(string name) => Db.GetDistances(theEvent.Identifier).Single(d => d.Name == name).SmsEnabled;

        public SmsWaveWindowTests()
        {
            theEvent = AddEvent(makeCurrent: true);
        }

        [AvaloniaFact]
        public void Lists_each_wave_once_in_order()
        {
            Dist("10K", 2, false);
            Dist("5K", 1, true);
            Dist("5K Walk", 1, true);

            SmsWaveEnabledWindow window = Open();

            Assert.Equal(["Wave 1", "Wave 2"], Waves(window).Select(w => w.WaveName));
            Assert.Equal([true, false], Waves(window).Select(w => w.SmsEnabled));
        }

        [AvaloniaFact]
        public void Set_turns_texts_on_or_off_for_every_distance_in_the_wave()
        {
            Dist("5K", 1, true);
            Dist("5K Walk", 1, true);
            Dist("10K", 2, false);
            SmsWaveEnabledWindow window = Open();
            Waves(window)[0].SmsEnabled = false;
            Waves(window)[1].SmsEnabled = true;

            Click(Find<Button>(window, "Set"));

            Assert.False(Sms("5K"));
            Assert.False(Sms("5K Walk"));
            Assert.True(Sms("10K"));
            Assert.False(window.IsVisible);
            Assert.Equal(1, MainWindow.NotifyTimingWorkerCalls);
        }

        [AvaloniaTheory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void A_wave_with_mixed_distances_shows_off_and_set_makes_them_match(bool first, bool second)
        {
            Dist("5K", 1, first);
            Dist("5K Walk", 1, second);
            SmsWaveEnabledWindow window = Open();
            Assert.False(Waves(window)[0].SmsEnabled);

            Waves(window)[0].SmsEnabled = true;
            Click(Find<Button>(window, "Set"));

            Assert.True(Sms("5K"));
            Assert.True(Sms("5K Walk"));
        }

        [AvaloniaFact]
        public void A_wave_with_mixed_distances_can_be_turned_all_off()
        {
            Dist("5K", 1, false);
            Dist("5K Walk", 1, true);
            SmsWaveEnabledWindow window = Open();

            Waves(window)[0].SmsEnabled = false;
            Click(Find<Button>(window, "Set"));

            Assert.False(Sms("5K"));
            Assert.False(Sms("5K Walk"));
        }

        [AvaloniaFact]
        public void Done_changes_nothing_and_leaves_the_timing_worker_alone()
        {
            Dist("5K", 1, true);
            SmsWaveEnabledWindow window = Open();
            Waves(window)[0].SmsEnabled = false;

            Click(Find<Button>(window, "Done"));

            Assert.True(Sms("5K"));
            Assert.Equal(0, MainWindow.NotifyTimingWorkerCalls);
            Assert.Equal(1, MainWindow.WindowFinalizeCalls);
        }

        [AvaloniaFact]
        public void Set_without_changes_leaves_the_timing_worker_alone()
        {
            Dist("5K", 1, true);
            SmsWaveEnabledWindow window = Open();

            Click(Find<Button>(window, "Set"));

            Assert.Equal(0, MainWindow.NotifyTimingWorkerCalls);
        }

        [AvaloniaFact]
        public async Task Timing_page_text_message_button_opens_it_with_the_main_window()
        {
            Dist("5K", 1, true);
            TimingPage page = new(MainWindow, Db);
            Host(page);
            await Settle();

            Click(Find<Button>(page, "ModifySmsButton"));

            Assert.True(AddedWindow<SmsWaveEnabledWindow>().IsVisible);
        }
    }
}
