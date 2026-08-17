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

