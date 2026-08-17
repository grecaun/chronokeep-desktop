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

namespace Chronokeep.Objects
{
    internal class StatsParticipant
    {
        private readonly Participant participant;
        public string LastSeen { get; }
        public string LastSeenTime { get; }
        public string Bib => participant.Bib;
        public string FirstName => participant.FirstName;
        public string LastName => participant.LastName;
        public string Gender => participant.Gender;
        public string Phone => participant.Phone;
        public string Mobile => participant.Mobile;
        public string Email => participant.Email;
        public string CurrentAge => participant.CurrentAge;

        internal StatsParticipant(Participant participant, string lastSeen, string lastSeenTime)
        {
            this.participant = participant;
            LastSeen = lastSeen;
            LastSeenTime = lastSeenTime;
        }

        public Participant GetParticipant()
        {
            return participant;
        }
    }
}

