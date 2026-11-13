using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Objects;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.API.Windows;
using Chronokeep.UI.ChipAssignment;
using Chronokeep.UI.EventWindows;
using Chronokeep.UI.Export;
using Chronokeep.UI.Participants;
using Chronokeep.UI.Timing.Notifications;
using Chronokeep.UI.Timing.Windows;
using Chronokeep.UI.Util;

namespace Chronokeep.Test.UI
{
    /// <summary>
    /// Every window that can be built from a database and the main window opens, lays out, and closes.
    /// Windows that need live hardware, a reader connection, an import file, or a GitHub release
    /// (reader settings, rewind/set time, import, download, chip reader) are left to targeted tests.
    /// MainWindow and MinWindow are not opened: they claim the single-instance lock and the real database.
    /// LicenseWindow is not opened either: laying out the full AGPL text in a TextBox does not finish
    /// under the headless renderer.
    /// </summary>
    public class WindowSmokeTests : UiTestBase
    {
        public static TheoryData<string> Windows =>
        [
            "ApiLink", "EditApiLink", "ChipTool", "ChangeEvent", "NewEvent",
            "ChangeMultiParticipant", "ModifyParticipantNew", "ModifyParticipantExisting",
            "ParticipantConflicts", "SmsWaveEnabled", "ManualEntry", "Wave", "ChangeLog",
            "PrintSettings", "ReaderNotification", "ExportResults", "ExportDistanceResults",
        ];

        private Window CreateWindow(string name)
        {
            List<Participant> participants = Db.GetParticipants(Db.GetCurrentEvent()!.Identifier);
            return name switch
            {
                "ApiLink" => ApiWindow.NewWindow(MainWindow, Db),
                "EditApiLink" => EditApiWindow.NewWindow(MainWindow, Db),
                "ChipTool" => ChipTool.NewWindow(MainWindow, Db),
                "ChangeEvent" => ChangeEventWindow.NewWindow(MainWindow, Db),
                "NewEvent" => NewEventWindow.NewWindow(MainWindow, Db),
                "ChangeMultiParticipant" => new ChangeMultiParticipantWindow(MainWindow, Db, participants),
                "ModifyParticipantNew" => ModifyParticipantWindow.NewWindow(MainWindow, Db),
                "ModifyParticipantExisting" => ModifyParticipantWindow.NewWindow(MainWindow, Db, participants[0]),
                "ParticipantConflicts" => ParticipantConflicts.NewWindow(MainWindow, participants),
                "SmsWaveEnabled" => new SmsWaveEnabledWindow(MainWindow, Db),
                "ManualEntry" => ManualEntryWindow.NewWindow(MainWindow, Db),
                "Wave" => new WaveWindow(MainWindow, Db),
                "ChangeLog" => ChangeLogWindow.NewWindow(MainWindow, Db),
                "PrintSettings" => new PrintSettingsWindow(),
                "ReaderNotification" => ReaderNotificationWindow.NewWindow(MainWindow),
                "ExportResults" => new ExportResults(MainWindow, Db),
                "ExportDistanceResults" => new ExportDistanceResults(MainWindow, Db),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
            };
        }

        [AvaloniaTheory]
        [MemberData(nameof(Windows))]
        public async Task Window_opens_lays_out_and_closes(string name)
        {
            SeedEvent();
            Window window = Show(CreateWindow(name));
            await Settle();

            Assert.True(window.IsMeasureValid, $"{name} window never completed layout.");
            window.Close();
            Pump();
            Assert.False(window.IsVisible);
        }
    }
}
