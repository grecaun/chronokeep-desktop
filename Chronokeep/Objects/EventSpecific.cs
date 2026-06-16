namespace Chronokeep.Objects
{
    public class EventSpecific
    {
        private int identifier, eventIdentifier, distanceIdentifier,
            checkedIn = 0,
            status = Constants.Timing.EVENTSPECIFIC_UNKNOWN,
            ageGroupId = Constants.Timing.TIMERESULT_DUMMYAGEGROUP,
            version = 0,
            uploaded_version = -1;
        private string comments = "", distanceName = "", owes = "", other = "", ageGroupName = "", bib = "", apparel = "", division = "";
        private bool anonymous, sms_enabled;

        public EventSpecific() { }

        // Constructor to be used when adding to db
        public EventSpecific(
            int eid,
            int did,
            string distanceName,
            string bib,
            int ci,
            string comments,
            string owes,
            string other,
            bool anonymous,
            bool sms_enabled,
            string apparel,
            string division
            )
        {
            eventIdentifier = eid;
            distanceIdentifier = did;
            this.distanceName = distanceName ?? "";
            this.bib = bib ?? "";
            checkedIn = ci == 0 ? 0 : 1;
            this.comments = comments ?? "";
            this.owes = owes ?? "";
            this.other = other ?? "";
            this.anonymous = anonymous;
            this.sms_enabled = sms_enabled;
            this.apparel = apparel ?? "";
            this.division = division ?? "";
            version = Constants.Timing.EVENTSPECIFIC_DEFAULT_VERSION;
            uploaded_version = Constants.Timing.EVENTSPECIFIC_DEFAULT_UPLOADED_VERSION;
        }

        // Constructor the database uses
        public EventSpecific(
            int id,
            int eid,
            int did,
            string distanceName,
            string bib,
            int ci,
            string comments,
            string owes,
            string other,
            int status,
            string ageGroupName,
            int ageGroupId,
            bool anonymous,
            bool sms_enabled,
            string apparel,
            string division,
            int version,
            int uploaded_version
            )
        {
            identifier = id;
            eventIdentifier = eid;
            distanceIdentifier = did;
            this.distanceName = distanceName ?? "";
            this.bib = bib ?? "";
            checkedIn = ci != 0 ? 1 : 0;
            this.owes = owes ?? "";
            this.other = other ?? "";
            this.comments = comments ?? "";
            this.status = status;
            this.ageGroupName = ageGroupName ?? "";
            this.ageGroupId = ageGroupId;
            this.anonymous = anonymous;
            this.sms_enabled = sms_enabled;
            this.apparel = apparel ?? "";
            this.division = division ?? "";
            this.version = version;
            this.uploaded_version = uploaded_version;
        }

        internal void Trim()
        {
            distanceName = distanceName.Trim();
            bib = bib.Trim();
            owes = owes.Trim();
            other = other.Trim();
            comments = comments.Trim();
            ageGroupName = ageGroupName.Trim();
            apparel = apparel.Trim();
            division = division.Trim();
        }

        internal static EventSpecific Blank()
        {
            return new EventSpecific(-1, -1, -1, "None", "", 0, "", "", "", 0, "", Constants.Timing.TIMERESULT_DUMMYAGEGROUP, false, false, "", "", Constants.Timing.EVENTSPECIFIC_DEFAULT_VERSION, Constants.Timing.EVENTSPECIFIC_DEFAULT_UPLOADED_VERSION);
        }

        public int Identifier { get => identifier; set => identifier = value; }
        public int EventIdentifier { get => eventIdentifier; set => eventIdentifier = value; }
        public int DistanceIdentifier { get => distanceIdentifier; set => distanceIdentifier = value; }
        public string Bib { get => bib; set => bib = value ?? ""; }
        public int CheckedIn { get => checkedIn; set => checkedIn = value; }
        public string Comments { get => comments; set => comments = value ?? ""; }
        public string DistanceName { get => distanceName; set => distanceName = value ?? ""; }
        public string Owes { get => owes; set => owes = value ?? ""; }
        public string Other { get => other; set => other = value ?? ""; }
        public int Status { get => status; set => status = value; }
        public string StatusStr { get => Constants.Timing.EVENTSPECIFIC_STATUS_NAMES[status]; }
        public string AgeGroupName { get => ageGroupName; set => ageGroupName = value ?? ""; }
        public int AgeGroupId { get => ageGroupId; set => ageGroupId = value; }
        public bool Anonymous { get => anonymous; set => anonymous = value; }
        public bool SMSEnabled { get => sms_enabled; set => sms_enabled = value; }
        public string Apparel { get => apparel; set => apparel = value ?? ""; }
        public string Division { get => division; set => division = value ?? ""; }
        public int Version { get => version; set => version = value; }
        public int UploadedVersion { get => uploaded_version; set => uploaded_version = value; }

        public void CopyFrom(EventSpecific other)
        {
            EventIdentifier = other.EventIdentifier;
            DistanceIdentifier = other.DistanceIdentifier;
            Bib = other.Bib;
            CheckedIn = other.CheckedIn;
            Comments = other.Comments;
            DistanceName = other.DistanceName;
            Owes = other.Owes;
            Other = other.Other;
            Status = other.Status;
            AgeGroupName = other.AgeGroupName;
            AgeGroupId = other.AgeGroupId;
            Anonymous = other.Anonymous;
            SMSEnabled = other.SMSEnabled;
            Apparel = other.Apparel;
            Division = other.Division;
            version = other.version;
            uploaded_version = other.uploaded_version;
        }

        public bool Equals(EventSpecific other)
        {
            if (other == null) return false;
            return EventIdentifier == other.EventIdentifier
                && DistanceIdentifier == other.DistanceIdentifier
                && Bib == other.Bib
                && Comments == other.Comments
                && Owes == other.Owes
                && Other == other.Other
                && Anonymous == other.Anonymous
                && SMSEnabled == other.SMSEnabled
                && Apparel == other.Apparel
                && Division == other.Division;
        }
    }
}
