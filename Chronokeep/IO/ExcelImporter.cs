/*
Chronokeep Desktop - Race Scoring Software
Copyright (C) 2026 James Sentinella

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

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
                int numHeaders = LastColumn();
                Log.D("IO.ExcelImporter", $"Columns {numHeaders}");
                string[] headers = new string[numHeaders];
                for (int i = 1; i <= numHeaders; i++)
                {
                    headers[i - 1] = worksheet!.Cell(1, i).Value.IsBlank ? "" : worksheet.Cell(1, i).Value.ToString();
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
                int numHeaders = LastColumn();
                int lastRow = worksheet!.LastRowUsed()?.RowNumber() ?? 0;
                Log.D("IO.ExcelImporter", $"Rows {lastRow} Columns {numHeaders}");
                // Row 1 holds the headers.
                for (int row = 2; row <= lastRow; row++)
                {
                    string[] dataLine = new string[numHeaders];
                    bool blank = true;
                    for (int column = 1; column <= numHeaders; column++)
                    {
                        XLCellValue value = worksheet.Cell(row, column).Value;
                        dataLine[column - 1] = value.IsBlank ? "" : value.ToString();
                        blank = blank && dataLine[column - 1].Length == 0;
                    }
                    // Skip completely empty rows rather than importing a blank participant.
                    if (!blank)
                    {
                        Data!.AddData(dataLine);
                    }
                }
                Finish();
            }
            catch (Exception excep)
            {
                Log.E("IO.ExcelImporter", $"Couldn't get data. {excep.StackTrace}");
            }
        }

        /// <summary>The last column with anything in it, so empty columns don't hide the ones after them.</summary>
        private int LastColumn() => worksheet!.LastColumnUsed()?.ColumnNumber() ?? 0;

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

