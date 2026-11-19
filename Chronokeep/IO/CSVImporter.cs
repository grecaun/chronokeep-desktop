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
            File = new StreamReader(filePath, FileEncoding(filePath));
            this.filePath = filePath;
        }

        /// <summary>
        /// UTF-8 unless the file isn't valid UTF-8, which means it was saved in the Windows code page
        /// (Excel's plain CSV), so accented names like José read correctly either way. A byte order mark
        /// still wins.
        /// </summary>
        internal static Encoding FileEncoding(string filePath)
        {
            try
            {
                new UTF8Encoding(false, true).GetString(System.IO.File.ReadAllBytes(filePath));
                return Encoding.UTF8;
            }
            catch (DecoderFallbackException)
            {
                return Encoding.Latin1;
            }
        }

        public void FetchHeaders()
        {
            Log.D("IO.CSVImporter", "Getting headers from file.");
            ProcessFirstLine(ReadRecord()!);
        }

        protected void ProcessFirstLine(string line)
        {
            Data = new ImportData(ParseLine(line), filePath, ImportData.FileType.CSV);
        }

        public void FetchData()
        {
            Log.D("IO.CSVImporter", "Getting data from file.");
            while (ReadRecord() is { } record)
            {
                Data!.AddData(ParseLine(record));
            }
            Finish();
        }

        /// <summary>
        /// Reads one row of the file. A quoted value can hold line breaks, so a row carries on over as many lines
        /// as it takes to close its quotes; the line breaks inside it are kept as "\n". Returns null at the end.
        /// </summary>
        protected string? ReadRecord()
        {
            if (File.ReadLine() is not { } line) return null;
            StringBuilder record = new(line);
            while (Parse(record.ToString(), out _) && File.ReadLine() is { } next)
            {
                record.Append('\n').Append(next);
            }
            return record.ToString();
        }

        /// <summary>
        /// Splits one CSV row into trimmed values, the standard way (RFC 4180): a value starting with a double quote
        /// runs to the matching quote, so it can hold commas and line breaks, and a doubled quote ("") inside it is
        /// a literal quote. A quote anywhere else in a value, like 5'10", is just part of the value.
        /// </summary>
        internal static string[] ParseLine(string line)
        {
            Parse(line, out List<string> values);
            return [.. values];
        }

        /// <summary>Splits a row into values, and says whether it ends inside a quoted value that isn't closed yet.</summary>
        private static bool Parse(string line, out List<string> values)
        {
            values = [];
            StringBuilder current = new();
            bool inQuotes = false;
            // Whether the value so far is only whitespace, so a quote here starts a quoted value.
            bool atValueStart = true;
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
                else if (c == '"' && atValueStart)
                {
                    current.Clear();
                    inQuotes = true;
                    atValueStart = false;
                }
                else if (c == ',')
                {
                    values.Add(current.ToString().Trim());
                    current.Clear();
                    atValueStart = true;
                }
                else
                {
                    current.Append(c);
                    if (!char.IsWhiteSpace(c)) atValueStart = false;
                }
            }
            values.Add(current.ToString().Trim());
            return inQuotes;
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
