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
