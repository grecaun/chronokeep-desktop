using Chronokeep.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Chronokeep.IO
{
    public partial class ImportData
    {
        private string FileName { get; }
        public FileType Type { get; private set; }
        public string[] Headers { get; }
        public List<string[]> Data { get; }

        [GeneratedRegex("[^\\\\]*\\.")]
        private static partial Regex DataRegex();

        public ImportData(string[] headers, string filename, FileType type)
        {
            Type = type;
            FileName = DataRegex().Match(filename).Value.TrimEnd('.');
            Log.D("IO.ImportData", $"{FileName} is the filename.");
            string[] newheaders = new string[headers.Length + 1];
            Array.Copy(headers, 0, newheaders, 1, headers.Length);
            GatheredInformationLog("Headers are", newheaders);
            Data = [];
            Headers = newheaders;
        }

        [Conditional("DEBUG")]
        private static void GatheredInformationLog(string named, string[] data)
        {
            StringBuilder sb = new(named);
            foreach (string s in data)
            {
                sb.Append($" '{s}'");
            }
            Log.D("IO.ImportData", sb.ToString());
        }

        public int GetNumHeaders()
        {
            return Headers.Length;
        }

        public void AddData(string[] data)
        {
            string[] newdata = new string[data.Length + 1];
            Array.Copy(data, 0, newdata, 1, data.Length);
            if (Headers.Length != newdata.Length)
            {
                Log.E("IO.ImportData", $"Header count wrong on import of data: {Headers.Length} - {newdata.Length}");
            }
            Data.Add(newdata);
            GatheredInformationLog("Data input is", newdata);
        }

        public string[] GetDistanceNames(int index)
        {
            HashSet<string> values = [];
            foreach (string[] line in Data.Where(line => line[index].Length > 0))
            {
                values.Add(line[index].Trim());
            }
            string[] output = new string[values.Count];
            values.CopyTo(output);
            return output;
        }

        public enum FileType { EXCEL, CSV }
    }
}
