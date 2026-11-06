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
using System.Text;

namespace Chronokeep.IO
{
    public class CsvImporter : IDataImporter
    {
        public ImportData? Data { get; private set; }
        private readonly string filePath;
        protected readonly StreamReader File;

        public CsvImporter(string filePath)
        {
            Log.D("IO.CSVImporter", "Opening file.");
            File = new StreamReader(filePath);
            this.filePath = filePath;
        }

        public void FetchHeaders()
        {
            Log.D("IO.CSVImporter", "Getting headers from file.");
            ProcessFirstLine(File.ReadLine()!);
        }

        protected void ProcessFirstLine(string line)
        {
            Data = new ImportData(ParseLine(line), filePath, ImportData.FileType.CSV);
        }

        public void FetchData()
        {
            Log.D("IO.CSVImporter", "Getting data from file.");
            while (File.ReadLine() is { } line)
            {
                Data!.AddData(ParseLine(line));
            }
            Finish();
        }

        /// <summary>
        /// Splits one CSV line into trimmed values. Commas inside double quotes are part of the value,
        /// and a doubled quote ("") inside a quoted value is a literal quote.
        /// </summary>
        internal static string[] ParseLine(string line)
        {
            List<string> values = [];
            StringBuilder current = new();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else if (c == '"')
                    {
                        inQuotes = false;
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    values.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            values.Add(current.ToString().Trim());
            return [.. values];
        }

        public void Finish()
        {
            try
            {
                Log.D("IO.CSVImporter", "Closing file.");
                File.Close();
            }
            catch
            {
                Log.D("IO.CSVImporter", "Already closed.");
            }
        }
    }
}
