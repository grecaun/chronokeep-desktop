using Chronokeep.Helpers;
using Chronokeep.Timing;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Chronokeep.Constants;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Chronokeep.Objects
{
    public partial class TimeResult : IEquatable<TimeResult>
    {
        private int uploaded;
        private string ageGroupName = "";

        private DateTime systemTime;
        private Event? theEvent;

        [GeneratedRegex(@"(\d+):(\d{2}):(\d{2})\.(\d{3})")]
        private static partial Regex TimeRegex();

        // database constructor
        public TimeResult(
            int eventId,
            int eventspecificId,
            int locationId,
            int segmentId,
            string time,
            int occurrence,
            string first,
            string last,
            string distance,
            string bib,
            int readId,
            string unknownId,
            long systemTimeSec,
            int systemTimeMill,
            string chipTime,
            int place,
            int agePlace,
            int genderPlace,
            string gender,
            int status,
            string split,
            int ageGroupId,
            string ageGroupName,
            int uploaded,
            string birthday,
            int type,
            string linkedDistanceName,
            string chip,
            bool anonymous,
            string participantId,
            Dictionary<int, TimingLocation> locations,
            Dictionary<int, Segment> segments,
            Dictionary<string, Distance> distances,
            Event theEvent,
            string division,
            int divisionPlace
            )
        {
            EventIdentifier = eventId;
            EventSpecificId = eventspecificId;
            LocationId = locationId;
            SegmentId = segmentId;
            Time = time;
            Occurrence = occurrence;
            LocationName = locations.TryGetValue(locationId, out TimingLocation? loc) ? loc.Name : "Unknown";
            if (Constants.Timing.SEGMENT_FINISH == SegmentId)
            {
                SegmentName = "Finish ";
            }
            else if (Constants.Timing.SEGMENT_START == SegmentId)
            {
                SegmentName = "Start ";
            }
            else if (segments.TryGetValue(SegmentId, out Segment? seg))
            {
                SegmentName = seg.Name + " ";
            }
            else
            {
                SegmentName = "";
            }
            if (theEvent.EventType == Constants.Timing.EVENT_TYPE_TIME)
            {
                if (Constants.Timing.SEGMENT_FINISH == SegmentId)
                {
                    if (linkedDistanceName.Length > 0
                        && distances.TryGetValue(linkedDistanceName, out Distance? linkedDist)
                        && linkedDist.DistanceValue > 0)
                    {
                        SegmentName = string.Format("{1:0.##} {2} - Lap {0}",
                            occurrence,
                            linkedDist.DistanceValue * occurrence,
                            Distances.DistanceString(linkedDist.DistanceUnit)
                            );
                    }
                    else if (distance.Length > 0
                        && distances.TryGetValue(distance, out Distance? oDist)
                        && oDist.DistanceValue > 0)
                    {
                        SegmentName = string.Format("{1:0.##} {2} - Lap {0}",
                            occurrence,
                            oDist.DistanceValue * occurrence,
                            Distances.DistanceString(oDist.DistanceUnit)
                            );
                    }
                    else
                    {
                        SegmentName = $"Lap {occurrence}";
                    }
                }
                else if (Constants.Timing.SEGMENT_START != SegmentId)
                {
                    if (linkedDistanceName.Length > 0
                        && segments.TryGetValue(SegmentId, out Segment? oSeg)
                        && oSeg.CumulativeDistance > 0)
                    {
                        SegmentName = string.Format("{2:0.##} {3} - {0}{1}",
                            SegmentName,
                            occurrence,
                            oSeg.CumulativeDistance * occurrence,
                            Distances.DistanceString(oSeg.DistanceUnit)
                            );
                    }
                    else
                    {
                        SegmentName = $"{SegmentName} {occurrence}";
                    }
                }
            }
            else if (Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA == theEvent.EventType)
            {
                int hour = (Occurrence / 2) + 1;
                if (Constants.Timing.SEGMENT_FINISH == SegmentId)
                {
                    if (linkedDistanceName.Length > 0
                        && distances.TryGetValue(linkedDistanceName, out Distance? linkedDist)
                        && linkedDist.DistanceValue > 0)
                    {
                        SegmentName = string.Format("{1:0.##} {2} - Hour {0}",
                            hour,
                            linkedDist.DistanceValue * hour,
                            Distances.DistanceString(linkedDist.DistanceUnit)
                            );
                    }
                    else if (distance.Length > 0
                        && distances.TryGetValue(distance, out Distance? oDist)
                        && oDist.DistanceValue > 0)
                    {
                        SegmentName = string.Format("{1:0.##} {2} - Hour {0}",
                            hour,
                            oDist.DistanceValue * hour,
                            Distances.DistanceString(oDist.DistanceUnit)
                            );
                    }
                    else
                    {
                        SegmentName = $"Hour {hour}";
                    }
                }
                else if (Constants.Timing.SEGMENT_START == SegmentId)
                {
                    SegmentName = $"Start {hour}";
                }
            }
            SegmentName = SegmentName.Trim();
            First = first;
            Last = last;
            RealDistanceName = distance;
            Bib = bib;
            UnknownId = unknownId;
            ReadId = readId;
            systemTime = Constants.Timing.RFIDEpochToDate(systemTimeSec).AddMilliseconds(systemTimeMill);
            this.ChipTime = chipTime;
            Place = place;
            AgePlace = agePlace;
            GenderPlace = genderPlace;
            this.Gender = gender;
            AgeGroupId = ageGroupId;
            this.ageGroupName = ageGroupName;
            Match chipTimeMatch = TimeRegex().Match(chipTime);
            ChipSeconds = 0;
            ChipMilliseconds = 0;
            if (chipTimeMatch.Success)
            {
                ChipSeconds = Convert.ToInt64(chipTimeMatch.Groups[1].Value) * 3600
                   + Convert.ToInt64(chipTimeMatch.Groups[2].Value) * 60
                   + Convert.ToInt64(chipTimeMatch.Groups[3].Value);
                ChipMilliseconds = Convert.ToInt32(chipTimeMatch.Groups[4].Value);
            }
            Match timeMatch = TimeRegex().Match(time);
            Seconds = 0;
            Milliseconds = 0;
            if (timeMatch.Success)
            {
                Seconds = Convert.ToInt64(timeMatch.Groups[1].Value) * 3600
                   + Convert.ToInt64(timeMatch.Groups[2].Value) * 60
                   + Convert.ToInt64(timeMatch.Groups[3].Value);
                Milliseconds = Convert.ToInt32(timeMatch.Groups[4].Value);
            }
            Status = status;
            LapTime = split;
            this.uploaded = uploaded;
            Birthday = birthday;
            Type = type;
            LinkedDistanceName = linkedDistanceName;
            Chip = chip;
            Anonymous = anonymous;
            ParticipantId = participantId;
            this.theEvent = theEvent;
            Division = division;
            DivisionPlace = divisionPlace;
        }

        // Used by routines to add new results to the database.
        public TimeResult(
            int eventId,
            int readId,
            int eventspecificId,
            int locationId,
            int segmentId,
            int occurrence,
            long seconds,
            int milliseconds,
            string unknownId,
            long chipSeconds,
            int chipMilliseconds,
            DateTime systemTime,
            string bib,
            int status,
            string division
            )
        {
            EventIdentifier = eventId;
            ReadId = readId;
            EventSpecificId = eventspecificId;
            LocationId = locationId;
            SegmentId = segmentId;
            Occurrence = occurrence;
            Time = Constants.Timing.TIMERESULT_STATUS_DNF == status ? "DNF" : Constants.Timing.TIMERESULT_STATUS_DNS == status ? "DNS" : Constants.Timing.ToTime(seconds, milliseconds);
            UnknownId = unknownId;
            ChipTime = Constants.Timing.TIMERESULT_STATUS_DNF == status ? "DNF" : Constants.Timing.TIMERESULT_STATUS_DNS == status ? "DNS" : Constants.Timing.ToTime(chipSeconds, chipMilliseconds);
            this.systemTime = systemTime;
            Bib = bib;
            Place = Constants.Timing.TIMERESULT_DUMMYPLACE;
            AgePlace = Constants.Timing.TIMERESULT_DUMMYPLACE;
            GenderPlace = Constants.Timing.TIMERESULT_DUMMYPLACE;
            DivisionPlace = Constants.Timing.TIMERESULT_DUMMYPLACE;
            Status = status;
            LapTime = "";
            Seconds = seconds;
            Milliseconds = milliseconds;
            ChipSeconds = chipSeconds;
            ChipMilliseconds = chipMilliseconds;
            Division = division;
        }

        public int EventSpecificId { get; set; }
        public int LocationId { get; set; }
        public int EventIdentifier { get; set; }
        public int SegmentId { get; set; }
        public int Occurrence { get; set; }
        public string Time { get; set; }
        public string LocationName { get; set; } = "";
        public string SegmentName { get; set; } = "";
        public string First { get; private set; } = "";
        public string Last { get; private set; } = "";
        public string ParticipantName => $"{First} {Last}".Trim();
        public string PrettyParticipantName => Anonymous ? $"Bib {Bib}" : $"{First} {Last}".Trim();
        public string DistanceName => LinkedDistanceName == "" ? RealDistanceName : LinkedDistanceName;
        internal string LinkedDistanceName { get; set; } = "";
        public string RealDistanceName { get; internal set; } = "";
        public string Bib { get; set; }
        public int AgeGroupId { get; private set; }
        public string UnknownId { get; set; }
        public int ReadId { get; set; }
        public int Place { get; set; }
        public string PlaceStr => theEvent is { DisplayPlacements: true } ? Place < 1 ? "" : Place.ToString() : "";
        public string PrettyPlaceStr =>
            Type == Constants.Timing.DISTANCE_TYPE_EARLY && Place > 0 ? $"{Place}e" :
            Type == Constants.Timing.DISTANCE_TYPE_UNOFFICIAL && Place > 0 ? $"{Place}u" :
            Type == Constants.Timing.DISTANCE_TYPE_DROP && Place > 0 ? $"{Place}d" :
            Type == Constants.Timing.DISTANCE_TYPE_LATE && Place > 0 ? $"{Place}l" :
            Finish && Place > 0 ? Place.ToString() : "";
        public int AgePlace { get; set; }
        public string AgePlaceStr => theEvent is { DisplayPlacements: true } ? AgePlace < 1 ? "" : AgePlace.ToString() : "";
        public int GenderPlace { get; set; }
        public string GenderPlaceStr => theEvent is { DisplayPlacements: true } ? GenderPlace < 1 ? "" : GenderPlace.ToString() : "";
        public string Division { get; set; }
        public int DivisionPlace { get; set; }
        public string DivisionPlaceStr => theEvent is { DisplayPlacements: true } ? DivisionPlace < 1 ? "" : DivisionPlace.ToString() : "";
        public int Type { get; private set; }
        public string Identifier => UnknownId;
        public string PrettyType => PrettyTypeStr();
        public string PrettyGender => Gender == "Man" ? "M" : Gender == "Woman" ? "W" : Gender == "Non-Binary" ? "X" : Gender == "Not Specified" || Gender.Equals("ns", StringComparison.OrdinalIgnoreCase) ? "" : Gender.Length <= 2 ? Gender : Gender[..2];

        private string PrettyTypeStr()
        {
            string output = Type == Constants.Timing.DISTANCE_TYPE_EARLY ? "E"
                : Type == Constants.Timing.DISTANCE_TYPE_UNOFFICIAL ? "U"
                : Type == Constants.Timing.DISTANCE_TYPE_LATE ? "L"
                : Type == Constants.Timing.DISTANCE_TYPE_VIRTUAL ? "V"
                : Type == Constants.Timing.DISTANCE_TYPE_DROP ? "D"
                : "";
            return Anonymous ? "A" + output : output;
        }

        public DateTime SystemTime { get => systemTime; set => systemTime = value; }

        public string SysTime => systemTime.ToString("MMM dd HH:mm:ss.fff");
        public string ChipLapTime => theEvent != null && theEvent.EventType == Constants.Timing.EVENT_TYPE_TIME ? LapTime : ChipTime;
        public string ChipTime { get; set; }
        public string ChipTimeNoMilliseconds => ChipTime.Split('.').Length > 0 ? ChipTime.Split('.')[0] : ChipTime;
        public string Gender { get; private set; } = "";
        public string AgeGroupName { get => PrettyAgeGroupName(); set => ageGroupName = value; }
        public int Status { get; set; }
        public string LapTime { get; set; }
        public long ChipSeconds { get; set; }
        public int ChipMilliseconds { get; set; }
        public long Seconds { get; set; }
        public int Milliseconds { get; set; }
        public int Uploaded { get => uploaded; set => uploaded = value == Constants.Timing.TIMERESULT_UPLOADED_FALSE ? Constants.Timing.TIMERESULT_UPLOADED_FALSE : Constants.Timing.TIMERESULT_UPLOADED_TRUE; }
        private string Birthday { get; set; } = "";
        public string Chip { get; private set; } = "";
        public bool Anonymous { get; private set; }
        public string AgeGenderString => theEvent != null ? $"{Age(theEvent.Date)} {PrettyGender}" : $"? {PrettyGender}";
        public bool Finish => SegmentId == Constants.Timing.SEGMENT_FINISH;
        public string ParticipantId { get; private set; } = "";

        public string PrettyAgeGroupName()
        {
            string[] agSplit = ageGroupName.Split('-');
            if (agSplit.Length <= 1 || agSplit[0] != "0") return ageGroupName;
            if (int.TryParse(agSplit[1], out int topAge) && topAge > 0)
            {
                return $"Under {topAge + 1}";
            }
            return topAge >= 99 ? $"Over {agSplit[0]}" : ageGroupName;
        }

        public static string BibToIdentifier(string iBib)
        {
            return "Bib:" + iBib;
        }
        public static string ChipToIdentifier(string iChip)
        {
            return "Chip:" + iChip;
        }

        public void SetParticipant(Participant p)
        {
            ParticipantId = p.Identifier.ToString();
            Anonymous = p.Anonymous;
            RealDistanceName = p.EventSpecific.DistanceName;
            Gender = p.Gender;
            First = p.FirstName;
            Last = p.LastName;
            AgeGroupId = p.EventSpecific.AgeGroupId;
            ageGroupName = p.EventSpecific.AgeGroupName;
            Birthday = p.Birthdate;
        }

        public void SetBlankParticipant()
        {
            ParticipantId = "";
            Anonymous = false;
            RealDistanceName = "";
            Gender = "";
            First = "";
            Last = "";
            AgeGroupId = -1;
            ageGroupName = "";
            Birthday = "";
            LinkedDistanceName = "";
        }

        public void SetLinkedDistanceName(string linkedDistanceName)
        {
            LinkedDistanceName = linkedDistanceName;
        }

        public void SetResultType(int type)
        {
            Type = type;
        }

        public void SetChipRead(
            string chip,
            string bib,
            long systemTimeSec,
            int systemTimeMill
            )
        {
            Chip = chip;
            Bib = bib;
            systemTime = Constants.Timing.RFIDEpochToDate(systemTimeSec).AddMilliseconds(systemTimeMill);
        }

        public void SetFinalValues(
            Dictionary<int, TimingLocation> locations,
            Dictionary<int, Segment> segments,
            Dictionary<string, Distance> distances,
            Event iEvent
            )
        {
            LocationName = locations.TryGetValue(LocationId, out TimingLocation? loc) ? loc.Name : "Unknown";
            if (Constants.Timing.SEGMENT_FINISH == SegmentId)
            {
                SegmentName = "Finish ";
            }
            else if (Constants.Timing.SEGMENT_START == SegmentId)
            {
                SegmentName = "Start ";
            }
            else if (segments.TryGetValue(SegmentId, out Segment? seg))
            {
                SegmentName = seg.Name + " ";
            }
            else
            {
                SegmentName = "";
            }
            if (iEvent.EventType == Constants.Timing.EVENT_TYPE_TIME)
            {
                if (Constants.Timing.SEGMENT_FINISH == SegmentId)
                {
                    if (LinkedDistanceName.Length > 0
                        && distances.TryGetValue(LinkedDistanceName, out Distance? linkedDist)
                        && linkedDist.DistanceValue > 0)
                    {
                        SegmentName = string.Format("{1:0.##} {2} - Lap {0}",
                            Occurrence,
                            linkedDist.DistanceValue * Occurrence,
                            Distances.DistanceString(linkedDist.DistanceUnit)
                            );
                    }
                    else if (RealDistanceName.Length > 0
                        && distances.TryGetValue(RealDistanceName, out Distance? oDist)
                        && oDist.DistanceValue > 0)
                    {
                        SegmentName = string.Format("{1:0.##} {2} - Lap {0}",
                            Occurrence,
                            oDist.DistanceValue * Occurrence,
                            Distances.DistanceString(oDist.DistanceUnit)
                            );
                    }
                    else
                    {
                        SegmentName = $"Lap {Occurrence}";
                    }
                }
                else if (Constants.Timing.SEGMENT_START != SegmentId)
                {
                    if (LinkedDistanceName.Length > 0
                        && segments.TryGetValue(SegmentId, out Segment? oSeg)
                        && oSeg.CumulativeDistance > 0)
                    {
                        SegmentName = $"{oSeg.CumulativeDistance * Occurrence:0.##} {Distances.DistanceString(oSeg.DistanceUnit)} - {SegmentName}{Occurrence}";
                    }
                    else
                    {
                        SegmentName = $"{SegmentName} {Occurrence}";
                    }
                }
            }
            else if (Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA == iEvent.EventType)
            {
                int hour = (Occurrence / 2) + 1;
                if (Constants.Timing.SEGMENT_FINISH == SegmentId)
                {
                    if (LinkedDistanceName.Length > 0
                        && distances.TryGetValue(LinkedDistanceName, out Distance? linkedDist)
                        && linkedDist.DistanceValue > 0)
                    {
                        SegmentName = string.Format("{1:0.##} {2} - Hour {0}",
                            hour,
                            linkedDist.DistanceValue * hour,
                            Distances.DistanceString(linkedDist.DistanceUnit)
                            );
                    }
                    else if (RealDistanceName.Length > 0
                        && distances.TryGetValue(RealDistanceName, out Distance? oDist)
                        && oDist.DistanceValue > 0)
                    {
                        SegmentName = string.Format("{1:0.##} {2} - Hour {0}",
                            hour,
                            oDist.DistanceValue * hour,
                            Distances.DistanceString(oDist.DistanceUnit)
                            );
                    }
                    else
                    {
                        SegmentName = $"Hour {hour}";
                    }
                }
                else if (Constants.Timing.SEGMENT_START == SegmentId)
                {
                    SegmentName = $"Start {hour}";
                }
            }
            SegmentName = SegmentName.Trim();
            theEvent = iEvent;
        }

        public int Age(string eventDate)
        {
            if (Birthday.Length < 1)
            {
                return -1;
            }
            DateTime eventDateTime = Convert.ToDateTime(eventDate);
            DateTime myDateTime = Convert.ToDateTime(Birthday);
            int numYears = eventDateTime.Year - myDateTime.Year;
            if (eventDateTime.Month < myDateTime.Month || eventDateTime.Month == myDateTime.Month && eventDateTime.Day < myDateTime.Day)
            {
                numYears--;
            }
            return numYears;
        }

        public bool IsUploaded()
        {
            return uploaded != Constants.Timing.TIMERESULT_UPLOADED_FALSE;
        }

        public bool IsDnf()
        {
            return Status == Constants.Timing.TIMERESULT_STATUS_DNF;
        }

        public static int CompareByGunTime(TimeResult one, TimeResult two)
        {
            return one.Seconds == two.Seconds ? one.Milliseconds.CompareTo(two.Milliseconds) : one.Seconds.CompareTo(two.Seconds);
        }

        public static int CompareByNetTime(TimeResult one, TimeResult two)
        {
            return one.ChipSeconds == two.ChipSeconds ? one.ChipMilliseconds.CompareTo(two.ChipMilliseconds) : one.ChipSeconds.CompareTo(two.ChipSeconds);
        }

        public static int CompareByAgeGroup(TimeResult one, TimeResult two)
        {
            if (!one.DistanceName.Equals(two.DistanceName)) return string.Compare(one.DistanceName, two.DistanceName, StringComparison.Ordinal);
            if (one.AgeGroupId != two.AgeGroupId) return one.AgeGroupId.CompareTo(two.AgeGroupId);
            if (!one.Gender.Equals(two.Gender)) return string.Compare(one.Gender, two.Gender, StringComparison.Ordinal);
            return one.Occurrence.Equals(two.Occurrence) ? one.Place.CompareTo(two.Place) : one.Occurrence.CompareTo(two.Occurrence);
        }

        public static int CompareByGender(TimeResult one, TimeResult two)
        {
            if (!one.DistanceName.Equals(two.DistanceName)) return string.Compare(one.DistanceName, two.DistanceName, StringComparison.Ordinal);
            return one.Gender.Equals(two.Gender) ? one.systemTime.CompareTo(two.systemTime) : string.Compare(one.Gender, two.Gender, StringComparison.Ordinal);
        }

        public static int CompareBySystemTime(TimeResult one, TimeResult two)
        {
            return one.systemTime.CompareTo(two.systemTime);
        }

        public static int CompareByBib(TimeResult one, TimeResult two)
        {
            if (one.Bib == two.Bib)
            {
                return one.systemTime.CompareTo(two.systemTime);
            }
            if (int.TryParse(one.Bib, out int bibOne) && int.TryParse(two.Bib, out int bibTwo))
            {
                return bibOne.CompareTo(bibTwo);
            }
            return string.Compare(one.Bib, two.Bib, StringComparison.Ordinal);
        }

        public static int CompareByDistance(TimeResult one, TimeResult two)
        {
            return one.DistanceName.Equals(two.DistanceName) ? one.systemTime.CompareTo(two.systemTime) : string.Compare(one.DistanceName, two.DistanceName, StringComparison.Ordinal);
        }

        public int CompareChip(TimeResult other)
        {
            return ChipSeconds == other.ChipSeconds ? ChipMilliseconds.CompareTo(other.ChipMilliseconds) : ChipSeconds.CompareTo(other.ChipSeconds);
        }

        public static int CompareByDistanceChip(TimeResult one, TimeResult two)
        {
            if (!one.DistanceName.Equals(two.DistanceName)) return string.Compare(one.DistanceName, two.DistanceName, StringComparison.Ordinal);
            return one.ChipSeconds == two.ChipSeconds ? one.ChipMilliseconds.CompareTo(two.ChipMilliseconds) : one.ChipSeconds.CompareTo(two.ChipSeconds);
        }

        public static int CompareByDistancePlace(TimeResult one, TimeResult two)
        {
            if (!one.DistanceName.Equals(two.DistanceName)) return string.Compare(one.DistanceName, two.DistanceName, StringComparison.Ordinal);
            if (one.Occurrence != two.Occurrence)
            {
                return two.Occurrence.CompareTo(one.Occurrence);
            }
            if (one.Status == Constants.Timing.TIMERESULT_STATUS_DNF && two.Status != Constants.Timing.TIMERESULT_STATUS_DNF)
            {
                return 1;
            }
            if (one.Status != Constants.Timing.TIMERESULT_STATUS_DNF && two.Status == Constants.Timing.TIMERESULT_STATUS_DNF)
            {
                return -1;
            }
            return one.Place == two.Place ? one.SystemTime.CompareTo(two.SystemTime) : one.Place.CompareTo(two.Place);
        }

        public static int CompareByDistanceGenderPlace(TimeResult one, TimeResult two)
        {
            return one.DistanceName.Equals(two.DistanceName) ? one.GenderPlace.CompareTo(two.GenderPlace) : string.Compare(one.DistanceName, two.DistanceName, StringComparison.Ordinal);
        }

        public static int CompareByDistanceAgeGroupPlace(TimeResult one, TimeResult two)
        {
            return one.DistanceName.Equals(two.DistanceName) ? one.AgePlace.CompareTo(two.AgePlace) : string.Compare(one.DistanceName, two.DistanceName, StringComparison.Ordinal);
        }

        public static int CompareByOccurrence(TimeResult one, TimeResult two)
        {
            if (!one.Occurrence.Equals(two.Occurrence)) return one.Occurrence.CompareTo(two.Occurrence);
            return one.Seconds == two.Seconds ? one.Milliseconds.CompareTo(two.Milliseconds) : one.Seconds.CompareTo(two.Seconds);
        }

        public static int CompareForBackyardElapsed(TimeResult one, TimeResult two)
        {
            if (!one.Occurrence.Equals(two.Occurrence)) return two.Occurrence.CompareTo(one.Occurrence);
            return one.Seconds == two.Seconds ? one.Milliseconds.CompareTo(two.Milliseconds) : one.Seconds.CompareTo(two.Seconds);
        }

        public static int CompareForBackyardCumulative(TimeResult one, TimeResult two)
        {
            if (!one.Occurrence.Equals(two.Occurrence)) return two.Occurrence.CompareTo(one.Occurrence);
            return one.ChipSeconds == two.ChipSeconds ? one.ChipMilliseconds.CompareTo(two.ChipMilliseconds) : one.ChipSeconds.CompareTo(two.ChipSeconds);
        }

        public static bool IsNotKnown(TimeResult one)
        {
            return one.EventSpecificId == Constants.Timing.TIMERESULT_DUMMYPERSON;
        }

        public static bool IsKnown(TimeResult one)
        {
            return one.EventSpecificId != Constants.Timing.TIMERESULT_DUMMYPERSON;
        }

        public static bool StartTimes(TimeResult one)
        {
            return one.EventSpecificId == Constants.Timing.TIMERESULT_DUMMYPERSON || one.SegmentId == Constants.Timing.SEGMENT_START;
        }

        public static bool IsNotStart(TimeResult one)
        {
            return one.SegmentId != Constants.Timing.SEGMENT_START;
        }

        public static bool IsNotFinish(TimeResult one)
        {
            return one.SegmentId != Constants.Timing.SEGMENT_FINISH;
        }

        public static bool IsNotFinishOrKnown(TimeResult one)
        {
            return one.SegmentId == Constants.Timing.SEGMENT_START
                || one.EventSpecificId != Constants.Timing.TIMERESULT_DUMMYPERSON
                || one.LocationId != Constants.Timing.LOCATION_FINISH;
        }

        public static bool IsNotStartOrKnown(TimeResult one)
        {
            return one.SegmentId != Constants.Timing.SEGMENT_START
                || one.EventSpecificId != Constants.Timing.TIMERESULT_DUMMYPERSON;
        }

        public bool IsNotMatch(string value)
        {
            return !Bib.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase) &&
                !ParticipantName.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        public bool SmsCanBeSent(TimingDictionary dictionary)
        {
            if (GlobalVars.TwilioCredentials.AccountSid.Length < 1 || GlobalVars.TwilioCredentials.AuthToken.Length < 1)
            {
                return false;
            }
            if (!dictionary.ParticipantBibDictionary.TryGetValue(Bib, out Participant? part) || !part.EventSpecific.SMSEnabled)
            {
                return false;
            }
            string validPhone = GlobalVars.GetValidPhone(part.Mobile);
            if (validPhone.Length == 0)
            {
                validPhone = GlobalVars.GetValidPhone(part.Phone);
            }

            // Invalid length. +15555551234 is a valid phone
            return validPhone.Length == 12 && GlobalVars.TwilioCredentials.PhoneNumber.Length == 12;
        }

        public static SmsState SendSmsAlert(string phone, string sms)
        {
            if (GlobalVars.TwilioCredentials.AccountSid.Length < 1 || GlobalVars.TwilioCredentials.AuthToken.Length < 1)
            {
                return SmsState.Invalid;
            }
            // Invalid length. +15555551234 is a valid phone
            if (phone.Length != 12 || GlobalVars.TwilioCredentials.PhoneNumber.Length != 12)
            {
                return SmsState.Invalid;
            }
            // Verify phone number isn't in our list of banned phone numbers (i.e. they've told us to not send texts)
            // return true if it is in the banned list, otherwise try to send it, and return true if we were able to send it
            if (GlobalVars.BannedPhones.Contains(phone))
            {
                Log.D("Objects.TimeResult", "Phone number is banned.");
                return SmsState.Invalid;
            }
            try
            {
                Log.D("Objects.TimeResult", "sms: '" + sms + "' phone: " + phone);
                CreateMessageOptions messageOptions = new(
                    new PhoneNumber(phone)
                    )
                {
                    From = new PhoneNumber(GlobalVars.TwilioCredentials.PhoneNumber),
                    Body = sms
                };
                MessageResource? message = MessageResource.Create(messageOptions);
                if (message.ErrorMessage != null)
                {
                    return SmsState.AddToBanned;
                }
            }
            catch
            {
                return SmsState.NetworkError;
            }
            return SmsState.Success;
        }

        public bool Equals(TimeResult? other)
        {
            return other != null && EventSpecificId == other.EventSpecificId
                && LocationId == other.LocationId
                && SegmentId == other.SegmentId
                && Occurrence == other.Occurrence;
        }

        public void UpdateEvent(Event newEvent)
        {
            theEvent = newEvent;
        }

        public enum SmsState
        {
            None = 0,
            Success,
            AddToBanned,
            Invalid,
            NetworkError
        }
    }
}
