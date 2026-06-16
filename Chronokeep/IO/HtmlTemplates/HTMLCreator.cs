using Chronokeep.Helpers;
using Chronokeep.Objects;
using System.Collections.Generic;

namespace Chronokeep.IO.HtmlTemplates
{
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
