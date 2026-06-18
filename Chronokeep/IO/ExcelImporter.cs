using Chronokeep.Helpers;
using Chronokeep.Interfaces.IO;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Chronokeep.IO
{
    internal class ExcelImporter : IDataImporter
    {
        public ImportData? Data { get; private set; }
        private readonly string filePath;

        private XLWorkbook? workbook;
        private IXLWorksheet? worksheet;

        public List<string> SheetNames { get; }
        private int NumSheets { get; }

        public ExcelImporter(string filename)
        {
            Log.D("IO.ExcelImporter", "Creating importer object.");
            filePath = filename;
            Log.D("IO.ExcelImporter", "Opening workbook.");
            workbook = new XLWorkbook(filename);
            NumSheets = workbook.Worksheets.Count;
            if (NumSheets > 0)
            {
                worksheet = workbook.Worksheets.Worksheet(1);
            }
            SheetNames = [];
            for (int i = 1; i <= NumSheets; i++)
            {
                string name = workbook.Worksheets.Worksheet(i).Name;
                SheetNames.Add(name);
                Log.D("IO.ExcelImporter", $"Sheet name is {name}");
            }
        }

        public void ChangeSheet(int ix)
        {
            try
            {
                if (ix <= NumSheets && ix >= 0)
                {
                    worksheet = workbook?.Worksheets.Worksheet(ix);
                }
            }
            catch
            {
                Log.E("IO.ExcelImporter", "Hmm, can't get that worksheet. Sorry.");
            }
        }

        public void FetchHeaders()
        {
            Log.D("IO.ExcelImporter", "Getting headers from excel file.");
            try
            {
                Log.D("IO.ExcelImporter", "Used range set.");
                IXLRows rows = worksheet!.RowsUsed();
                int numHeaders = 0;
                int numDataRows = 0;
                foreach (IXLRow? row in rows)
                {
                    int tmp = row.CellsUsed().Count();
                    numHeaders = numHeaders > tmp ? numHeaders : tmp;
                    numDataRows++;
                }
                Log.D("IO.ExcelImporter", $"Value array populated. Rows {numDataRows} Columns {numHeaders}");
                string[] headers = new string[numHeaders];
                for (int i = 1; i <= numHeaders; i++)
                {
                    headers[i - 1] = worksheet.Cell(1, i).Value.IsBlank ? "" : worksheet.Cell(1, i).Value.ToString();
                }
                Data = new ImportData(headers, filePath, ImportData.FileType.EXCEL);
            }
            catch (Exception excep)
            {
                Log.E("IO.ExcelImporter", $"Something went wrong when trying to get headers. {excep.StackTrace}");
                string[] headers = [];
                Data = new ImportData(headers, filePath, ImportData.FileType.EXCEL);
            }
        }

        public void FetchData()
        {
            Log.D("IO.ExcelImporter", "Getting data from excel file.");
            try
            {
                IXLRows rows = worksheet!.RowsUsed();
                int numHeaders = 0;
                int numDataRows = 0;
                foreach (IXLRow? row in rows)
                {
                    int tmp = row.CellsUsed().Count();
                    numHeaders = numHeaders > tmp ? numHeaders : tmp;
                    numDataRows++;
                }
                object[,] valueArray = new object[numDataRows, numHeaders];
                for (int i = 1; i <= numDataRows; i++)
                {
                    for (int j = 1; j <= numHeaders; j++)
                    {
                        valueArray[i - 1, j - 1] = worksheet.Cell(i, j).Value;
                    }
                }
                Log.D("IO.ExcelImporter", $"Value array populated. Rows {numDataRows} Columns {numHeaders}");
                for (int row = 1; row < numDataRows; row++)
                {
                    string[] dataLine = new string[numHeaders];
                    for (int column = 0; column < numHeaders; column++)
                    {
                        dataLine[column] = valueArray[row, column].ToString()!;
                    }
                    Data!.AddData(dataLine);
                }
                Finish();
            }
            catch (Exception excep)
            {
                Log.E("IO.ExcelImporter", $"Couldn't get data. {excep.StackTrace}");
            }
        }

        public void Finish()
        {
            Log.D("IO.ExcelImporter", "Closing file.");
            try
            {
                worksheet = null;
                workbook?.Dispose();
                workbook = null;
            }
            catch
            {
                Log.D("IO.ExcelImporter", "Something went wrong when trying to close excel file.");
            }
        }
    }
}
