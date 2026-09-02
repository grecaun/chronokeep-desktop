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

using Chronokeep.Helpers;
using Chronokeep.Objects;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;

namespace Chronokeep.IO.HtmlTemplates
{
    public partial class HtmlPrintableSelectionTemplate
    {
        private readonly Event theEvent;
        private readonly Dictionary<string, TimeResult> finishResults = [];
    }

    public partial class HtmlPrintableTemplate
    {
        private readonly TimeResult result;
        private readonly int numOverall, numGender, numAgeGroup;
        private readonly string GenderStr, AgeGroupStr, pace, paceStr, distanceValueStr;
        private readonly double distanceValue;

        public static int TopMargin = 0;
        public static int LeftMargin = 0;
        public static double Scale = 1.0;

        public HtmlPrintableTemplate(
            TimeResult result,
            int numOverall,
            int numGender,
            int numAgeGroup,
            Distance? distance)
        {
            this.result = result;
            this.numOverall = numOverall;
            this.numGender = numGender;
            this.numAgeGroup = numAgeGroup;
            GenderStr = result.Gender == "Man" ? "Men" : result.Gender == "Woman" ? "Women" : result.Gender == "Not Specified" || result.Gender.Equals("ns", StringComparison.OrdinalIgnoreCase) ? "" : result.Gender;
            AgeGroupStr = $"{GenderStr} {result.PrettyAgeGroupName()}".Trim();
            distanceValue = distance?.DistanceValue ?? 0.0;
            distanceValueStr = distance?.DistanceUnit switch
            {
                Constants.Distances.KILOMETERS => "kilometers",
                Constants.Distances.METERS => "meters",
                Constants.Distances.MILES => "miles",
                Constants.Distances.YARDS => "yards",
                Constants.Distances.FEET => "feet",
                _ => "??",
            };
            pace = Constants.Timing.SecondsToMinuteTime((long)(result.ChipSeconds / distanceValue));
            paceStr = distance?.DistanceUnit switch
            {
                Constants.Distances.KILOMETERS => "min/km",
                Constants.Distances.METERS => "min/meter",
                Constants.Distances.MILES => "min/mile",
                Constants.Distances.YARDS => "min/yard",
                Constants.Distances.FEET => "min/foot",
                _ => "min/??",
            };

        }

        private static string ScaledTextOne()
        {
            return $"{(Scale * 2):F1}";
        }

        private static string ScaledTextTwo()
        {
            return $"{Scale:F1}";
        }

        private static int ScaledImageHeight()
        {
            return (int)(Scale * 100);
        }

        private static int ScaledColumnWidth()
        {
            return (int)(Scale * 125);
        }

        private static int ScaledDividerWidth()
        {
            return (int)(Scale * 35);
        }

        private static int ScaledTotalWidth()
        {
            return (int)(Scale * 500);
        }
    }

    public partial class HtmlResultsTemplate
    {
        private readonly Event theEvent;
        private readonly Dictionary<string, List<TimeResult>> distanceResults = [];
        private readonly bool linkPart;

        public HtmlResultsTemplate(
            Event theEvent,
            List<TimeResult> resultList,
            bool linkPart = false)
        {
            this.theEvent = theEvent;
            resultList.Sort(TimeResult.CompareByDistancePlace);
            foreach (TimeResult result in resultList)
            {
                if (!distanceResults.TryGetValue(result.DistanceName, out List<TimeResult>? distResList))
                {
                    distResList = [];
                    distanceResults[result.DistanceName] = distResList;
                }

                distResList.Add(result);
            }
            this.linkPart = linkPart;
        }
    }
    public partial class HtmlParticipantTemplate
    {
        private readonly Event theEvent;
        private readonly List<TimeResult> resultList;
        private readonly TimeResult? finish;
        private readonly TimeResult? start;
        private readonly string rankingGender = "";

        public HtmlParticipantTemplate(
            Event theEvent,
            List<TimeResult> rList)
        {
            this.theEvent = theEvent;
            resultList = rList;
            resultList.Sort(TimeResult.CompareBySystemTime);
            foreach (TimeResult result in resultList)
            {
                if (result.LocationId == Constants.Timing.LOCATION_FINISH)
                {
                    if (finish == null || finish.Occurrence < result.Occurrence)
                    {
                        finish = result;
                    }
                }
                if (result.SegmentId == Constants.Timing.SEGMENT_START)
                {
                    start = result;
                }
            }
            if (finish != null)
            {
                resultList.RemoveAll(r =>
                    (r.Occurrence == finish.Occurrence && r.LocationId == Constants.Timing.LOCATION_FINISH)
                    || (r.SegmentId == Constants.Timing.SEGMENT_START)
                    );
                rankingGender = finish.Gender.ToUpper();
                rankingGender = rankingGender switch
                {
                    "WOMAN" => "Women",
                    "MAN" => "Men",
                    _ => finish.Gender
                };
            }
            Log.D("IO.HtmlTemplates.HtmlParticipantTemplate", "Template created.");
        }
    }

    public partial class HtmlCertificateEmailTemplate
    {
        private readonly string eventName;
        private readonly string distanceName;
        private readonly string participantName;
        private readonly string time;
        private readonly string certificateUrl;
        private readonly string resultsLink;
        private readonly string unsubscribe;

        public HtmlCertificateEmailTemplate(
            Event theEvent,
            TimeResult result,
            string email,
            bool singleDist,
            ApiObject? api)
        {
            eventName = $"{theEvent.Year} {theEvent.Name}";
            distanceName = "";
            if (!singleDist)
            {
                distanceName = $" {result.DistanceName}";
            }
            participantName = result.First;
            time = result.ChipTimeNoMilliseconds;
            certificateUrl = $"https://cert.chronokeep.com/{result.First} {result.Last}/{eventName}{distanceName}/{time}/{theEvent.LongDate}";
            resultsLink = "";
            string[] eventIds = theEvent.ApiEventId.Split(',');
            if (api is { WebUrl.Length: > 1 })
            {
                resultsLink = eventIds.Length == 2 ? string.Format("<p><a href=\"{2}results/{0}/{1}\">Click here for more results.</a></p>", eventIds[0], eventIds[1], api.WebUrl) : $"<p><a href=\"{api.WebUrl}\">Click here for more results.</a></p>";
            }
            unsubscribe = $"<br>If you don't want to receive these emails <a href=\"https://www.chronokeep.com/unsubscribe/{email}\">click here</a>.";
        }
    }
}

