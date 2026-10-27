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
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Chronokeep.IO
{
    internal class ExcelExporter : IDataExporter
    {
        private string[] headers = [];
        private List<object[]> data = [];

        public void ExportData(string path)
        {
            using XLWorkbook workbook = new();
            IXLWorksheet worksheet = workbook.Worksheets.Add();
            List<object[]> localData =
            [
                headers, .. data
            ];
            for (int i = 0; i < localData.Count; i++)
            {
                for (int j = 0; j < localData[0].Length; j++)
                {
                    worksheet.Cell(i + 1, j + 1).Style.NumberFormat.Format = "@";
                    worksheet.Cell(i + 1, j + 1).Value = localData[i][j] != null ? localData[i][j].ToString() : "";
                }
            }
            workbook.SaveAs(path);
        }

        public Utils.FileType FileType()
        {
            return Utils.FileType.EXCEL;
        }

        public void SetData(string[] iHeaders, List<object[]> iData)
        {
            headers = iHeaders;
            data = iData;
            GatheredInformationLog("Headers", headers);
            foreach (object[] line in data)
            {
                GatheredInformationLog("Row", line);
            }
        }

        [Conditional("DEBUG")]
        private static void GatheredInformationLog(string named, object[] data)
        {
            StringBuilder sb = new(named);
            foreach (object s in data)
            {
                sb.Append($" '{s}'");
            }
            Log.D("IO.ExcelExporter", sb.ToString());
        }
    }
}
