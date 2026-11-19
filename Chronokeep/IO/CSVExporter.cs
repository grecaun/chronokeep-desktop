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
using System.Text;

namespace Chronokeep.IO
{
    /// <summary>
    /// Writes a CSV file the standard way (RFC 4180): values are separated by commas, a value is only quoted when
    /// it holds a comma, a quote, a line break or starts or ends with a space, and quotes inside a quoted value are
    /// doubled. Lines end with CRLF. The file is UTF-8 with a byte order mark, so Excel opening it directly reads
    /// accented names correctly.
    /// </summary>
    internal class CsvExporter : IDataExporter
    {
        private string[] headers = [];
        private List<object[]> data = [];

        public void ExportData(string path)
        {
            using (FileStream outFile = File.Create(path))
            {
                using StreamWriter outWriter = new(outFile, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)) { NewLine = "\r\n" };
                outWriter.WriteLine(Line(headers));
                foreach (object[] line in data)
                {
                    // Empty cells (such as a place for someone who hasn't finished) are written blank, as the Excel exporter does.
                    outWriter.WriteLine(Line(line.Select(x => x?.ToString() ?? "")));
                }
            }
            // Let every user on the computer open or replace the export.
            ProgramFolders.ShareFile(path);
        }

        /// <summary>One CSV line: each value, quoted only if it needs to be, separated by commas.</summary>
        internal static string Line(IEnumerable<string?> values) => string.Join(",", values.Select(Field));

        /// <summary>One CSV value, quoted (with any quotes in it doubled) only if it needs to be.</summary>
        internal static string Field(string? value)
        {
            value ??= "";
            bool needsQuotes = value.Length > 0
                && (value.IndexOfAny([',', '"', '\r', '\n']) >= 0 || char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]));
            return needsQuotes ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
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

