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
using System.Collections.Generic;
using System.Globalization;

namespace Chronokeep.Constants
{
    /// <summary>
    /// The states a participant's registration can be in, stored in eventspecific_removed.
    /// Empty (entered in Chronokeep) and active (in the race per Runsignup) are in the race.
    /// removed_{reason} is out of the race; reactivated_{reason} was put back by hand after being
    /// removed for that reason, so the reason isn't lost and later downloads leave them in.
    /// </summary>
    public static class Registration
    {
        /// <summary>Entered in Chronokeep, or downloaded before states were tracked.</summary>
        public const string NONE = "";
        /// <summary>In the race according to the registration system.</summary>
        public const string ACTIVE = "active";

        public const string REMOVED_PREFIX = "removed_";
        public const string REACTIVATED_PREFIX = "reactivated_";

        // The reasons Runsignup gives (removed_reason), plus unknown for a removal with no reason
        // and bib_changed for the old row Chronokeep keeps when a Runsignup bib changes.
        public const string TRANSFERRED = "transferred";
        public const string REMOVED = "removed";
        public const string CLEARED = "cleared";
        public const string DEFERRED = "deferred";
        public const string UNKNOWN = "unknown";
        public const string BIB_CHANGED = "bib_changed";

        private static readonly Dictionary<string, string> Labels = new()
        {
            [TRANSFERRED] = "Transferred",
            [REMOVED] = "Removed",
            [CLEARED] = "Cleared",
            [DEFERRED] = "Deferred",
            [UNKNOWN] = "Removed",
            [BIB_CHANGED] = "Bib Changed",
        };

        /// <summary>The state for someone removed for this reason, such as removed_transferred.</summary>
        public static string Removed(string reason) => REMOVED_PREFIX + reason;

        /// <summary>The state for someone put back in the race after being removed for this reason.</summary>
        public static string Reactivated(string reason) => REACTIVATED_PREFIX + reason;

        public static bool IsRemoved(string? state) => (state ?? "").StartsWith(REMOVED_PREFIX, StringComparison.Ordinal);

        public static bool IsReactivated(string? state) => (state ?? "").StartsWith(REACTIVATED_PREFIX, StringComparison.Ordinal);

        /// <summary>The reason in a removed or reactivated state ("transferred"), or "" for anyone else.</summary>
        public static string Reason(string? state)
        {
            string value = state ?? "";
            if (IsRemoved(value)) return value[REMOVED_PREFIX.Length..];
            if (IsReactivated(value)) return value[REACTIVATED_PREFIX.Length..];
            return "";
        }

        /// <summary>
        /// How a reason reads to a person: "Transferred", "Deferred", "Bib Changed". A reason
        /// Chronokeep doesn't know yet is shown with its first letter capitalised.
        /// </summary>
        public static string Label(string reason)
        {
            if (Labels.TryGetValue(reason, out string? label)) return label;
            string words = reason.Replace('_', ' ').Trim();
            return words.Length == 0 ? Labels[UNKNOWN] : char.ToUpper(words[0], CultureInfo.InvariantCulture) + words[1..];
        }

        /// <summary>The reason to store for a Runsignup removed_reason. Missing reasons are unknown.</summary>
        public static string FromRunsignup(string? removedReason)
        {
            string reason = (removedReason ?? "").Trim().ToLowerInvariant().Replace(' ', '_');
            return reason.Length == 0 ? UNKNOWN : reason;
        }

        /// <summary>SQL that's true when eventspecific_removed is in the race (anything not removed_*).</summary>
        public const string InRaceSql = @"e.eventspecific_removed NOT LIKE 'removed\_%' ESCAPE '\'";
    }
}
