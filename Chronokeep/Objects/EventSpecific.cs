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
    public class EventSpecific
    {
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
            bool smsEnabled,
            string apparel,
            string division
            )
        {
            EventIdentifier = eid;
            DistanceIdentifier = did;
            DistanceName = distanceName;
            Bib = bib;
            CheckedIn = ci == 0 ? 0 : 1;
            Comments = comments;
            Owes = owes;
            Other = other;
            Anonymous = anonymous;
            SmsEnabled = smsEnabled;
            Apparel = apparel;
            Division = division;
            Version = Constants.Timing.EVENTSPECIFIC_DEFAULT_VERSION;
            UploadedVersion = Constants.Timing.EVENTSPECIFIC_DEFAULT_VERSION;
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
            bool smsEnabled,
            string apparel,
            string division,
            int version,
            int uploadedVersion
            )
        {
            Identifier = id;
            EventIdentifier = eid;
            DistanceIdentifier = did;
            DistanceName = distanceName;
            Bib = bib;
            CheckedIn = ci != 0 ? 1 : 0;
            Owes = owes;
            Other = other;
            Comments = comments;
            Status = status;
            AgeGroupName = ageGroupName;
            AgeGroupId = ageGroupId;
            Anonymous = anonymous;
            SmsEnabled = smsEnabled;
            Apparel = apparel;
            Division = division;
            Version = version;
            UploadedVersion = uploadedVersion;
        }

        internal void Trim()
        {
            DistanceName = DistanceName.Trim();
            Bib = Bib.Trim();
            Owes = Owes.Trim();
            Other = Other.Trim();
            Comments = Comments.Trim();
            AgeGroupName = AgeGroupName.Trim();
            Apparel = Apparel.Trim();
            Division = Division.Trim();
        }

        internal static EventSpecific Blank()
        {
            return new EventSpecific(-1, -1, -1, "None", "", 0, "", "", "", 0, "", Constants.Timing.TIMERESULT_DUMMYAGEGROUP, false, false, "", "", Constants.Timing.EVENTSPECIFIC_DEFAULT_VERSION, Constants.Timing.EVENTSPECIFIC_DEFAULT_VERSION);
        }

        public int Identifier { get; set; }
        public int EventIdentifier { get; set; }
        public int DistanceIdentifier { get; set; }
        public string Bib { get; set; }
        public int CheckedIn { get; private set; }
        public string Comments { get; private set; }
        public string DistanceName { get; set; }
        public string Owes { get; private set; }
        public string Other { get; private set; }
        public int Status { get; set; } = Constants.Timing.EVENTSPECIFIC_UNKNOWN;
        public string StatusStr => Constants.Timing.EVENTSPECIFIC_STATUS_NAMES[Status];
        public string AgeGroupName { get; set; } = "";
        public int AgeGroupId { get; set; } = Constants.Timing.TIMERESULT_DUMMYAGEGROUP;
        public bool Anonymous { get; private set; }

        public bool SmsEnabled { get; set; }
        public string Apparel { get; private set; }
        public string Division { get; private set; }
        public int Version { get; set; }
        public int UploadedVersion { get; set; }

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
            SmsEnabled = other.SmsEnabled;
            Apparel = other.Apparel;
            Division = other.Division;
            Version = other.Version;
            UploadedVersion = other.UploadedVersion;
        }

        public bool Equals(EventSpecific other)
        {
            return EventIdentifier == other.EventIdentifier
                   && DistanceIdentifier == other.DistanceIdentifier
                   && Bib == other.Bib
                   && Comments == other.Comments
                   && Owes == other.Owes
                   && Other == other.Other
                   && Anonymous == other.Anonymous
                   && SmsEnabled == other.SmsEnabled
                   && Apparel == other.Apparel
                   && Division == other.Division;
        }
    }
}

