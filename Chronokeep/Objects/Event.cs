using System;
using Chronokeep.Constants;

namespace Chronokeep.Objects
{
    public class Event : IEquatable<Event>, IComparable<Event>
    {
        private int commonAgeGroups = 1, commonStartFinish = 1, distanceSpecificSegments, rankByGun = 1;
        private int displayPlacements = 1, divisionsEnabled, uploadSpecific, useMaleFemale = 0;

        public Event() { }

        public Event(string n, long d, string yearCode)
        {
            DateTime time = new(d);
            Date = time.ToShortDateString();
            Name = n;
            YearCode = yearCode;
            StartWindow = 600;
            FinishIgnoreWithin = 600;
        }

        public Event(int id, string n, long d)
        {
            Identifier = id;
            Name = n;
            DateTime time = new(d);
            Date = time.ToShortDateString();
            StartWindow = 600;
            FinishIgnoreWithin = 600;
        }

        public Event(string n, long d, int age, int start, int seg, int gun)
        {
            DateTime time = new(d);
            Date = time.ToShortDateString();
            Name = n;
            commonAgeGroups = age;
            commonStartFinish = start;
            distanceSpecificSegments = seg;
            rankByGun = gun;
            StartWindow = 600;
            FinishIgnoreWithin = 600;
        }

        public Event(int id, string n, long d, int age, int start, int seg, int gun)
        {
            Identifier = id;
            Name = n;
            DateTime time = new(d);
            Date = time.ToShortDateString();
            commonAgeGroups = age;
            commonStartFinish = start;
            distanceSpecificSegments = seg;
            rankByGun = gun;
            StartWindow = 600;
            FinishIgnoreWithin = 600;
        }

        public Event(int id, string n, string d, int age, int start, int seg,
            int gun, string yearCode, int maxOcc, int ignWith, int window,
            long startSec, int startMill, int type, int apiId,
            string apiEventId, int displayPlacements, int ageGroupsAsDivisions,
            int daysAllowed, int upSpecific, int startMaxOccurrences, bool useMaleFemale)
        {
            Identifier = id;
            Name = n;
            DateTime time = DateTime.Parse(d);
            Date = time.ToShortDateString();
            commonAgeGroups = age;
            commonStartFinish = start;
            distanceSpecificSegments = seg;
            rankByGun = gun;
            YearCode = yearCode;
            FinishMaxOccurrences = maxOcc;
            FinishIgnoreWithin = ignWith;
            StartWindow = window;
            StartSeconds = startSec;
            StartMilliseconds = startMill;
            EventType = type;
            ApiId = apiId;
            ApiEventId = apiEventId;
            this.displayPlacements = displayPlacements;
            divisionsEnabled = ageGroupsAsDivisions;
            DaysAllowed = daysAllowed;
            uploadSpecific = upSpecific;
            StartMaxOccurrences = startMaxOccurrences;
            UseMaleFemale = useMaleFemale;
        }

        public int Identifier { get; set; }
        public string Name { get; set; } = "";
        public string Date { get; set; } = "";
        public string LongDate => DateTime.Parse(Date).ToString("MMMM d, yyyy");
        public bool CommonAgeGroups { get => commonAgeGroups != 0; set => commonAgeGroups = value ? 1 : 0; }
        public bool CommonStartFinish { get => commonStartFinish != 0; set => commonStartFinish = value ? 1 : 0; }
        public bool DistanceSpecificSegments { get => distanceSpecificSegments != 0; set => distanceSpecificSegments = value ? 1 : 0; }
        public bool RankByGun { get => rankByGun != 0; set => rankByGun = value ? 1 : 0; }
        public string YearCode { get; set; } = "";
        public string Year => Date.Split('/').Length == 3 ? Date.Split('/')[2] : "";
        public int StartWindow { get; set; } = -1;
        public int FinishMaxOccurrences { get; set; } = 1;
        public int FinishIgnoreWithin { get; set; }
        public long StartSeconds { get; set; } = -1;
        public int StartMaxOccurrences { get; set; } = 1;
        public int StartMilliseconds { get; set; }
        public int EventType { get; set; } = Constants.Timing.EVENT_TYPE_DISTANCE;
        public int ApiId { get; set; } = ApiConstants.NULL_ID;
        public string ApiEventId { get; set; } = ApiConstants.NULL_EVENT_ID;
        public bool DisplayPlacements { get => displayPlacements != 0; set => displayPlacements = value ? 1 : 0; }
        public bool DivisionsEnabled { get => divisionsEnabled != 0;
            private set => divisionsEnabled = value ? 1 : 0; }
        public int DaysAllowed { get; private set; } = 1;
        public bool UploadSpecific { get => uploadSpecific != 0; set => uploadSpecific = value ? 1 : 0; }
        public bool UseMaleFemale { get => useMaleFemale != 0; set => useMaleFemale = value ? 1 : 0; }

        public string EventTypeString
        {
            get
            {
                if (EventType == Constants.Timing.EVENT_TYPE_TIME)
                {
                    return "Time Based";
                }
                return EventType == Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA ? "Backyard Ultra" : "Distance Based";
            }
        }

        public int CompareTo(Event? other)
        {
            if (other == null) return 1;
            DateTime thisDate = DateTime.Parse(Date);
            DateTime otherDate = DateTime.Parse(other.Date);
            return thisDate.CompareTo(otherDate) * -1;
        }

        public bool Equals(Event? other)
        {
            if (other == null) return false;
            return Date == other.Date && Name == other.Name || Identifier == other.Identifier;
        }

        public void CopyFrom(Event other)
        {
            EventType = other.EventType;
            StartWindow = other.StartWindow;
            StartMaxOccurrences = other.StartMaxOccurrences;
            FinishIgnoreWithin = other.FinishIgnoreWithin;
            FinishMaxOccurrences = other.FinishMaxOccurrences;
            CommonAgeGroups = other.CommonAgeGroups;
            CommonStartFinish = other.CommonStartFinish;
            DistanceSpecificSegments = other.DistanceSpecificSegments;
            DisplayPlacements = other.DisplayPlacements;
            DivisionsEnabled = other.DivisionsEnabled;
            DaysAllowed = other.DaysAllowed;
            RankByGun = other.RankByGun;
            UseMaleFemale = other.UseMaleFemale;
        }

        public void CopyAll(Event other)
        {
            Name = other.Name;
            Date = other.Date;
            CommonAgeGroups = other.CommonAgeGroups;
            CommonStartFinish = other.CommonStartFinish;
            DistanceSpecificSegments = other.DistanceSpecificSegments;
            RankByGun = other.RankByGun;
            YearCode = other.YearCode;
            StartWindow = other.StartWindow;
            FinishMaxOccurrences = other.FinishMaxOccurrences;
            FinishIgnoreWithin = other.FinishIgnoreWithin;
            StartSeconds = other.StartSeconds;
            StartMaxOccurrences = other.StartMaxOccurrences;
            StartMilliseconds = other.StartMilliseconds;
            EventType = other.EventType;
            ApiId = other.ApiId;
            ApiEventId = other.ApiEventId;
            DisplayPlacements = other.DisplayPlacements;
            DivisionsEnabled = other.DivisionsEnabled;
            DaysAllowed = other.DaysAllowed;
            UploadSpecific = other.UploadSpecific;
            UseMaleFemale = other.UseMaleFemale;
        }
    }
}
