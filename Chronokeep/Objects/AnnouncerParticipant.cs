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
using System.Linq;

namespace Chronokeep.Objects
{
    /// <summary>
    /// One row in the announcer list: a runner seen at the announcer location or crossing the finish line.
    /// The person is null when the bib isn't registered to anyone.
    /// </summary>
    public class AnnouncerParticipant(string bib, Participant? person, long seconds)
    {
        private readonly long seconds = seconds;
        private readonly Participant? person = person;

        public AnnouncerParticipant(Participant person, long seconds) : this(person.Bib, person, seconds) { }

        public static Event? TheEvent { get; set; }
        public DateTime When => Constants.Timing.RfidEpochToDate(seconds);
        public string AnnouncerWhen => When.ToString("HH:mm:ss");
        public string Distance => person?.Distance ?? "";
        public string Bib { get; } = bib;
        public string ParticipantName => person == null ? "" : $"{person.FirstName} {person.LastName}";
        public string CityState => person == null ? "" : $"{person.City} {person.State}";
        public string AgeGender => person == null ? "" : TheEvent == null ? $"? {person.Gender}" : $"{person.Age(TheEvent.Date)} {person.Gender}";
        public string Comments => person?.Comments ?? "";

        public int CompareTo(AnnouncerParticipant other)
        {
            return seconds.CompareTo(other.seconds);
        }

        /// <summary>
        /// Combines the announcer-location rows with the finishers into one list, newest first. Only rows at or
        /// after the cutoff are kept, and each bib shows once, at its latest time, so a runner read at the
        /// announcer location who then finishes isn't listed twice.
        /// </summary>
        public static List<AnnouncerParticipant> Merge(
            IEnumerable<AnnouncerParticipant> announced,
            IEnumerable<TimeResult> results,
            Func<string, Participant?> findParticipant,
            DateTime cutoff,
            bool includeUnknown)
        {
            long cutoffSeconds = Constants.Timing.RfidDateToEpoch(cutoff);
            return [.. announced
                .Concat(results
                    .Where(r => !TimeResult.IsNotFinish(r) && !r.IsDnf() && r.Bib != Constants.Timing.CHIPREAD_DUMMYBIB)
                    .Select(r => new AnnouncerParticipant(r.Bib, null, Constants.Timing.RfidDateToEpoch(r.SystemTime))))
                .Where(x => x.seconds >= cutoffSeconds)
                .GroupBy(x => x.Bib)
                .Select(g => new AnnouncerParticipant(g.Key,
                    g.Select(x => x.person).FirstOrDefault(p => p != null) ?? findParticipant(g.Key),
                    g.Max(x => x.seconds)))
                .Where(x => includeUnknown || x.person != null)
                .OrderByDescending(x => x.seconds)];
        }
    }
}
