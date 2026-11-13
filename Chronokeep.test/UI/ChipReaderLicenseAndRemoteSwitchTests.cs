using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Objects;
using Chronokeep.Test.Network;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.API.Windows;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.UhfRfidReader;
using Chronokeep.UI.Util;
using System.Net;
using static Chronokeep.Constants.ApiConstants;
using static Chronokeep.Timing.Remote.RemoteReadsController;

namespace Chronokeep.Test.UI
{
    /// <summary>The USB tag tester: reads listed as they come in, and the big-text window showing whose chip it is.</summary>
    public class ChipReaderWindowTests : UiTestBase
    {
        private Event theEvent = null!;

        public ChipReaderWindowTests()
        {
            AllowDialogs();
            theEvent = AddEvent("Spring Fling", makeCurrent: true);
            Distance d = AddDistance(theEvent.Identifier, "5K");
            AddParticipant(theEvent.Identifier, d, "42", first: "Ada", last: "Lovelace");
            Db.AddBibChipAssociation(theEvent.Identifier, [new BibChipAssociation { EventId = theEvent.Identifier, Bib = "42", Chip = "1001" }]);
            Db.SetAppSetting(Constants.Settings.DEFAULT_CHIP_TYPE, Constants.Settings.CHIP_TYPE_DEC);
        }

        private ChipReaderWindow Open() => Show(ChipReaderWindow.NewWindow(MainWindow, Db));

        private static List<RfidInfo> Reads(ChipReaderWindow window) =>
            [.. Find<DataGrid>(window, "ChipNumbers").ItemsSource!.Cast<RfidInfo>()];

        [AvaloniaFact]
        public void Shows_the_current_event()
        {
            ChipReaderWindow window = Open();

            Assert.Equal("Spring Fling", Find<TextBlock>(window, "EventName").Text);
            Assert.Equal("Connect", ContentText(Find<Button>(window, "ConnectBtn")));
        }

        [AvaloniaFact]
        public async Task Connect_without_a_port_says_so()
        {
            ChipReaderWindow window = Open();
            Find<ComboBox>(window, "SerialPortCb").SelectedIndex = -1;

            Click(Find<Button>(window, "ConnectBtn"));

            Answer(await WaitForDialog("No serial port selected"), false);
            Assert.Equal("Connect", ContentText(Find<Button>(window, "ConnectBtn")));
        }

        [AvaloniaFact]
        public void Reads_are_listed_and_numbered()
        {
            ChipReaderWindow window = Open();

            window.AddRfidItem(new RfidInfo { DecNumber = 1001, HexNumber = "3E9" });
            window.AddRfidItem(new RfidInfo { DecNumber = 1002, HexNumber = "3EA" });

            Assert.Equal([(1, 1001L), (2, 1002L)], Reads(window).Select(r => (r.ReadNumber, r.DecNumber)));
        }

        [AvaloniaFact]
        public void Read_numbers_start_over_in_a_new_window()
        {
            ChipReaderWindow first = Open();
            first.AddRfidItem(new RfidInfo { DecNumber = 1001 });
            first.AddRfidItem(new RfidInfo { DecNumber = 1002 });
            first.Close();
            Pump();

            ChipReaderWindow second = Open();
            second.AddRfidItem(new RfidInfo { DecNumber = 1003 });

            Assert.Equal(1, Reads(second).Single().ReadNumber);
        }

        [AvaloniaFact]
        public async Task Info_window_shows_whose_chip_was_read()
        {
            ChipReaderWindow window = Open();
            Click(Find<Button>(window, "BeautyBtn"));
            ChipPersonWindow info = window.PersonWindow!;
            Assert.Equal("Close Info Window", ContentText(Find<Button>(window, "BeautyBtn")));

            window.AddRfidItem(new RfidInfo { DecNumber = 1001, HexNumber = "3E9" });
            await WaitUntil(() => !string.IsNullOrEmpty(Find<TextBlock>(info, "Chip").Text), because: "the runner to show");

            Assert.Equal("Bib: 42", Find<TextBlock>(info, "Bib").Text);
            Assert.Equal("Chip: 1001", Find<TextBlock>(info, "Chip").Text);
            Assert.Equal("Ada Lovelace", Find<TextBlock>(info, "PersonName").Text);
            Assert.Equal("5K", Find<TextBlock>(info, "Distance").Text);
            info.Close();
        }

        [AvaloniaFact]
        public async Task Info_window_says_when_a_chip_is_nobodys()
        {
            ChipReaderWindow window = Open();
            Click(Find<Button>(window, "BeautyBtn"));
            ChipPersonWindow info = window.PersonWindow!;

            window.AddRfidItem(new RfidInfo { DecNumber = 9999, HexNumber = "270F" });
            await WaitUntil(() => Find<TextBlock>(info, "Unknown").IsVisible, because: "the unknown message");

            Assert.Equal("Information not found.", Find<TextBlock>(info, "Unknown").Text);
            Assert.False(Find<StackPanel>(info, "InfoHolder").IsVisible);
            info.Close();
        }

        [AvaloniaFact]
        public async Task Hex_events_look_chips_up_by_their_hex_number()
        {
            Db.SetAppSetting(Constants.Settings.DEFAULT_CHIP_TYPE, Constants.Settings.CHIP_TYPE_HEX);
            Db.AddBibChipAssociation(theEvent.Identifier, [new BibChipAssociation { EventId = theEvent.Identifier, Bib = "42", Chip = "3E9" }]);
            ChipReaderWindow window = Open();
            Click(Find<Button>(window, "BeautyBtn"));
            ChipPersonWindow info = window.PersonWindow!;

            window.AddRfidItem(new RfidInfo { DecNumber = 1001, HexNumber = "3E9" });
            await WaitUntil(() => !string.IsNullOrEmpty(Find<TextBlock>(info, "Chip").Text), because: "the runner to show");

            Assert.Equal("Chip: 3E9", Find<TextBlock>(info, "Chip").Text);
            info.Close();
        }

        [AvaloniaFact]
        public void Closing_the_info_window_resets_its_button()
        {
            ChipReaderWindow window = Open();
            Click(Find<Button>(window, "BeautyBtn"));

            window.PersonWindow!.Close();
            Pump();

            Assert.Null(window.PersonWindow);
            Assert.Equal("Show Info Window", ContentText(Find<Button>(window, "BeautyBtn")));
        }

        [AvaloniaFact]
        public void Closing_the_tester_closes_its_info_window()
        {
            ChipReaderWindow window = Open();
            Click(Find<Button>(window, "BeautyBtn"));
            ChipPersonWindow info = window.PersonWindow!;

            window.Close();
            Pump();

            Assert.False(info.IsVisible);
            Assert.Equal(1, MainWindow.WindowFinalizeCalls);
        }

        [AvaloniaFact]
        public async Task Dashboard_tag_tester_opens_it_with_the_main_window()
        {
            DashboardPage page = new(MainWindow, Db);
            Host(page);
            await Settle();

            Click(Find<Button>(page, "TagTesterButton"));

            Assert.True(AddedWindow<ChipReaderWindow>().IsVisible);
        }
    }

    /// <summary>The license window opened from the About page.</summary>
    public class LicenseWindowTests : UiTestBase
    {
        [AvaloniaFact]
        public void Shows_the_agpl_and_closes()
        {
            LicenseWindow window = Show(new LicenseWindow());

            Assert.Contains("GNU AFFERO GENERAL PUBLIC LICENSE", Find<TextBlock>(window, "MessageBox").Text);
            Click(Find<Button>(window, "RightButton"));

            Assert.False(window.IsVisible);
        }

        [AvaloniaFact]
        public void About_page_license_button_opens_it_with_the_main_window()
        {
            AboutPage page = new(MainWindow, Db);
            Host(page);

            Click(Find<Button>(page, "License"));

            Assert.True(AddedWindow<LicenseWindow>().IsVisible);
        }
    }

    /// <summary>The Timing page's switch for downloading reads from remote readers, and its readers button.</summary>
    public class RemoteReadsSwitchTests : UiTestBase
    {
        private readonly FakeApiServer server = new();

        public override void Dispose()
        {
            base.Dispose();
            server.Dispose();
        }

        private void AddRemoteApi() => Db.AddApi(new ApiObject(0, CHRONOKEEP_REMOTE_SELF, server.Url, "Remote", "tok", ""));

        private async Task<TimingPage> OpenPage()
        {
            TimingPage page = new(MainWindow, Db);
            Host(page);
            await Settle();
            return page;
        }

        [AvaloniaFact]
        public async Task Switch_is_hidden_without_a_remote_api()
        {
            AddEvent(makeCurrent: true);

            TimingPage page = await OpenPage();

            Assert.False(Find<ToggleSwitch>(page, "RemoteControllerSwitch").IsVisible);
            Find<Expander>(page, "ReaderExpander").IsExpanded = true;
            Pump();
            Assert.False(Find<Button>(page, "RemoteReadersButton").IsVisible);
        }

        [AvaloniaFact]
        public async Task Readers_button_shows_while_the_readers_are_open()
        {
            AddEvent(makeCurrent: true);
            AddRemoteApi();
            TimingPage page = await OpenPage();
            Expander readers = Find<Expander>(page, "ReaderExpander");

            Assert.True(Find<ToggleSwitch>(page, "RemoteControllerSwitch").IsVisible);
            readers.IsExpanded = false;
            Pump();
            Assert.False(Find<Button>(page, "RemoteReadersButton").IsVisible);
            readers.IsExpanded = true;
            Pump();
            Assert.True(Find<Button>(page, "RemoteReadersButton").IsVisible);
        }

        [AvaloniaFact]
        public async Task Switching_on_starts_downloads_and_waits_for_them()
        {
            AddEvent(makeCurrent: true);
            AddRemoteApi();
            TimingPage page = await OpenPage();
            ToggleSwitch toggle = Find<ToggleSwitch>(page, "RemoteControllerSwitch");
            int starts = MainWindow.StartRemoteCalls;

            toggle.IsChecked = true;
            Pump();

            Assert.Equal(starts + 1, MainWindow.StartRemoteCalls);
            Assert.False(toggle.IsEnabled);

            MainWindow.RemoteState = RemoteStatus.RUNNING;
            MainWindow.RemoteErrorCount = 2;
            page.UpdateView();
            Assert.True(toggle.IsEnabled);
            Assert.True(toggle.IsChecked);
            Assert.Equal("2", Find<TextBlock>(page, "RemoteErrorsBlock").Text);
        }

        [AvaloniaFact]
        public async Task Switching_off_stops_downloads()
        {
            AddEvent(makeCurrent: true);
            AddRemoteApi();
            MainWindow.RemoteState = RemoteStatus.RUNNING;
            TimingPage page = await OpenPage();
            ToggleSwitch toggle = Find<ToggleSwitch>(page, "RemoteControllerSwitch");
            Assert.True(toggle.IsChecked);
            int stops = MainWindow.StopRemoteCalls;

            toggle.IsChecked = false;
            Pump();

            Assert.Equal(stops + 1, MainWindow.StopRemoteCalls);
            MainWindow.RemoteState = RemoteStatus.STOPPED;
            page.UpdateView();
            Assert.True(toggle.IsEnabled);
            Assert.Equal("", Find<TextBlock>(page, "RemoteErrorsBlock").Text);
        }

        [AvaloniaFact]
        public async Task Readers_button_opens_remote_readers_with_the_main_window()
        {
            AddEvent(makeCurrent: true);
            AddRemoteApi();
            server.Responder = _ => (HttpStatusCode.OK, """{"readers":[]}""");
            TimingPage page = await OpenPage();
            Find<Expander>(page, "ReaderExpander").IsExpanded = true;
            Pump();

            Click(Find<Button>(page, "RemoteReadersButton"));

            RemoteReadersWindow window = AddedWindow<RemoteReadersWindow>();
            Assert.True(window.IsVisible);
            await WaitUntil(() => !Find<StackPanel>(window, "LoadingPanel").IsVisible, because: "the readers to load");
        }
    }

    /// <summary>The Timing page's Create HTML results page.</summary>
    public class ResultsHtmlTests : UiTestBase
    {
        [AvaloniaFact]
        public void Results_page_lists_finishers_by_distance()
        {
            Event e = AddEvent("Spring Fling", makeCurrent: true);
            Distance fiveK = AddDistance(e.Identifier, "5K");
            Participant ada = AddParticipant(e.Identifier, fiveK, "42", first: "Ada", last: "Lovelace");
            Participant ben = AddParticipant(e.Identifier, fiveK, "43", first: "Ben", last: "Bravo");
            e = Db.GetCurrentEvent()!;
            Db.AddTimingResults([
                new TimeResult(e.Identifier, Constants.Timing.TIMERESULT_DUMMYREAD, ada.EventSpecific.Identifier,
                    Constants.Timing.LOCATION_FINISH, Constants.Timing.SEGMENT_FINISH, 1, 1500, 0, "", 1500, 0,
                    DateTime.Now, "42", Constants.Timing.TIMERESULT_STATUS_NONE, ""),
            ]);

            string html = TimingPage.ResultsHtml(Db, e);

            Assert.Contains("Spring Fling", html);
            Assert.Contains("Lovelace", html);
            Assert.Contains("5K", html);
            Assert.DoesNotContain("Bravo", html);
        }
    }
}
