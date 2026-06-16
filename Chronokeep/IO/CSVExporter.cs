using Chronokeep.Helpers;
using Chronokeep.Interfaces.IO;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Chronokeep.IO
{
    internal class CsvExporter(string format) : IDataExporter
    {
        private string[] headers = [];
        private List<object[]> data = [];

        public void ExportData(string path)
        {
            using FileStream outFile = File.Create(path);
            using StreamWriter outWriter = new StreamWriter(outFile);
            outWriter.WriteLine(format, headers);
            foreach (object[] line in data)
            {
                outWriter.WriteLine(format, [.. line.Select(x => x.ToString())]);
            }
        }

        public Utils.FileType FileType()
        {
            return Utils.FileType.CSV;
        }

        public void SetData(string[] iHeaders, List<object[]> iData)
        {
            headers = iHeaders;
            data = iData;
        }
    }
}
