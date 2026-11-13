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
using Chronokeep.Objects;
using Chronokeep.Objects.Runsignup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Chronokeep.Network.Runsignup
{
    /// <summary>
    /// Finds the Runsignup race a Chronokeep event most likely is, by name and date, so the link
    /// wizard can pick it out. It only answers when one race is clearly close; otherwise it leaves
    /// the choice to the person.
    /// </summary>
    public static partial class RaceMatcher
    {
        // Words that say nothing about which race it is.
        private static readonly HashSet<string> Filler = ["the", "a", "an", "and", "of", "annual"];

        // Words many races share, which can't pick out a race by themselves (already without a plural "s").
        private static readonly HashSet<string> Generic =
        [
            "run", "race", "walk", "marathon", "half", "full", "ultra", "trail", "road", "fun", "mile", "k", "km", "relay",
            "virtual", "kid", "kids", "family", "challenge", "classic", "event", "series", "dash", "jog", "trot", "triathlon",
        ];

        /// <summary>How many days apart the event and the race can be and still count as the same date.</summary>
        private const int CloseDays = 3;
        /// <summary>How many days apart they can be and still count as the same race season.</summary>
        private const int NearDays = 45;

        public static RsuRace? BestMatch(Event theEvent, IEnumerable<RsuRace> races)
        {
            HashSet<string> eventWords = Words(theEvent.Name);
            bool hasDate = EventDates.TryParse(theEvent.Date, out DateTime eventDate);
            List<(RsuRace Race, double Score, double Extra, int Days)> scored = [];
            foreach (RsuRace race in races)
            {
                HashSet<string> raceWords = Words(race.Name);
                // At least one shared word has to say which race it is, not just what kind ("Half Marathon").
                if (!eventWords.Any(w => raceWords.Contains(w) && !Generic.Contains(w) && !w.Any(char.IsDigit))) continue;
                double name = NameScore(eventWords, raceWords);
                int? days = hasDate ? DaysApart(eventDate, race) : null;
                double score = Score(name, days);
                if (score <= 0) continue;
                // For ties: fewer words the event doesn't have ("Virtual ..."), then the closer date.
                double extra = raceWords.Count == 0 ? 1 : (double)raceWords.Count(w => !eventWords.Contains(w)) / raceWords.Count;
                scored.Add((race, score, extra, days ?? int.MaxValue));
            }
            if (scored.Count == 0) return null;
            scored.Sort((a, b) => b.Score != a.Score ? b.Score.CompareTo(a.Score)
                : a.Extra != b.Extra ? a.Extra.CompareTo(b.Extra) : a.Days.CompareTo(b.Days));
            // Races that can't be told apart by any of these mean it doesn't guess.
            if (scored.Count > 1 && scored[0].Score == scored[1].Score && scored[0].Extra == scored[1].Extra && scored[0].Days == scored[1].Days)
            {
                return null;
            }
            return scored[0].Race;
        }

        /// <summary>
        /// 0 when the race isn't close. A name match needs at least half of the event's words in the
        /// race's name; on the same day half is enough, within the season most of them, and with no
        /// date nearly all of them.
        /// </summary>
        internal static double Score(double name, int? days)
        {
            if (name < 0.5) return 0;
            if (days is int d && d <= CloseDays) return name + 1;
            if (days is int n && n <= NearDays) return name >= 0.67 ? name + 0.5 : 0;
            return name >= 0.9 ? name : 0;
        }

        /// <summary>How much of the event's name is in the race's name (0 to 1).</summary>
        internal static double NameScore(HashSet<string> eventWords, HashSet<string> raceWords)
        {
            if (eventWords.Count == 0) return 0;
            return (double)eventWords.Count(raceWords.Contains) / eventWords.Count;
        }

        /// <summary>Days between the event and whichever of the race's next or last date is closer.</summary>
        private static int? DaysApart(DateTime eventDate, RsuRace race)
        {
            int? best = null;
            foreach (string date in new[] { race.NextDate, race.LastDate })
            {
                if (date.Length > 0 && RunsignupDownload.TryStartDate(date, out DateTime raceDate))
                {
                    int days = (int)Math.Abs((raceDate - eventDate).TotalDays);
                    if (best == null || days < best) best = days;
                }
            }
            return best;
        }

        /// <summary>
        /// A name's words in lower case, without years, ordinals ("40th"), filler words, punctuation or a
        /// plural "s", and with "&amp;" read as "and".
        /// </summary>
        internal static HashSet<string> Words(string name)
        {
            HashSet<string> words = [];
            foreach (string word in NonWord().Split(name.ToLowerInvariant().Replace("&", " and ").Replace("'", "").Replace("’", "")))
            {
                if (word.Length == 0 || Filler.Contains(word) || Year().IsMatch(word) || Ordinal().IsMatch(word)) continue;
                // "Runs" and "Run" are the same race.
                words.Add(word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss") ? word[..^1] : word);
            }
            return words;
        }

        [GeneratedRegex(@"[^\p{L}\p{N}]+")]
        private static partial Regex NonWord();

        [GeneratedRegex(@"^(19|20)\d\d$")]
        private static partial Regex Year();

        [GeneratedRegex(@"^\d+(st|nd|rd|th)$")]
        private static partial Regex Ordinal();
    }
}
