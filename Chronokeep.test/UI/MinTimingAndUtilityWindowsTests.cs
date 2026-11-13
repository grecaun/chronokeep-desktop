using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Helpers;
using Chronokeep.IO.HtmlTemplates;
using Chronokeep.Objects;
using Chronokeep.Objects.Changelog;
using Chronokeep.Objects.ChronokeepPortal;
using Chronokeep.Objects.ChronokeepRemote;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.Timing.Interfaces;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.MainPages.Timing;
using Chronokeep.UI.Parts;
using Chronokeep.UI.Timing.Notifications;
using Chronokeep.UI.Timing.Windows;
using Chronokeep.UI.Util;
using System.Reflection;
using Readers = Chronokeep.Constants.Readers;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.UI
{
    /// <summary>The web printout margins and scale, opened from the Timing page's Web Settings button.</summary>
    public class PrintSettingsWindowTests : UiTestBase
    {
        private readonly (int Top, int Left, double Scale) saved =
            (HtmlPrintableTemplate.TopMargin, HtmlPrintableTemplate.LeftMargin, HtmlPrintableTemplate.Scale);

        public PrintSettingsWindowTests()
        {
            HtmlPrintableTemplate.TopMargin = 5;
            HtmlPrintableTemplate.LeftMargin = 7;
            HtmlPrintableTemplate.Scale = 1.0;
        }

        public override void Dispose()
        {
            (HtmlPrintableTemplate.TopMargin, HtmlPrintableTemplate.LeftMargin, HtmlPrintableTemplate.Scale) = saved;
            base.Dispose();
        }

        private PrintSettingsWindow Open() => Show(new PrintSettingsWindow());

        [AvaloniaFact]
        public void Shows_the_current_settings()
        {
            PrintSettingsWindow window = Open();

            Assert.Equal("5", Find<TextBox>(window, "TopMarginBox").Text);
            Assert.Equal("7", Find<TextBox>(window, "LeftMarginBox").Text);
            Assert.Equal(1.0.ToString(), Find<TextBox>(window, "ScalingBox").Text);
        }

        [AvaloniaFact]
        public void Save_keeps_the_typed_settings()
        {
            PrintSettingsWindow window = Open();
            Find<TextBox>(window, "TopMarginBox").Text = "20";
            Find<TextBox>(window, "LeftMarginBox").Text = "0";
            Find<TextBox>(window, "ScalingBox").Text = 0.85.ToString();

            Click(Find<Button>(window, "SaveButton"));

            Assert.Equal(20, HtmlPrintableTemplate.TopMargin);
            Assert.Equal(0, HtmlPrintableTemplate.LeftMargin);
            Assert.Equal(0.85, HtmlPrintableTemplate.Scale);
        }

        [AvaloniaTheory]
        [InlineData("abc", "x", "big")]
        [InlineData("-10", "-1", "0")]
        [InlineData("", "", "-0.5")]
        public void Save_ignores_values_that_cant_be_used(string top, string left, string scale)
        {
            PrintSettingsWindow window = Open();
            Find<TextBox>(window, "TopMarginBox").Text = top;
            Find<TextBox>(window, "LeftMarginBox").Text = left;
            Find<TextBox>(window, "ScalingBox").Text = scale;

            Click(Find<Button>(window, "SaveButton"));

            Assert.Equal(5, HtmlPrintableTemplate.TopMargin);
            Assert.Equal(7, HtmlPrintableTemplate.LeftMargin);
            Assert.Equal(1.0, HtmlPrintableTemplate.Scale);
        }

        [AvaloniaFact]
        public void Close_closes_without_saving()
        {
            PrintSettingsWindow window = Open();
            Find<TextBox>(window, "TopMarginBox").Text = "30";

            Click(Find<Button>(window, "DoneButton"));

            Assert.False(window.IsVisible);
            Assert.Equal(5, HtmlPrintableTemplate.TopMargin);
        }

        [AvaloniaFact]
        public async Task Timing_page_web_settings_opens_it_with_the_main_window()
        {
            AddEvent(makeCurrent: true);
            TimingPage page = new(MainWindow, Db);
            Host(page);
            await Settle();

            Click(Find<Button>(page, "WebSettingsButton"));

            Assert.True(AddedWindow<PrintSettingsWindow>().IsVisible);
        }
    }

    /// <summary>The list of notifications readers have sent (power, UPS, and the like).</summary>
    public class ReaderNotificationWindowTests : UiTestBase
    {
        public ReaderNotificationWindowTests() => Globals.ClearReaderMessages();

        public override void Dispose()
        {
            Globals.ClearReaderMessages();
            base.Dispose();
        }

        private static ReaderMessage Message(string reader, string type, string when, ReaderMessage.SeverityLevel severity = ReaderMessage.SeverityLevel.Low) =>
            new()
            {
                SystemName = reader,
                Address = "10.0.0.5",
                Severity = severity,
                Message = new RemoteNotification { Type = type, When = when },
            };

        private ReaderNotificationWindow Open() => Show(ReaderNotificationWindow.NewWindow(MainWindow));

        private static List<ReaderMessage> Listed(ReaderNotificationWindow window) =>
            [.. Find<ListBox>(window, "NotificationsList").ItemsSource!.Cast<ReaderMessage>()];

        [AvaloniaFact]
        public void Lists_notifications_oldest_first()
        {
            Globals.AddReaderMessage(Message("Finish", PortalNotification.UPS_ON_BATTERY, "2026-06-15 09:00:00", ReaderMessage.SeverityLevel.High));
            Globals.AddReaderMessage(Message("Start", PortalNotification.UPS_CONNECTED, "2026-06-15 08:00:00"));

            ReaderNotificationWindow window = Open();

            Assert.Equal(["Start", "Finish"], Listed(window).Select(m => m.Who));
            Assert.Equal("High", Listed(window)[1].SeverityString);
        }

        [AvaloniaFact]
        public void Opening_marks_every_notification_as_seen()
        {
            Globals.AddReaderMessage(Message("Finish", PortalNotification.UPS_ON_BATTERY, "2026-06-15 09:00:00"));

            Open();

            Assert.All(Globals.GetReaderMessages(), m => Assert.True(m.Notified));
        }

        [AvaloniaFact]
        public void Shows_an_empty_list_without_notifications()
        {
            Assert.Empty(Listed(Open()));
        }

        [AvaloniaFact]
        public void Closing_tells_the_main_window()
        {
            ReaderNotificationWindow window = Open();

            Click(Find<Button>(window, "CancelButton"));

            Assert.False(window.IsVisible);
            Assert.Equal(1, MainWindow.WindowFinalizeCalls);
        }

        [AvaloniaFact]
        public async Task Timing_page_notifications_button_opens_it_with_the_main_window()
        {
            AddEvent(makeCurrent: true);
            TimingPage page = new(MainWindow, Db);
            Host(page);
            await Settle();

            Click(Find<Button>(page, "ReaderMessageButton"));

            Assert.True(AddedWindow<ReaderNotificationWindow>().IsVisible);
        }
    }

    /// <summary>The About page: version, help link, and the changelog it opens.</summary>
    public class AboutPageTests : UiTestBase
    {
        private AboutPage Open()
        {
            AboutPage page = new(MainWindow, Db);
            Host(page);
            return page;
        }

        private static string BuiltVersion()
        {
            using Stream stream = typeof(AboutPage).Assembly.GetManifestResourceStream("Chronokeep.version.txt")!;
            return new StreamReader(stream).ReadToEnd().Trim();
        }

        [AvaloniaFact]
        public void Shows_the_built_version()
        {
            AboutPage page = Open();

            Assert.False(string.IsNullOrWhiteSpace(BuiltVersion()));
            Assert.Equal(BuiltVersion(), Find<TextBlock>(page, "VersionLabel").Text);
        }

        [AvaloniaFact]
        public void Help_link_points_at_the_bundled_help()
        {
            AboutPage page = Open();

            Uri help = Find<HyperlinkButton>(page, "HelpDocsButton").NavigateUri!;
            Assert.Equal(Path.Combine(AppContext.BaseDirectory, "help", "index.html"), help.LocalPath);
        }

        [AvaloniaFact]
        public void Changelog_opens_with_the_five_newest_versions_and_the_newest_open()
        {
            AboutPage page = Open();

            Click(Find<Button>(page, "Changelog"));
            ChangeLogWindow window = AddedWindow<ChangeLogWindow>();

            List<Entry> entries = [.. Find<ListBox>(window, "LogList").ItemsSource!.Cast<Entry>()];
            Assert.Equal(5, entries.Count);
            Assert.Equal("2.0.0", entries[0].Version.TrimStart('v'));
            Assert.Equal("1.9.10", entries[1].Version.TrimStart('v'));
            Assert.True(entries[0].IsExpanded);
            Assert.All(entries.Skip(1), e => Assert.False(e.IsExpanded));
        }

        [AvaloniaTheory]
        [InlineData(true, Constants.Settings.SETTING_TRUE)]
        [InlineData(false, Constants.Settings.SETTING_FALSE)]
        public void Changelog_remembers_whether_to_show_after_updates(bool show, string stored)
        {
            AboutPage page = Open();
            Click(Find<Button>(page, "Changelog"));
            ChangeLogWindow window = AddedWindow<ChangeLogWindow>();

            Find<ToggleSwitch>(window, "AutoChangelogToggleSwitch").IsChecked = show;
            Click(Find<Button>(window, "CancelButton"));

            Assert.False(window.IsVisible);
            Assert.Equal(stored, Db.GetAppSetting(Constants.Settings.AUTO_SHOW_CHANGELOG)!.Value);
            Assert.Equal(1, MainWindow.WindowFinalizeCalls);
        }
    }

    /// <summary>The safe mode timing page: reader rows over the raw reads list.</summary>
    public class MinTimingPageTests : ReaderUiTestBase
    {
        private MinTimingPage OpenPage()
        {
            MinTimingPage page = new(MainWindow, Db);
            Host(page);
            return page;
        }

        private static List<ReaderPart> Rows(MinTimingPage page) =>
            [.. Find<ListBox>(page, "ReadersBox").Items.Cast<ReaderPart>()];

        private static List<string> LocationNames(ReaderPart row) =>
            [.. Find<ComboBox>(row, "ReaderLocation").Items.Cast<ComboBoxItem>().Select(i => i.Content!.ToString()!)];

        [AvaloniaFact]
        public void Without_an_event_there_are_no_readers_or_reads()
        {
            Db.SetCurrentEvent(-1);

            MinTimingPage page = OpenPage();

            Assert.Empty(Rows(page));
            Assert.Null(Find<ContentControl>(page, "TimingFrame").Content);
        }

        [AvaloniaFact]
        public void Opens_with_four_reader_rows_over_the_raw_reads()
        {
            MinTimingPage page = OpenPage();

            Assert.Equal(4, Rows(page).Count);
            Assert.IsType<TimingRawReadsPage>(Find<ContentControl>(page, "TimingFrame").Content);
        }

        [AvaloniaFact]
        public void Rows_offer_the_events_locations()
        {
            Event theEvent = Db.GetCurrentEvent()!;
            theEvent.CommonStartFinish = false;
            Db.UpdateEvent(theEvent);
            Db.AddTimingLocation(new TimingLocation(theEvent.Identifier, "Mile 3"));

            MinTimingPage page = OpenPage();

            Assert.Equal(["Start", "Finish", "Announcer", "Mile 3"], LocationNames(Rows(page)[0]));
        }

        [AvaloniaFact]
        public void New_locations_reach_the_rows_on_update()
        {
            MinTimingPage page = OpenPage();
            Event theEvent = Db.GetCurrentEvent()!;

            Db.AddTimingLocation(new TimingLocation(theEvent.Identifier, "Mile 3"));
            page.UpdateView();

            Assert.Contains("Mile 3", LocationNames(Rows(page)[0]));
        }

        [AvaloniaFact]
        public void Connected_readers_are_listed_first()
        {
            TimingSystem system = Ultra().System;
            MainWindow.ConnectedSystems.Add(system);

            MinTimingPage page = OpenPage();

            Assert.Same(system, Rows(page)[0].Reader);
        }

        [AvaloniaFact]
        public void Connecting_every_row_adds_another()
        {
            MinTimingPage page = OpenPage();
            List<ReaderPart> rows = Rows(page);

            for (int i = 0; i < rows.Count; i++)
            {
                Find<TextBox>(rows[i], "ReaderIp").Text = $"10.0.0.{i + 1}";
                Click(Find<Button>(rows[i], "ConnectButton"));
            }

            Assert.Equal(4, MainWindow.ConnectRequests.Count);
            Assert.Equal(5, Rows(page).Count);
        }

        [AvaloniaFact]
        public void Remove_deletes_the_saved_reader_and_its_row()
        {
            Db.AddTimingSystem(new TimingSystem("192.168.1.50", Readers.SYSTEM_RFID));
            TimingSystem stored = Assert.Single(Db.GetTimingSystems());
            MainWindow.ConnectedSystems.Add(stored);
            MinTimingPage page = OpenPage();
            ReaderPart row = Rows(page).Single(r => r.Reader == stored);

            Click(Find<Button>(row, "RemoveButton"));

            Assert.Empty(Db.GetTimingSystems());
            Assert.DoesNotContain(row, Rows(page));
            Assert.Equal(4, Rows(page).Count);
        }

        [AvaloniaFact]
        public void Set_all_only_sets_connected_readers()
        {
            TimingSystem connected = Ultra().System;
            using Timing.SocketPair otherPair = new();
            TimingSystem saved = new("10.0.0.9", Readers.SYSTEM_RFID) { Status = SYSTEM_STATUS.DISCONNECTED };
            RfidUltraInterface savedUltra = new(Db, TimingConstants.LOCATION_FINISH, MainWindow);
            savedUltra.SetMainSocket(otherPair.Client);
            saved.SystemInterface = savedUltra;
            MainWindow.ConnectedSystems.AddRange([connected, saved]);
            MinTimingPage page = OpenPage();

            page.SetAllTimingSystemsToTime(new DateTime(2026, 6, 15, 8, 30, 5), false);

            Assert.Equal("t 08:30:05 15-06-2026\n", Pair.Line());
            Assert.True(otherPair.NothingSent());
        }

        [AvaloniaFact]
        public void Set_all_keeps_going_past_a_reader_that_fails()
        {
            TimingSystem broken = new("10.0.0.8", Readers.SYSTEM_RFID) { Status = SYSTEM_STATUS.CONNECTED };
            // A reader whose connection has gone away throws when sent anything.
            broken.SystemInterface = new RfidUltraInterface(Db, TimingConstants.LOCATION_FINISH, MainWindow);
            TimingSystem working = Ultra().System;
            MainWindow.ConnectedSystems.AddRange([broken, working]);
            MinTimingPage page = OpenPage();

            page.SetAllTimingSystemsToTime(new DateTime(2026, 6, 15, 8, 30, 5), false);

            Assert.Equal("t 08:30:05 15-06-2026\n", Pair.Line());
        }

        [AvaloniaFact]
        public void Clock_opens_set_time_and_reader_answers_show_in_it()
        {
            ShowMainWindow();
            TimingSystem system = Ultra().System;
            MainWindow.ConnectedSystems.Add(system);
            MinTimingPage page = OpenPage();

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
            MinTimingPage page = OpenPage();
            Click(Find<Button>(Rows(page)[0], "RewindButton"));
            RewindWindow window = MainWindow.OwnedWindows.OfType<RewindWindow>().Single();

            system.Status = SYSTEM_STATUS.DISCONNECTED;
            page.UpdateView();
            Pump();

            Assert.False(window.IsVisible);
        }

        [AvaloniaFact]
        public void Losing_the_event_hides_the_reads()
        {
            MinTimingPage page = OpenPage();

            Db.SetCurrentEvent(-1);
            page.UpdateView();

            Assert.False(Find<ContentControl>(page, "TimingFrame").IsVisible);
        }
    }
}
