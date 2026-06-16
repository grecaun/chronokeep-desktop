using System;

namespace Chronokeep.Objects
{
    public class AnnouncerParticipant(Participant person, long seconds)
    {
        private readonly long seconds = seconds;

        public static Event? TheEvent { get; set; }
        public DateTime When => Constants.Timing.RFIDEpochToDate(seconds);
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
