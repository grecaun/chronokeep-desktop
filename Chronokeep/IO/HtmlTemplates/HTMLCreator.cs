using Chronokeep.Helpers;
using Chronokeep.Objects;
using System.Collections.Generic;

namespace Chronokeep.IO.HtmlTemplates
{
    public partial class HtmlResultsTemplate
    {
        private readonly Event theEvent;
        private readonly Dictionary<string, List<TimeResult>> distanceResults = [];
        private readonly bool linkPart = false;

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
                if (rankingGender == "WOMAN")
                {
                    rankingGender = "Women";
                }
                else if (rankingGender == "MAN")
                {
                    rankingGender = "Men";
                }
                else
                {
                    rankingGender = finish.Gender;
                }
            }
            Log.D("IO.HtmlTemplates.HtmlParticipantTemplate", "Template created.");
        }
    }

    public partial class HtmlCertificateEmailTemplate
    {
        readonly string eventName;
        readonly string distanceName;
        readonly string participantName;
        readonly string time;
        readonly string certificateUrl;
        readonly string resultsLink;
        readonly string unsubscribe;

        public HtmlCertificateEmailTemplate(
            Event theEvent,
            TimeResult result,
            string email,
            bool singleDist,
            ApiObject? api)
        {
            eventName = string.Format("{0} {1}", theEvent.Year, theEvent.Name);
            distanceName = "";
            if (!singleDist)
            {
                distanceName = string.Format(" {0}", result.DistanceName);
            }
            participantName = result.First;
            time = result.ChipTimeNoMilliseconds;
            certificateUrl = string.Format("https://cert.chronokeep.com/{0} {1}/{2}{3}/{4}/{5}", result.First, result.Last, eventName, distanceName, time, theEvent.LongDate);
            resultsLink = "";
            string[] event_ids = theEvent.ApiEventId.Split(',');
            if (api != null && api.WebUrl.Length > 1)
            {
                if (event_ids.Length == 2)
                {
                    resultsLink = string.Format("<p><a href=\"{2}results/{0}/{1}\">Click here for more results.</a></p>", event_ids[0], event_ids[1], api.WebUrl);
                }
                else
                {
                    resultsLink = string.Format("<p><a href=\"{0}\">Click here for more results.</a></p>", api.WebUrl);
                }
            }
            unsubscribe = string.Format("<br>If you don't want to receive these emails <a href=\"https://www.chronokeep.com/unsubscribe/{0}\">click here</a>.", email);
        }
    }
}
