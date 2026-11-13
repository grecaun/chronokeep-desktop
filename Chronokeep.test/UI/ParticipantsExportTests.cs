using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ClosedXML.Excel;
using Chronokeep.Objects;
using Chronokeep.Test.Network;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.MainPages;
using System.Net;
using System.Text.Json;
using static Chronokeep.Constants.ApiConstants;

namespace Chronokeep.Test.UI
{
    /// <summary>The Participants page's Export file and its Delete Uploaded button.</summary>
    public class ParticipantsExportTests : UiTestBase
    {
        private readonly FakeApiServer server = new();
        private readonly string folder = Directory.CreateTempSubdirectory("ck-entrants-").FullName;
        private Event theEvent = null!;

        public override void Dispose()
        {
            base.Dispose();
            server.Dispose();
            try { Directory.Delete(folder, true); } catch { }
        }

        private void Seed()
        {
            theEvent = AddEvent(makeCurrent: true);
            Distance fiveK = AddDistance(theEvent.Identifier, "5K");
            Distance tenK = AddDistance(theEvent.Identifier, "10K");
            Db.AddParticipant(new Participant(
                "Ann", "Alpha", "123 Main St", "Springfield", "OR", "97477", "1990/02/01",
                new EventSpecific(theEvent.Identifier, fiveK.Identifier, fiveK.Name, "1", 0,
                    "Says \"hi\",\nthen leaves", "owes", "other", false, false, "M", ""),
                "ann@example.com", "555-0100", "555-0101", "Parent", "USA", "Apt 1",
                "F", "EC Name", "555-0199"));
            AddParticipant(theEvent.Identifier, tenK, "22", first: "Cat", last: "Charlie", birthday: "1985/11/30", gender: "X");
            theEvent = Db.GetCurrentEvent()!;
        }

        private static List<string[]> ReadCsv(string path) =>
            [.. File.ReadAllLines(path).Select(line => line[1..^1].Split("\",\""))];

        // ---- Export ----

        [AvaloniaFact]
        public void Csv_export_has_a_header_and_a_row_per_participant()
        {
            Seed();
            string path = Path.Combine(folder, "entrants.csv");

            ParticipantsPage.WriteParticipants(Db, theEvent, path);

            List<string[]> rows = ReadCsv(path);
            Assert.Equal(3, rows.Count);
            string[] header = rows[0];
            Assert.Equal(27, header.Length);
            Assert.Equal(["Bib", "Distance", "Status", "First", "Last", "Birthday"], header[..6]);
            Assert.Equal("Apparel", header[^1]);
            Dictionary<string, string[]> byBib = rows.Skip(1).ToDictionary(r => r[0]);
            Assert.Equal(["1", "5K"], byBib["1"][..2]);
            Assert.Equal(["Ann", "Alpha", "1990/02/01"], byBib["1"][3..6]);
            Assert.Equal(["22", "10K"], byBib["22"][..2]);
            Assert.Equal("X", byBib["22"][Array.IndexOf(header, "Gender")]);
        }

        [AvaloniaFact]
        public void Csv_export_strips_quotes_and_line_breaks_from_comments()
        {
            Seed();
            string path = Path.Combine(folder, "entrants.csv");

            ParticipantsPage.WriteParticipants(Db, theEvent, path);

            List<string[]> rows = ReadCsv(path);
            Assert.Equal(3, rows.Count);
            string comment = rows.Single(r => r[0] == "1")[Array.IndexOf(rows[0], "Comments")];
            Assert.Equal("Says  hi , then leaves", comment);
        }

        [AvaloniaFact]
        public void Excel_export_has_a_header_and_a_row_per_participant()
        {
            Seed();
            string path = Path.Combine(folder, "entrants.xlsx");

            ParticipantsPage.WriteParticipants(Db, theEvent, path);

            using XLWorkbook book = new(path);
            IXLWorksheet sheet = book.Worksheets.First();
            Assert.Equal("Bib", sheet.Cell(1, 1).GetString());
            Assert.Equal("Apparel", sheet.Cell(1, 27).GetString());
            Assert.Equal(3, sheet.LastRowUsed()!.RowNumber());
            List<string> firsts = [sheet.Cell(2, 4).GetString(), sheet.Cell(3, 4).GetString()];
            Assert.Equal(["Ann", "Cat"], firsts.Order());
        }

        [AvaloniaFact]
        public void Export_of_an_empty_event_is_just_the_header()
        {
            theEvent = AddEvent(makeCurrent: true);
            string path = Path.Combine(folder, "entrants.csv");

            ParticipantsPage.WriteParticipants(Db, theEvent, path);

            Assert.Equal("Bib", Assert.Single(ReadCsv(path))[0]);
        }

        // ---- Delete Uploaded ----

        private void LinkToFakeApi()
        {
            int apiId = Db.AddApi(new ApiObject(0, CHRONOKEEP_RESULTS_SELF, server.Url, "Local", "tok", ""));
            theEvent.ApiId = apiId;
            theEvent.ApiEventId = "spring-fling,2026";
            Db.UpdateEvent(theEvent);
        }

        private async Task<ParticipantsPage> OpenPage()
        {
            ParticipantsPage page = new(MainWindow, Db);
            Host(page);
            page.UpdateView();
            await Settle(100);
            return page;
        }

        [AvaloniaFact]
        public async Task Delete_uploaded_deletes_participants_then_chips_for_the_linked_event()
        {
            Seed();
            LinkToFakeApi();
            server.Responder = _ => (HttpStatusCode.OK, """{"count":2}""");
            ParticipantsPage page = await OpenPage();

            await page.DeleteUploaded();

            Assert.Equal(["participants/delete", "bibchips/delete"], server.Requests.Select(r => r.Path));
            Assert.All(server.Requests, r => Assert.Equal("DELETE", r.Method));
            JsonElement body = JsonDocument.Parse(server.Requests[0].Body).RootElement;
            Assert.Equal("spring-fling", body.GetProperty("slug").GetString());
            Assert.Equal("2026", body.GetProperty("year").GetString());
            Assert.Equal("Bearer tok", server.Requests[0].Authorization);
            Assert.Equal("Delete Uploaded", ContentText(Find<Button>(page, "Delete")));
        }

        [AvaloniaFact]
        public async Task Delete_uploaded_error_stops_and_resets_the_button()
        {
            Seed();
            LinkToFakeApi();
            server.Responder = _ => (HttpStatusCode.InternalServerError, """{"message":"boom"}""");
            ParticipantsPage page = await OpenPage();

            await page.DeleteUploaded();

            Assert.Equal("participants/delete", Assert.Single(server.Requests).Path);
            Assert.Equal("Delete Uploaded", ContentText(Find<Button>(page, "Delete")));
        }

        [AvaloniaFact]
        public async Task Delete_uploaded_does_nothing_without_a_linked_event()
        {
            Seed();
            ParticipantsPage page = await OpenPage();

            await page.DeleteUploaded();

            Assert.Empty(server.Requests);
            Assert.Equal("Delete Uploaded", ContentText(Find<Button>(page, "Delete")));
        }

        [AvaloniaFact]
        public async Task Delete_uploaded_button_asks_before_deleting()
        {
            Seed();
            LinkToFakeApi();
            ParticipantsPage page = await OpenPage();

            Click(Find<Button>(page, "Delete"));
            await Settle();

            Assert.Empty(server.Requests);
            Assert.Equal("Delete Uploaded", ContentText(Find<Button>(page, "Delete")));
        }
    }
}
