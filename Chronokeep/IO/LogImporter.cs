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
using System.Text.RegularExpressions;

namespace Chronokeep.IO
{
    public partial class LogImporter(string filePath) : CsvImporter(filePath)
    {
        [GeneratedRegex("^\\d,[0-9A-Fa-f]+,\\d,\"(\\d{4}-\\d{2}-\\d{2} )?\\d{1,2}:\\d{2}:\\d{2}\\.\\d{3}\"$|" + // RFID Timing style?
                                "^[0-9A-Fa-f]+\\t(\\d{4}-\\d{2}-\\d{2} )?\\d{1,2}:\\d{2}:\\d{2}\\.\\d{3}$")]           // RFID Server style?    
        private static partial Regex Rfid();
        [GeneratedRegex(@"aa[0-9a-fA-F]{34,36}")]
        private static partial Regex Ipico();
        [GeneratedRegex("[\"]?status[\"]?,[\"]?chip_number[\"]?,[\"]?seconds[\"]?,[\"]?milliseconds[\"]?,[\"]?time_seconds[\"]?,[\"]?time_milliseconds[\"]?,[\"]?antenna[\"]?,[\"]?reader[\"]?,[\"]?box[\"]?,[\"]?log_index[\"]?,[\"]?rssi[\"]?,[\"]?is_rewind[\"]?,[\"]?reader_time[\"]?,[\"]?start_time[\"]?,[\"]?read_bib[\"]?,[\"]?type[\"]?")]
        private static partial Regex Chronokeep();

        public Type Kind = Type.CUSTOM;

        public void FindType()
        {
            string headerLine = File.ReadLine()!;
            Log.D("IO.LogImporter", $"HeaderLine: {headerLine}");
            if (Rfid().IsMatch(headerLine))
            {
                Log.D("IO.LogImporter", "Found a match! RFID");
                Kind = Type.RFID;
            }
            if (Ipico().IsMatch(headerLine))
            {
                Log.D("IO.LogImporter", "Found a match! Ipico");
                Kind = Type.IPICO;
            }
            if (Chronokeep().IsMatch(headerLine))
            {
                Log.D("IO.LogImporter", "Found a match! Chronokeep");
                Kind = Type.CHRONOKEEP;
            }
            ProcessFirstLine(headerLine);
        }

        public enum Type
        { RFID, IPICO, CHRONOKEEP, CUSTOM }
    }
}

