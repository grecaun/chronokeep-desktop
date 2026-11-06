using Chronokeep.IO;
using ClosedXML.Excel;

namespace Chronokeep.Test.IO
{
    /// <summary>Gives each test its own temp folder for sample files.</summary>
    public abstract class FileTestBase : IDisposable
    {
        protected readonly string Dir = Path.Combine(Path.GetTempPath(), "chronokeep-tests", Guid.NewGuid().ToString("N"));

        protected FileTestBase()
        {
            Directory.CreateDirectory(Dir);
            // Log.E appends to the error log, which the app sets up at startup.
            Helpers.Globals.ErrorLogPath = Path.Combine(Dir, "error_log.txt");
        }

        protected string WriteFile(string name, string contents)
        {
            string path = Path.Combine(Dir, name);
            File.WriteAllText(path, contents);
            return path;
        }

        public void Dispose()
        {
            try { Directory.Delete(Dir, true); } catch (IOException) { }
            GC.SuppressFinalize(this);
        }
    }

    public class CsvImporterTests : FileTestBase
    {
        private ImportData Import(string contents)
        {
            CsvImporter importer = new(WriteFile("people.csv", contents));
            importer.FetchHeaders();
            importer.FetchData();
            return importer.Data!;
        }

        [Fact]
        public void Headers_GetABlankFirstColumn()
        {
            var data = Import("First,Last,Bib\nJane,Runner,1\n");
            Assert.Equal(["", "First", "Last", "Bib"], data.Headers);
            Assert.Equal(4, data.GetNumHeaders());
            Assert.Equal(ImportData.FileType.CSV, data.Type);
        }

        [Fact]
        public void Rows_AreSplitIntoColumns()
        {
            var data = Import("First,Last,Bib\nJane,Runner,1\nJohn,Walker,2\n");
            Assert.Equal(2, data.Data.Count);
            Assert.Equal(["", "Jane", "Runner", "1"], data.Data[0]);
            Assert.Equal(["", "John", "Walker", "2"], data.Data[1]);
        }

        [Fact]
        public void EmptyFields_AreKept()
        {
            var data = Import("First,Middle,Last\nJane,,Runner\n");
            Assert.Equal(["", "Jane", "", "Runner"], data.Data[0]);
        }

        [Fact]
        public void QuotedFieldWithComma_StaysOneColumn()
        {
            var data = Import("Last,First,Bib\n\"Runner, Jr\",Jane,1\n");
            Assert.Equal(["", "Runner, Jr", "Jane", "1"], data.Data[0]);
        }

        [Fact]
        public void QuotedFieldWithCommaAtEndOfLine_StaysOneColumn()
        {
            var data = Import("Bib,First,Address\n1,Jane,\"1 Main St, Apt 4\"\n");
            Assert.Equal(["", "1", "Jane", "1 Main St, Apt 4"], data.Data[0]);
        }

        [Fact]
        public void DoubledQuotesInsideQuotedValue_BecomeAQuote()
        {
            var data = Import("Bib,Nickname\n1,\"Jane \"\"Speedy\"\" Runner\"\n");
            Assert.Equal(["", "1", "Jane \"Speedy\" Runner"], data.Data[0]);
        }

        [Fact]
        public void WindowsLineEndings_AreHandled()
        {
            var data = Import("First,Last\r\nJane,Runner\r\n");
            Assert.Equal(["", "First", "Last"], data.Headers);
            Assert.Equal(["", "Jane", "Runner"], data.Data[0]);
        }

        [Fact]
        public void SurroundingWhitespace_IsTrimmed()
        {
            var data = Import(" First , Last \n Jane , Runner \n");
            Assert.Equal(["", "First", "Last"], data.Headers);
            Assert.Equal(["", "Jane", "Runner"], data.Data[0]);
        }

        [Fact]
        public void GetDistanceNames_ReturnsDistinctTrimmedValues()
        {
            var data = Import("Bib,Distance\n1,5K\n2, 5K\n3,10K\n4,\n");
            Assert.Equal(["10K", "5K"], data.GetDistanceNames(2).Order());
        }
    }

    public class LogImporterTests : FileTestBase
    {
        private LogImporter.Type Detect(string firstLine)
        {
            LogImporter importer = new(WriteFile("log.txt", firstLine + "\n"));
            importer.FindType();
            importer.Finish();
            return importer.Kind;
        }

        [Theory]
        [InlineData("1,058001,1,\"2026-06-15 08:00:00.123\"", LogImporter.Type.RFID)]
        [InlineData("1,058001,1,\"8:00:00.123\"", LogImporter.Type.RFID)]
        [InlineData("058001\t2026-06-15 08:00:00.123", LogImporter.Type.RFID)]
        [InlineData("aa010580001234560000260615080000 0aff", LogImporter.Type.CUSTOM)]
        [InlineData("aa01058000123456000026061508000000aff", LogImporter.Type.IPICO)]
        [InlineData("status,chip_number,seconds,milliseconds,time_seconds,time_milliseconds,antenna,reader,box,log_index,rssi,is_rewind,reader_time,start_time,read_bib,type", LogImporter.Type.CHRONOKEEP)]
        [InlineData("Bib,Time", LogImporter.Type.CUSTOM)]
        public void FindType_RecognizesLogFormats(string firstLine, LogImporter.Type expected)
        {
            Assert.Equal(expected, Detect(firstLine));
        }

        [Fact]
        public void FindType_KeepsFirstLineAsHeaders()
        {
            LogImporter importer = new(WriteFile("log.csv", "Bib,Time\n1,8:00:00\n"));
            importer.FindType();
            importer.FetchData();
            Assert.Equal(["", "Bib", "Time"], importer.Data!.Headers);
            Assert.Single(importer.Data.Data);
        }
    }

    public class ExcelImporterTests : FileTestBase
    {
        private string Workbook(Action<IXLWorksheet> fill, string sheet = "People", Action<XLWorkbook>? more = null)
        {
            string path = Path.Combine(Dir, "people.xlsx");
            using XLWorkbook wb = new();
            fill(wb.Worksheets.Add(sheet));
            more?.Invoke(wb);
            wb.SaveAs(path);
            return path;
        }

        private static ImportData Import(string path, int sheet = 1)
        {
            ExcelImporter importer = new(path);
            importer.ChangeSheet(sheet);
            importer.FetchHeaders();
            importer.FetchData();
            return importer.Data!;
        }

        private static void Row(IXLWorksheet ws, int row, params object?[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null)
                {
                    ws.Cell(row, i + 1).Value = XLCellValue.FromObject(values[i]);
                }
            }
        }

        [Fact]
        public void ReadsHeadersAndRows()
        {
            var data = Import(Workbook(ws =>
            {
                Row(ws, 1, "First", "Last", "Bib");
                Row(ws, 2, "Jane", "Runner", 101);
                Row(ws, 3, "John", "Walker", 102);
            }));

            Assert.Equal(["", "First", "Last", "Bib"], data.Headers);
            Assert.Equal(ImportData.FileType.EXCEL, data.Type);
            Assert.Equal(2, data.Data.Count);
            Assert.Equal(["", "Jane", "Runner", "101"], data.Data[0]);
        }

        [Fact]
        public void BlankCellsInsideARow_StayBlank()
        {
            var data = Import(Workbook(ws =>
            {
                Row(ws, 1, "First", "Middle", "Last");
                Row(ws, 2, "Jane", null, "Runner");
                Row(ws, 3, "John", "Q", "Walker");
            }));
            Assert.Equal(["", "Jane", "", "Runner"], data.Data[0]);
        }

        [Fact]
        public void BlankRowInTheMiddle_DoesNotDropLaterRows()
        {
            var data = Import(Workbook(ws =>
            {
                Row(ws, 1, "First", "Bib");
                Row(ws, 2, "Jane", 1);
                Row(ws, 4, "John", 2);
            }));
            Assert.Contains(data.Data, r => r[1] == "John");
            Assert.Equal(2, data.Data.Count);   // the blank row itself isn't imported
        }

        [Fact]
        public void EmptyColumn_DoesNotDropColumnsAfterIt()
        {
            var data = Import(Workbook(ws =>
            {
                Row(ws, 1, "First", "Last", null, "Bib");
                Row(ws, 2, "Jane", "Runner", null, 1);
            }));
            Assert.Equal(5, data.GetNumHeaders());
            Assert.Equal("1", data.Data[0][4]);
        }

        [Fact]
        public void SheetNames_AndChangeSheet()
        {
            string path = Workbook(ws => Row(ws, 1, "Ignore"), "Cover", wb =>
            {
                var people = wb.Worksheets.Add("People");
                Row(people, 1, "First", "Bib");
                Row(people, 2, "Jane", 1);
            });

            ExcelImporter importer = new(path);
            Assert.Equal(["Cover", "People"], importer.SheetNames);
            importer.Finish();

            var data = Import(path, sheet: 2);
            Assert.Equal(["", "First", "Bib"], data.Headers);
            Assert.Equal("Jane", data.Data[0][1]);
        }
    }

    public class ExporterTests : FileTestBase
    {
        [Fact]
        public void CsvExporter_WritesHeaderAndRowsUsingFormat()
        {
            string path = Path.Combine(Dir, "out.csv");
            CsvExporter exporter = new("{0},\"{1}\",{2}");
            exporter.SetData(["Bib", "Name", "Time"], [[1, "Runner, Jane", "0:25:00"], [2, "Walker, John", "0:26:00"]]);

            exporter.ExportData(path);

            Assert.Equal(
                ["Bib,\"Name\",Time", "1,\"Runner, Jane\",0:25:00", "2,\"Walker, John\",0:26:00"],
                File.ReadAllLines(path));
            Assert.Equal(Helpers.Utils.FileType.CSV, exporter.FileType());
        }

        [Fact]
        public void ExcelExporter_WritesTextCellsThatImportBack()
        {
            string path = Path.Combine(Dir, "out.xlsx");
            ExcelExporter exporter = new();
            exporter.SetData(["Bib", "Name", "Time"], [[1, "Jane Runner", "0:25:00"], [2, null!, "0:26:00"]]);

            exporter.ExportData(path);

            using (XLWorkbook wb = new(path))
            {
                var cell = wb.Worksheet(1).Cell(2, 1);
                Assert.True(cell.Value.IsText);
                Assert.Equal("1", cell.GetString());
            }
            ExcelImporter importer = new(path);
            importer.FetchHeaders();
            importer.FetchData();
            Assert.Equal(["", "Bib", "Name", "Time"], importer.Data!.Headers);
            Assert.Equal(["", "2", "", "0:26:00"], importer.Data.Data[1]);
            Assert.Equal(Helpers.Utils.FileType.EXCEL, exporter.FileType());
        }
    }
}
