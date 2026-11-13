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

using System;
using System.Globalization;

namespace Chronokeep.Helpers
{
    /// <summary>
    /// Event dates are stored as yyyy/MM/dd, like birthdates, whatever the computer's language, so a
    /// database reads the same on every computer. Reading also accepts M/d/yyyy and the computer's own
    /// date format, for databases written before this was settled.
    /// </summary>
    internal static class EventDates
    {
        public const string Format = "yyyy/MM/dd";

        private static readonly string[] StoredFormats =
        [
            "yyyy/MM/dd", "yyyy/M/d", "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss",
            "M/d/yyyy", "MM/dd/yyyy", "M/d/yyyy H:mm:ss", "M/d/yyyy h:mm:ss tt",
        ];

        /// <summary>Reads a stored event date: the stored format first, then the computer's own format.</summary>
        public static bool TryParse(string? value, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            string trimmed = value.Trim();
            return DateTime.TryParseExact(trimmed, StoredFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                || DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
        }

        /// <summary>
        /// Reads a date written by this computer before event dates had one format: the computer's own
        /// format first, so 05/06/2026 written on a UK computer stays 5 June.
        /// </summary>
        public static bool TryParseLocalFirst(string? value, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            string trimmed = value.Trim();
            return DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out date)
                || DateTime.TryParseExact(trimmed, StoredFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        /// <summary>The event's date at midnight. Throws FormatException if it isn't a date, as DateTime.Parse did.</summary>
        public static DateTime Parse(string? value) =>
            TryParse(value, out DateTime date) ? date.Date : throw new FormatException($"'{value}' is not an event date.");

        /// <summary>A date in the stored format.</summary>
        public static string From(DateTime date) => date.ToString(Format, CultureInfo.InvariantCulture);

        /// <summary>The date for people to read, such as June 15, 2026, or the text as stored if it isn't a date.</summary>
        public static string Display(string? value) =>
            TryParse(value, out DateTime date) ? date.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture) : value?.Trim() ?? "";

        /// <summary>The date as MM/dd/yyyy, the layout US results services expect, or empty if it isn't a date.</summary>
        public static string ToMonthDayYear(string? value) =>
            TryParse(value, out DateTime date) ? date.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture) : "";

        /// <summary>A date in the stored format, or the text as given if it isn't a date.</summary>
        public static string Normalize(string? value) =>
            TryParse(value, out DateTime date) ? From(date) : value?.Trim() ?? "";
    }
}
