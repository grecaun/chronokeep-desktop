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

