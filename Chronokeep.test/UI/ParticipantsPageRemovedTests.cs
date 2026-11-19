using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Chronokeep.Objects;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.MainPages;

namespace Chronokeep.Test.UI
{
    /// <summary>People removed in Runsignup stand out on the Participants page.</summary>
    public class ParticipantsPageRemovedTests : UiTestBase
    {
        [AvaloniaFact]
        public async Task Removed_participants_rows_are_tinted_and_say_why()
        {
            (Event e, Distance d) = SeedEvent();
            Participant removed = AddParticipant(e.Identifier, d, "200", first: "Gone");
            removed.EventSpecific.Removed = Constants.Registration.REMOVED_PREFIX + Constants.Registration.TRANSFERRED;
            Db.UpdateParticipant(removed);

            ParticipantsPage page = new(MainWindow, Db);
            Host(page);
            DataGrid list = Find<DataGrid>(page, "ParticipantsList");
            await WaitUntil(() => list.ItemsSource?.Cast<object>().Count() == 2, because: "the participants load");
            Pump();

            DataGridRow RowFor(string first) => list.GetVisualDescendants().OfType<DataGridRow>()
                .Single(r => r.DataContext is Participant p && p.FirstName == first);
            DataGridRow gone = RowFor("Gone");
            DataGridRow running = RowFor("Jane");

            Assert.Contains("removed", gone.Classes);
            Assert.Equal(Color.Parse("#40E53935"), Assert.IsAssignableFrom<ISolidColorBrush>(gone.Background).Color);
            Assert.Contains("Transferred", ToolTip.GetTip(gone) as string);

            Assert.DoesNotContain("removed", running.Classes);
            Assert.NotEqual(Color.Parse("#40E53935"), (running.Background as ISolidColorBrush)?.Color);
            Assert.Null(ToolTip.GetTip(running));
        }

        [AvaloniaFact]
        public async Task Conflicting_entries_are_tinted()
        {
            (Event e, Distance d) = SeedEvent();
            // Two different people with the same bib conflict with each other; the seeded Jane (bib 100) doesn't.
            AddParticipant(e.Identifier, d, "300", first: "Ann", last: "One");
            AddParticipant(e.Identifier, d, "300", first: "Bea", last: "Two", birthday: "03/04/1985");

            ParticipantsPage page = new(MainWindow, Db);
            Host(page);
            DataGrid list = Find<DataGrid>(page, "ParticipantsList");
            await WaitUntil(() => list.ItemsSource?.Cast<object>().Count() == 3, because: "the participants load");
            Pump();

            DataGridRow RowFor(string first) => list.GetVisualDescendants().OfType<DataGridRow>()
                .Single(r => r.DataContext is Participant p && p.FirstName == first);
            foreach (string first in new[] { "Ann", "Bea" })
            {
                DataGridRow row = RowFor(first);
                Assert.Contains("conflict", row.Classes);
                Assert.Equal(Color.Parse("#40FFB300"), Assert.IsAssignableFrom<ISolidColorBrush>(row.Background).Color);
                Assert.Contains("Conflicts button", ToolTip.GetTip(row) as string);
            }
            DataGridRow jane = RowFor("Jane");
            Assert.DoesNotContain("conflict", jane.Classes);
            Assert.Null(ToolTip.GetTip(jane));
            Assert.True(Find<Button>(page, "ConflictsBtn").IsVisible);
        }
    }
}
