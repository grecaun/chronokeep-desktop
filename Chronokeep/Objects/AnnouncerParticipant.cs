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
        // The finish result when this pass includes the runner finishing.
        private TimeResult? finish;

        public AnnouncerParticipant(Participant person, long seconds) : this(person.Bib, person, seconds) { }

        public static Event? TheEvent { get; set; }
        // Read times are this computer's local time, though the RFID epoch they're counted from is marked UTC.
        public DateTime When => DateTime.SpecifyKind(Constants.Timing.RfidEpochToDate(seconds), DateTimeKind.Local);
        public string Distance => person?.Distance ?? "";
        public string Bib { get; } = bib;
        // Anonymous runners keep their bib, distance and age, but nothing that names them or where they're from.
        private bool Hidden => person == null || person.Anonymous;
        public string ParticipantName => person == null ? "" : person.Anonymous ? "Anonymous" : $"{person.FirstName} {person.LastName}";
        public string CityState => Hidden ? "" : $"{person!.City} {person.State}";
        // The gender is shown as its short code (see TimeResult.ShortGender), and left off when it isn't specified.
        public string AgeGender => person == null ? "" : $"{(TheEvent == null ? "?" : person.Age(TheEvent.Date))} {TimeResult.ShortGender(person.Gender)}".TrimEnd();
        public string Comments => Hidden ? "" : person!.Comments;

        /// <summary>The runner's finish time, once they've finished in this pass; blank until then.</summary>
        public string FinishTime => finish?.Time.Split('.')[0] ?? "";

        public int CompareTo(AnnouncerParticipant other)
        {
            return seconds.CompareTo(other.seconds);
        }

        /// <summary>
        /// Combines the announcer-location rows with the finishers into one list, newest first. Each runner's
        /// sightings are grouped into passes: a sighting within repeatMinutes of the start of their last pass
        /// is part of it, and a repeatMinutes of 0 means a runner only ever has one pass. Each pass shows once,
        /// at the time the runner was first seen, and only passes starting at or after the cutoff are listed.
        /// </summary>
        public static List<AnnouncerParticipant> Merge(
            IEnumerable<AnnouncerParticipant> announced,
            IEnumerable<TimeResult> results,
            Func<string, Participant?> findParticipant,
            DateTime cutoff,
            int repeatMinutes,
            bool includeUnknown)
        {
            long cutoffSeconds = Constants.Timing.RfidDateToEpoch(cutoff);
            List<AnnouncerParticipant> output = [];
            foreach (IGrouping<string, AnnouncerParticipant> sightings in announced
                .Concat(results
                    .Where(r => !TimeResult.IsNotFinish(r) && !r.IsDnf() && r.Bib != Constants.Timing.CHIPREAD_DUMMYBIB)
                    .Select(r => new AnnouncerParticipant(r.Bib, null, Constants.Timing.RfidDateToEpoch(r.SystemTime)) { finish = r }))
                .GroupBy(x => x.Bib))
            {
                Participant? person = sightings.Select(x => x.person).FirstOrDefault(p => p != null);
                AnnouncerParticipant? pass = null;
                foreach (AnnouncerParticipant seen in sightings.OrderBy(x => x.seconds))
                {
                    if (pass != null && (repeatMinutes == 0 || seen.seconds - pass.seconds < repeatMinutes * 60L))
                    {
                        // Part of the pass already listed (or left out); it keeps the first time, but takes a finish.
                        pass.finish ??= seen.finish;
                        continue;
                    }
                    bool recent = seen.seconds >= cutoffSeconds;
                    if (recent) person ??= findParticipant(sightings.Key);
                    pass = new AnnouncerParticipant(sightings.Key, person, seen.seconds) { finish = seen.finish };
                    if (recent && (includeUnknown || person != null))
                    {
                        output.Add(pass);
                    }
                }
            }
            return [.. output.OrderByDescending(x => x.seconds)];
        }
    }
}
