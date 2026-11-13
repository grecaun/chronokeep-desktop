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
using System.Text.RegularExpressions;

namespace Chronokeep.Helpers
{
    /// <summary>
    /// The one birthdate format Chronokeep stores and compares: yyyy/MM/dd. It is year-first, so it
    /// reads the same on every computer, and it is one of the two forms Chronokeep Results accepts
    /// (Go layouts 1/2/2006 and 2006/1/2; it rejects yyyy-MM-dd). Every source
    /// (imports, the participant editor, the Chronokeep API, registration, the database) runs its
    /// value through <see cref="Normalize"/>, so the same date is always the same string.
    /// </summary>
    internal static partial class Birthdates
    {
        public const string Format = "yyyy/MM/dd";

        // Year-first forms are unambiguous, so they are tried before the computer's own date order.
        private static readonly string[] YearFirstFormats =
        [
            "yyyy-MM-dd", "yyyy-M-d", "yyyy/MM/dd", "yyyy/M/d",
            "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssK",
            "yyyy/MM/dd HH:mm:ss", "yyyy/M/d H:mm:ss", "yyyy/MM/dd HH:mm:ss zzz",
        ];

        /// <summary>
        /// Returns the date as yyyy/MM/dd. Empty input stays empty. Text that is not a date is
        /// returned trimmed but otherwise untouched, so nothing a person typed is thrown away.
        /// Day/month order for slashed dates such as 01/02/1990 follows the computer's culture,
        /// the same rule the app has always used to read them.
        /// </summary>
        public static string Normalize(string? value)
        {
            if (value == null)
            {
                return "";
            }
            string trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                return "";
            }
            return TryParse(trimmed, out DateTime date)
                ? date.ToString(Format, CultureInfo.InvariantCulture)
                : trimmed;
        }

        /// <summary>Reads a birthdate in any accepted form.</summary>
        public static bool TryParse(string? value, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            string trimmed = value.Trim();
            if (!DateTime.TryParseExact(trimmed, YearFirstFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                && !DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out date))
            {
                return false;
            }
            // .NET reads a two-digit year up to 49 as 20xx, so 1/2/45 would be 2045. Nobody is born in the
            // future, so a two-digit year that lands there belongs to the century before.
            if (date > DateTime.Today && !FourDigitYear().IsMatch(trimmed))
            {
                date = date.AddYears(-100);
            }
            return true;
        }

        [GeneratedRegex(@"\d{4}")]
        private static partial Regex FourDigitYear();

        /// <summary>
        /// A stored birthdate as MM/dd/yyyy for files sent to other systems (Abbott WMM, Boston, UltraSignup)
        /// that expect month/day/year. Empty when the birthdate isn't a date.
        /// </summary>
        public static string ToMonthDayYear(string? stored) =>
            TryParse(stored, out DateTime date) ? date.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture) : "";

        /// <summary>A placeholder birthdate on January 1st of the given year, in the stored format.</summary>
        public static string JanuaryFirst(int year) => new DateTime(year, 1, 1).ToString(Format, CultureInfo.InvariantCulture);
    }
}
