using Chronokeep.Helpers;
using Chronokeep.Interfaces.IO;
using ClosedXML.Excel;
using System.Collections.Generic;

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
                    worksheet.Cell(i + 1, j + 1).Value = localData[i][j].ToString();
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
            Log.D("IO.ExcelExporter", $"Headers {headers} Data {data}");
        }
    }
}