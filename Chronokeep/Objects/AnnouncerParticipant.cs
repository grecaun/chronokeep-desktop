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

namespace Chronokeep.Objects
{
    public class AnnouncerParticipant(Participant person, long seconds)
    {
        private readonly long seconds = seconds;

        public static Event? TheEvent { get; set; }
        public DateTime When => Constants.Timing.RfidEpochToDate(seconds);
        public string AnnouncerWhen => When.ToString("HH:mm:ss");
        public string Distance => person.Distance;
        public string Bib => person.Bib;
        public string ParticipantName => $"{person.FirstName} {person.LastName}";
        public string CityState => $"{person.City} {person.State}";
        public string AgeGender => TheEvent == null ? $"? {person.Gender}" : $"{person.Age(TheEvent.Date)} {person.Gender}";
        public string Comments => person.Comments;

        public int CompareTo(AnnouncerParticipant other)
        {
            return seconds.CompareTo(other.seconds);
        }
    }
}

