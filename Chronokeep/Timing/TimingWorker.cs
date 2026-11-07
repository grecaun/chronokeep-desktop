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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Chronokeep.Constants;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Network.API;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronoKeepAPI;
using Chronokeep.Timing.Routines;
using static Chronokeep.Objects.TimeResult;

namespace Chronokeep.Timing
{
    internal partial class TimingWorker
    {
        private readonly IdbInterface database;
        private readonly IMainWindow window;
        private static TimingWorker? worker;

        private static readonly Semaphore Semaphore = new(0, 2);
        private static readonly Lock WorkLock = new();
        private static readonly Lock ResetDictionariesLock = new();
        private static readonly Lock ResultsLock = new();
        private static bool quittingTime;
        private static bool newResults;
        private static bool resetDictionariesBool = true;

        private static readonly TimingDictionary Dictionary = new();
        private static DateTime lastSubscriptionFetch = DateTime.Now.AddMinutes(-1);

        [GeneratedRegex("[^A-Za-z]")]
        private static partial Regex AlphaOnly();

        private TimingWorker(IMainWindow window, IdbInterface database)
        {
            this.window = window;
            this.database = database;
        }

        public static TimingWorker NewWorker(IMainWindow window, IdbInterface database)
        {
            worker ??= new TimingWorker(window, database);
            return worker;
        }

        /// <summary>
        /// Finds the results that could get a text message.
        /// </summary>
        /// <param name="results">The event's results.</param>
        /// <param name="distances">The event's distances by name.</param>
        /// <param name="alertsSent">The (event specific id, segment id) pairs that already had a text sent.</param>
        /// <param name="fifteenPrior">The time 15 minutes ago. Results from before then are too old to send.</param>
        /// <returns>
        /// Finish and split results for known participants in distances with text alerts turned on,
        /// that haven't had a text sent and happened in the last 15 minutes.
        /// </returns>
        internal static List<TimeResult> GetSmsResults(
            List<TimeResult> results,
            Dictionary<string, Distance> distances,
            HashSet<(int, int)> alertsSent,
            DateTime fifteenPrior
            )
        {
            List<TimeResult> output = [];
            foreach (TimeResult result in results)
            {
                // verify the distance is set to allow sms alerts and the runner hasn't been notified already,
                // and we're within 15 minutes of it happening
                if (!distances.TryGetValue(result.RealDistanceName, out Distance? dist)
                    || !dist.SmsEnabled
                    || Constants.Timing.TIMERESULT_DUMMYPERSON == result.EventSpecificId
                    || alertsSent.Contains((result.EventSpecificId, result.SegmentId))
                    || result.SystemTime.CompareTo(fifteenPrior) <= 0)
                {
                    continue;
                }
                if (Constants.Timing.SEGMENT_START != result.SegmentId && Constants.Timing.SEGMENT_NONE != result.SegmentId)
                {
                    output.Add(result);
                }
            }
            return output;
        }

        /// <summary>
        /// Builds a lookup from a participant's name to their bib, used for subscriptions that only have a name.
        /// </summary>
        /// <param name="participants">The event's participants.</param>
        /// <returns>
        /// Bibs keyed by first and last name, lower case with everything but letters removed. Names shared by more
        /// than one participant are left out since we can't tell those people apart.
        /// </returns>
        internal static Dictionary<string, string> GetNameToBib(List<Participant> participants)
        {
            Dictionary<string, string> output = [];
            HashSet<string> duplicateNames = [];
            foreach (Participant p in participants)
            {
                string name = p.FirstName.ToLower() + p.LastName.ToLower();
                name = AlphaOnly().Replace(name, string.Empty);
                if (output.ContainsKey(name))
                {
                    duplicateNames.Add(name);
                }
                output[name] = p.Bib;
            }
            foreach (string dup in duplicateNames)
            {
                output.Remove(dup);
            }
            return output;
        }

        /// <summary>
        /// Works out which phone numbers are subscribed to each bib.
        /// </summary>
        /// <param name="subscriptions">The event's text subscriptions.</param>
        /// <param name="nameToBib">Bibs by name (see GetNameToBib), for subscriptions without a bib.</param>
        /// <returns>The valid phone numbers subscribed to each bib.</returns>
        internal static Dictionary<string, HashSet<string>> GetBibToPhones(List<ApiSmsSubscription> subscriptions, Dictionary<string, string> nameToBib)
        {
            Dictionary<string, HashSet<string>> output = [];
            foreach (ApiSmsSubscription sub in subscriptions)
            {
                string bib = sub.Bib;
                string phone = GlobalVars.GetValidPhone(sub.Phone);
                if (bib.Length < 1 && sub.First.Length + sub.Last.Length > 0)
                {
                    string name = sub.First.ToLower() + sub.Last.ToLower();
                    name = AlphaOnly().Replace(name, string.Empty);
                    if (nameToBib.TryGetValue(name, out string? bibFromName))
                    {
                        bib = bibFromName;
                    }
                }
                if (bib.Length <= 0 || phone.Length <= 0)
                {
                    continue;
                }
                if (!output.TryGetValue(bib, out HashSet<string>? phoneSet))
                {
                    phoneSet = [];
                    output[bib] = phoneSet;
                }
                phoneSet.Add(phone);
            }
            return output;
        }

        /// <summary>
        /// Matches results to the phone numbers that should get a text for them.
        /// </summary>
        /// <param name="smsResults">The results that could get a text (see GetSmsResults).</param>
        /// <param name="bibToPhones">The phone numbers subscribed to each bib (see GetBibToPhones).</param>
        /// <returns>The phone numbers to text for each result. Results with no subscribers are left out.</returns>
        internal static Dictionary<TimeResult, HashSet<string>> GetPhonesToText(List<TimeResult> smsResults, Dictionary<string, HashSet<string>> bibToPhones)
        {
            Dictionary<TimeResult, HashSet<string>> output = [];
            foreach (TimeResult result in smsResults)
            {
                if (!bibToPhones.TryGetValue(result.Bib, out HashSet<string>? phonesFromDict))
                {
                    continue;
                }
                foreach (string phone in phonesFromDict)
                {
                    if (!output.TryGetValue(result, out HashSet<string>? phones))
                    {
                        phones = [];
                        output[result] = phones;
                    }
                    phones.Add(phone);
                }
            }
            return output;
        }

        /// <summary>
        /// The "More results" link added to text messages.
        /// </summary>
        /// <param name="theEvent">The event.</param>
        /// <param name="api">The results API the event uploads to, if any.</param>
        /// <returns>The link text, or an empty string if the API has no website.</returns>
        internal static string GetResultsUrl(Event theEvent, ApiObject? api)
        {
            string output = "";
            if (api != null && api.WebUrl.Length > 0)
            {
                string[] eventIds = theEvent.ApiEventId.Split(',');
                output = eventIds.Length == 2 ? $" More results @ {api.WebUrl}results/{eventIds[0]}/{eventIds[1]}." : $" More results @ {api.WebUrl}.";
            }
            return output;
        }

        /// <summary>
        /// Sends the text messages and records which results have been handled.
        /// </summary>
        /// <param name="toSendTo">The phone numbers to text for each result (see GetPhonesToText).</param>
        /// <param name="theEvent">The event.</param>
        /// <param name="multipleDistances">Whether the event has more than one main distance, so the message names the distance.</param>
        /// <param name="resultsUrl">The link text to add (see GetResultsUrl).</param>
        /// <param name="database">The database the sent alerts are saved in.</param>
        /// <param name="sendSms">Sends one message to one phone number and returns how it went.</param>
        /// <param name="banPhone">Adds a phone number to the banned list.</param>
        internal static void SendSmsAlerts(
            Dictionary<TimeResult, HashSet<string>> toSendTo,
            Event theEvent,
            bool multipleDistances,
            string resultsUrl,
            IdbInterface database,
            Func<string, string, SmsState> sendSms,
            Action<string> banPhone
            )
        {
            foreach (TimeResult result in toSendTo.Keys)
            {
                // Only send alert if participant wants it sent
                // Do not add to the AlertsSent database because they
                // may change their mind later, and
                // we still want to be able to send an SMS to them.
                // Only add to the database/dictionary if successful.
                if (Constants.Timing.TIMERESULT_DUMMYPERSON == result.EventSpecificId)
                {
                    continue;
                }
                string sms = BuildSmsMessage(result, theEvent, multipleDistances, resultsUrl);
                bool sent = false;
                bool tryAgain = false;
                if (result.Anonymous)
                {
                    sent = true;
                }
                else
                {
                    foreach (string phone in toSendTo[result])
                    {
                        SmsState status = sendSms(phone, sms);
                        switch (status)
                        {
                            // add to banned phones list
                            case SmsState.AddToBanned:
                                banPhone(phone);
                                break;
                            case SmsState.Success:
                                sent = true;
                                break;
                            // Try again later if the message couldn't go out for now.
                            case SmsState.NetworkError:
                            case SmsState.NotSetUp:
                                tryAgain = true;
                                break;
                            case SmsState.None:
                            case SmsState.Invalid:
                            default:
                                break;
                        }
                    }
                }
                // update status if we sent a message out, or there's no reason to try again
                if (sent || !tryAgain)
                {
                    database.AddSmsAlert(theEvent.Identifier, result.EventSpecificId, result.SegmentId);
                }
            }
        }

        /// <summary>The text message sent to people subscribed to a runner's results.</summary>
        internal static string BuildSmsMessage(TimeResult result, Event theEvent, bool multipleDistances, string resultsUrl)
        {
            if (Constants.Timing.SEGMENT_FINISH == result.SegmentId)
            {
                string distance = multipleDistances ? $" {result.DistanceName}" : "";
                return $"{result.First} {result.Last} has finished the {theEvent.Year} {theEvent.Name}{distance} in {result.ChipTimeNoMilliseconds}.{resultsUrl} Reply STOP to opt-out.";
            }
            return $"{result.First} {result.Last} has reached {result.SegmentName.Trim()} in {result.ChipTimeNoMilliseconds}.{resultsUrl} Reply STOP to opt-out.";
        }

        public static bool NewResultsExist()
        {
            bool output = false;
            //Log.D("Timing.TimingWorker", "Lock Wait 02");
            if (!ResultsLock.TryEnter(3000)) return output;
            try
            {
                output = newResults;
                newResults = false;
            }
            finally
            {
                ResultsLock.Exit();
            }
            return output;
        }

        public static void Shutdown()
        {
            Log.D("Timing.TimingWorker", "Lock Wait 01");
            if (!WorkLock.TryEnter(3000)) return;
            try
            {
                quittingTime = true;
            }
            finally
            {
                WorkLock.Exit();
            }
        }

        public static void Notify()
        {
            try
            {
                Log.D("Timing.TimingWorker", "Releasing semaphore.");
                Semaphore.Release();
            }
            catch
            {
                Log.D("Timing.TimingWorker", "Unable to release, release is full.");
            }
        }

        public static void ResetDictionaries()
        {
            Log.D("Timing.TimingWorker", "Resetting dictionaries next go around.");
            Log.D("Timing.TimingWorker", "Lock Wait 04");
            if (!ResetDictionariesLock.TryEnter(3000)) return;
            try
            {
                resetDictionariesBool = true;
            }
            finally
            {
                ResetDictionariesLock.Exit();
            }
        }

        private void RecalculateDictionaries(Event theEvent)
        {
            Log.D("Timing.TimingWorker", "Recalculating dictionaries.");
            // Locations for checking if we're past the maximum number of occurrences
            // Stored in a dictionary based upon the location ID for easier access.
            Dictionary.LocationDictionary.Clear();
            foreach (TimingLocation _ in database.GetTimingLocations(theEvent.Identifier).Where(loc => !Dictionary.LocationDictionary.TryAdd(loc.Identifier, loc)))
            {
                Log.D("Timing.TimingWorker", "Multiples of a location found in location set.");
            }
            // Segments so we can give a result a segment ID if it's at the right location
            // and occurrence. Stored in a dictionary for obvious reasons.
            Dictionary.SegmentDictionary.Clear();
            // Keep track of the list of Segments by distance
            Dictionary.DistanceSegmentOrder.Clear();
            Dictionary.SegmentByIdDictionary.Clear();
            foreach (Segment seg in database.GetSegments(theEvent.Identifier))
            {
                if (!Dictionary.SegmentDictionary.TryAdd((seg.DistanceId, seg.LocationId, seg.Occurrence), seg))
                {
                    Log.D("Timing.TimingWorker", "Multiples of a segment found in segment set.");
                }
                if (!Dictionary.DistanceSegmentOrder.TryGetValue(seg.DistanceId, out List<Segment>? segList))
                {
                    segList = [];
                    Dictionary.DistanceSegmentOrder[seg.DistanceId] = segList;
                }
                segList.Add(seg);
                Dictionary.SegmentByIdDictionary[seg.Identifier] = seg;
            }
            // Add finish segments to DistanceSegmentOrder if distance is specified
            foreach (Distance d in database.GetDistances(theEvent.Identifier).Where(d => d.DistanceValue > 0))
            {
                if (!Dictionary.DistanceSegmentOrder.TryGetValue(d.Identifier, out List<Segment>? segOrderList))
                {
                    segOrderList = [];
                    Dictionary.DistanceSegmentOrder[d.Identifier] = segOrderList;
                }

                segOrderList.Add(
                    new Segment(
                        Constants.Timing.SEGMENT_FINISH,
                        theEvent.Identifier,
                        d.Identifier,
                        Constants.Timing.LOCATION_FINISH,
                        d.FinishOccurrence,
                        0.0,
                        d.DistanceValue,
                        d.DistanceUnit,
                        "Finish",
                        "",
                        "")
                );
            }
            // Participants so we can check their Distance.
            Dictionary.ParticipantBibDictionary.Clear();
            Dictionary.ParticipantEventSpecificDictionary.Clear();
            foreach (Participant part in database.GetParticipants(theEvent.Identifier))
            {
                if (!Dictionary.ParticipantBibDictionary.TryAdd(part.Bib, part))
                {
                    Log.D("Timing.TimingWorker", $"Multiples of a Bib found in participants set. {part.Bib}");
                }
                Dictionary.ParticipantEventSpecificDictionary[part.EventSpecific.Identifier] = part;
            }
            // Get the start time for the event. (Net time of 0:00:00.000)
            Dictionary.DistanceStartDict.Clear();
            DateTime startTime = DateTime.Parse(theEvent.Date).AddSeconds(theEvent.StartSeconds);
            Dictionary.DistanceStartDict[0] = (Constants.Timing.RfidDateToEpoch(startTime), theEvent.StartMilliseconds);
            // And the end time (for time based events)
            Dictionary.DistanceEndDict.Clear();
            Dictionary.DistanceEndDict[0] = Dictionary.DistanceStartDict[0];
            // Distances so we can get their start offset.
            Dictionary.DistanceDictionary.Clear();
            Dictionary.DistanceNameDictionary.Clear();
            List<Distance> distances = database.GetDistances(theEvent.Identifier);
            foreach (Distance d in distances)
            {
                if (!Dictionary.DistanceDictionary.TryAdd(d.Identifier, d))
                {
                    Log.D("Timing.TimingWorker", "Multiples of a Distance found in distances set.");
                }
                Dictionary.DistanceNameDictionary[d.Name] = d;
                Log.D("Timing.TimingWorker", $"Distance {d.Name} offsets are {d.StartOffsetSeconds} {d.StartOffsetMilliseconds}");
                long startSeconds = Dictionary.DistanceStartDict[0].Seconds + d.StartOffsetSeconds;
                int startMilliseconds = Dictionary.DistanceStartDict[0].Milliseconds + d.StartOffsetMilliseconds;
                switch (startMilliseconds)
                {
                    case < 0:
                        startSeconds -= 1;
                        startMilliseconds += 1000;
                        break;
                    case >= 1000:
                        startSeconds += 1;
                        startMilliseconds -= 1000;
                        break;
                }
                Dictionary.DistanceStartDict[d.Identifier] = (startSeconds, startMilliseconds);
                Dictionary.DistanceEndDict[d.Identifier] = (startSeconds + d.EndSeconds, startMilliseconds);
                Dictionary.DistanceEndDict[0] = (startSeconds + d.EndSeconds, startMilliseconds);
            }
            // Set up bibToChipDictionary so we can link bibs to chips
            Dictionary.ChipToBibDictionary.Clear();
            Dictionary.BibToChipDictionary.Clear();
            List<BibChipAssociation> bibChips = database.GetBibChips(theEvent.Identifier);
            foreach (BibChipAssociation assoc in bibChips)
            {
                Dictionary.ChipToBibDictionary[assoc.Chip] = assoc.Bib;
                if (!Dictionary.BibToChipDictionary.TryGetValue(assoc.Bib, out List<string>? chipList))
                {
                    chipList = [];
                    Dictionary.BibToChipDictionary[assoc.Bib] = chipList;
                }

                chipList.Add(assoc.Chip);
            }
            // Dictionary for looking up linked distances
            Dictionary.LinkedDistanceDictionary.Clear();
            Dictionary.LinkedDistanceIdentifierDictionary.Clear();
            Dictionary.MainDistances.Clear();
            foreach (Distance d in distances)
            {
                // Check if it's a linked distance
                if (d.LinkedDistance > 0)
                {
                    Log.D("Timing.TimingWorker", $"Linked distance found. {d.LinkedDistance}");
                    // Verify we know the distance it's linked to.
                    if (!Dictionary.DistanceDictionary.TryGetValue(d.LinkedDistance, out Distance? distVal))
                    {
                        Log.E("Timing.TimingWorker", "Unable to find linked distance.");
                    }
                    else
                    {
                        Log.D("Timing.TimingWorker", $"Setting linked dictionaries. Ranking: {d.Ranking}");
                        // Set linked distance for ranking as the linked distance and set ranking int.
                        Dictionary.LinkedDistanceDictionary[d.Name] = (distVal, d.Ranking);
                        Dictionary.LinkedDistanceIdentifierDictionary[d.Identifier] = distVal.Identifier;
                        // Set end time for linked distance to linked distances end time.
                        Dictionary.DistanceEndDict[d.Identifier] = (Dictionary.DistanceStartDict[d.Identifier].Seconds + distVal.EndSeconds, Dictionary.DistanceStartDict[d.Identifier].Milliseconds);
                    }
                }
                else
                {
                    Log.D("Timing.TimingWorker", "Setting linked dictionaries (no linked distance found). Ranking: 0");
                    // No linked distance found, use distance and 0 as ranking int.
                    Dictionary.LinkedDistanceDictionary[d.Name] = (d, 0);
                    Dictionary.LinkedDistanceIdentifierDictionary[d.Identifier] = d.Identifier;
                    // not a linked distance, add it to mainDistances so we can check if there's only one distance
                    Dictionary.MainDistances.Add(d);
                }
            }
            Dictionary.Apis.Clear();
            foreach (ApiObject api in database.GetAllApi())
            {
                Dictionary.Apis[api.Identifier] = api;
            }
            // Clear distance segment list if no distance values are set
            List<int> distanceNotSet = [];
            // Sort the segments in our dictionary.
            foreach (List<Segment> segments in Dictionary.DistanceSegmentOrder.Values)
            {
                int distanceCount = 0;
                int distanceId = -1;
                foreach (Segment segment in segments)
                {
                    distanceId = segment.DistanceId;
                    if (segment.CumulativeDistance > 0)
                    {
                        distanceCount += 1;
                    }
                }
                if (distanceCount == segments.Count)
                {
                    segments.Sort((x1, x2) => x1.CumulativeDistance.CompareTo(x2.CumulativeDistance));
                }
                else
                {
                    distanceNotSet.Add(distanceId);
                }
            }
            // remove all that we didn't find with distances specified
            foreach (int distanceId in distanceNotSet)
            {
                Dictionary.DistanceSegmentOrder.Remove(distanceId);
            }
            RecalculateDns(theEvent);
        }

        private void RecalculateDns(Event theEvent)
        {
            // Get a list of DNS entries.
            Dictionary.DnsChips.Clear();
            Dictionary.DnsBibs.Clear();
            List<ChipRead> dnsReads = database.GetDnsChipReads(theEvent.Identifier);
            foreach (ChipRead read in dnsReads)
            {
                Dictionary.DnsChips.Add(read.ChipNumber);
                if (Dictionary.ChipToBibDictionary.TryGetValue(read.ChipNumber, out string? oBib))
                {
                    Dictionary.DnsBibs.Add(oBib);
                }
            }
            Dictionary.DnsEntryCount = dnsReads.Count;
        }

        public async void Run()
        {
            try
            {
                do
                {
                    Log.D("Timing.TimingWorker", "Lock Wait 05");
                    Semaphore.WaitOne();        // Wait for work.
                    if (WorkLock.TryEnter(3000))    // Check if we've been told to quit.
                    {                           // Do that here so we don't try to process another loop after being told to quit.
                        try
                        {
                            if (quittingTime)
                            {
                                break;
                            }
                        }
                        finally
                        {
                            WorkLock.Exit();
                        }
                    }
                    else
                    {
                        break;
                    }
                    Event theEvent = database.GetCurrentEvent()!;
                    // ensure the event exists and we've got unprocessed reads
                    if (theEvent.Identifier == -1) continue;
                    Log.D("Timing.TimingWorker", "Lock Wait 06");
                    if (ResetDictionariesLock.TryEnter(3000))
                    {
                        try
                        {
                            if (resetDictionariesBool)
                            {
                                RecalculateDictionaries(theEvent);
                            }
                            resetDictionariesBool = false;
                        }
                        finally
                        {
                            ResetDictionariesLock.Exit();
                        }
                    }
                    bool touched = false;
                    // Check if we have new DNS entries and reset if necessary.
                    if (database.GetDnsChipReads(theEvent.Identifier).Count > Dictionary.DnsEntryCount)
                    {
                        RecalculateDns(theEvent);
                    }
                    // Process chip reads first.
                    if (database.UnprocessedReadsExist(theEvent.Identifier))
                    {
                        Log.D("Timing.TimingWorker", "Unprocessed reads exist.");
#if DEBUG
                        DateTime start = DateTime.Now;
#endif
                        switch (theEvent.EventType)
                        {
                            // If RACE TYPE is DISTANCE
                            case Constants.Timing.EVENT_TYPE_DISTANCE:
                                _ = DistanceRoutine.ProcessRace(theEvent, database, Dictionary, window);
                                touched = true;
                                break;
                            // Else RACE TYPE is TIME
                            case Constants.Timing.EVENT_TYPE_TIME:
                                _ = TimeRoutine.ProcessRace(theEvent, database, Dictionary, window);
                                touched = true;
                                break;
                            // Else if RACE TYPE is BACKYARD_ULTRA
                            case Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA:
                                _ = BackyardUltraRoutine.ProcessRace(theEvent, database, Dictionary, window);
                                touched = true;
                                break;
                        }
#if DEBUG
                        DateTime end = DateTime.Now;
                        TimeSpan time = end - start;
                        Log.D("Timing.TimingWorker", $"Time to process all chip reads was: {time.Hours} hours {time.Minutes} minutes {time.Seconds} seconds {time.Milliseconds} milliseconds");
#endif
                    }
                    // Now process Results that aren't ranked.
                    if (database.UnprocessedResultsExist(theEvent.Identifier))
                    {
                        Log.D("Timing.TimingWorker", "Unprocessed results exist.");
#if DEBUG
                        DateTime start = DateTime.Now;
#endif
                        switch (theEvent.EventType)
                        {
                            // If RACE TYPE is DISTANCE
                            case Constants.Timing.EVENT_TYPE_DISTANCE:
                                _ = DistanceRoutine.ProcessPlacements(theEvent, database, Dictionary);
                                touched = true;
                                break;
                            // Else if RACE TYPE is TIME
                            case Constants.Timing.EVENT_TYPE_TIME:
                                TimeRoutine.ProcessLapTimes(theEvent, database);
                                _ = TimeRoutine.ProcessPlacements(theEvent, database, Dictionary);
                                touched = true;
                                break;
                            // Else if RACE TYPE is BACKYARD_ULTRA
                            case Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA:
                                _ = BackyardUltraRoutine.ProcessPlacements(theEvent, database, Dictionary);
                                touched = true;
                                break;
                        }
#if DEBUG
                        DateTime end = DateTime.Now;
                        TimeSpan time = end - start;
                        Log.D("Timing.TimingWorker", $"Time to process placements was: {time.Hours} hours {time.Minutes} minutes {time.Seconds} seconds {time.Milliseconds} milliseconds");
#endif
                        window.NetworkUpdateResults();
                    }
                    if (Constants.Timing.EVENT_TYPE_DISTANCE == theEvent.EventType) // && SMS set up && SMS enabled on event
                    {
                        // Build list of potential SMS Alerts to send out.
                        // First check for any alerts already sent out.
                        List<(int, int)> alerts = database.GetSmsAlerts(theEvent.Identifier);
                        // If null, db lookup failed, so soft fail here.
                        {
                            DateTime now = DateTime.Now;
                            DateTime fifteenPrior = now.AddMinutes(-15);
                            // Changing alerts hashset to locally based and pulled from the database each time we try to send alerts
                            HashSet<(int, int)> alertsSent = [.. alerts];
                            // Check the finish results for results we can send SMS messages for.
                            List<TimeResult> smsResults = GetSmsResults(database.GetTimingResults(theEvent.Identifier), Dictionary.DistanceNameDictionary, alertsSent, fifteenPrior);
                            // Only process further if there are potential SMS results.
                            if (smsResults.Count > 0)
                            {
                                if (lastSubscriptionFetch.AddSeconds(30).CompareTo(now) < 0)
                                {
                                    ApiObject apiObject = database.GetApi(theEvent.ApiId)!;
                                    string[] eventIds = theEvent.ApiEventId.Split(',');
                                    if (eventIds.Length == 2)
                                    {
                                        try
                                        {
                                            GetSmsSubscriptionsResponse subscriptionResponse = await ApiHandlers.GetSmsSubscriptions(apiObject, eventIds[0], eventIds[1]);
                                            // delete old then upload all the new subscriptions
                                            // this is just to make sure that we remove anyone who may have unsubscribed
                                            database.DeleteSmsSubscriptions(theEvent.Identifier);
                                            database.AddSmsSubscriptions(theEvent.Identifier, subscriptionResponse.Subscriptions);
                                            lastSubscriptionFetch = now;
                                        }
                                        catch
                                        {
                                            Log.E("Timing.TimingWorker", "Exception getting sms subscriptions.");
                                        }
                                    }
                                }
                                Dictionary<string, string> nameToBibDict = GetNameToBib(database.GetParticipants(theEvent.Identifier));
                                Dictionary<string, HashSet<string>> bibToPhonesDict = GetBibToPhones(database.GetSmsSubscriptions(theEvent.Identifier), nameToBibDict);
                                Dictionary<TimeResult, HashSet<string>> toSendTo = GetPhonesToText(smsResults, bibToPhonesDict);
                                // Only check banned phones or try to send texts if there is something to send.
                                if (toSendTo.Count > 0)
                                {
                                    // Update banned phones list.
                                    GlobalVars.UpdateBannedPhones();
                                    Dictionary.Apis.TryGetValue(theEvent.ApiId, out ApiObject? api);
                                    SendSmsAlerts(
                                        toSendTo,
                                        theEvent,
                                        Dictionary.MainDistances.Count > 1,
                                        GetResultsUrl(theEvent, api),
                                        database,
                                        SendSmsAlert,
                                        GlobalVars.AddBannedPhone
                                        );
                                }
                            }
                        }
                    }
                    if (!touched) continue;
                    if (ResultsLock.TryEnter(3000))
                    {
                        try
                        {
                            newResults = true;
                        }
                        finally
                        {
                            ResultsLock.Exit();
                        }
                    }
                    window.UpdateTiming();
                } while (true);
            }
            catch (Exception e)
            {
                Log.D("Timing.TimingWorker", $"Error with run function. {e}");
            }
        }
    }
}

