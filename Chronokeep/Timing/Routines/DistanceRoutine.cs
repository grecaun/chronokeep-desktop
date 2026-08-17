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

using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Chronokeep.Timing.Routines
{
    internal static class DistanceRoutine
    {
        public static List<TimeResult> ProcessRace(Event theEvent, IdbInterface database, TimingDictionary dictionary, IMainWindow window)
        {
            Log.D("Timing.Routines.DistanceRoutine", "Processing chip reads for a distance based event.");
            // Check if there's anything to process.
            // Pre-process information we'll need to fully process chip reads
            // Get start TimeResults
            Dictionary<string, TimeResult> startTimes = [];
            foreach (TimeResult result in database.GetStartTimes(theEvent.Identifier))
            {
                startTimes[result.Identifier] = result;
            }
            // Get finish TimeResults
            Dictionary<string, TimeResult> finishTimes = [];
            foreach (TimeResult result in database.GetFinishTimes(theEvent.Identifier))
            {
                finishTimes[result.Identifier] = result;
            }
            // Get the last known time we've seen each participant
            Dictionary<int, TimeResult> lastSeen = [];
            foreach (TimeResult result in database.GetTimingResults(theEvent.Identifier))
            {
                // if there is no time result,
                // or we've seen the person but our time is BEFORE the one we're looking at
                // set the last seen as this result
                if (!lastSeen.TryGetValue(result.EventSpecificId, out TimeResult? lastSee)
                    || lastSee.SystemTime < result.SystemTime)
                {
                    lastSeen[result.EventSpecificId] = result;
                }
            }
            // Get all the Chip Reads we find useful (Unprocessed, and those used as a result.)
            // and then sort them into groups based upon Bib, Chip, or put them in the ignore pile if
            // they have no bib or chip.
            Dictionary<string, List<ChipRead>> bibReadPairs = [];
            Dictionary<string, List<ChipRead>> chipReadPairs = [];
            // Make sure we keep track of the
            // last occurrence for a person at a specific location.
            // (Bib, Location), Last Chip Read
            Dictionary<(string, int), (ChipRead Read, int Occurrence)> bibLastReadDictionary = [];
            Dictionary<(string, int), (ChipRead Read, int Occurrence)> chipLastReadDictionary = [];
            // Keep a list of DNF participants so we can mark them as DNF in results.
            // Keep a record of the DNF chip read so we can link it with the TimeResult
            Dictionary<string, ChipRead> bibDnfDictionary = [];
            Dictionary<string, ChipRead> chipDnfDictionary = [];
            // Keep a list of DNS participants so we can mark them as DNS in results.
            // Keep a record of the DNS chip read so we can link it with the TimeResult
            Dictionary<string, ChipRead> bibDnsDictionary = [];
            Dictionary<string, ChipRead> chipDnsDictionary = [];

            List<ChipRead> allChipReads = database.GetUsefulChipReads(theEvent.Identifier);
            allChipReads.Sort();
            List<ChipRead> setUnknown = [];

            // Create a dictionary for keeping track of all of our chip reads.
            Dictionary<int, ChipRead> chipReadDict = [];

            // Get some variables to check if we need to sound an alarm.
            // Get a time value to check to ensure the chip read isn't too far in the past.
            DateTime? before = DateTime.Now.AddMinutes(-5);
            (Dictionary<string, Alarm> bibAlarms, Dictionary<string, Alarm> chipAlarms) = Alarm.GetAlarmDictionaries();

            foreach (ChipRead read in allChipReads)
            {
                chipReadDict[read.ReadId] = read;
                // Check to set off an alarm.
                if (read.Time > before)
                {
                    // Bib set on the read, alarm exists, and it has not went off.
                    if (read.Bib.Length > 0 && read.Bib != Constants.Timing.CHIPREAD_DUMMYBIB
                                            && bibAlarms.TryGetValue(read.Bib, out Alarm? alarm1) && alarm1.Enabled)
                    {
                        window.NotifyAlarm(read.Bib, "");
                    }
                    // Bib not set, chip is set, alarm exists, and it has not went off.
                    else if (read.ChipNumber != Constants.Timing.CHIPREAD_DUMMYCHIP
                        && chipAlarms.TryGetValue(read.ChipNumber, out Alarm? alarm2) && alarm2.Enabled)
                    {
                        window.NotifyAlarm("", read.ChipNumber);
                    }
                }
                if (read.Bib != Constants.Timing.CHIPREAD_DUMMYBIB)
                {
                    // Start by checking if we've got a record of the person not starting.
                    // If they are, we set them to AFTER_DNS.
                    // This status can be ignored later and won't be changed to DNS_IGNORE
                    // which would keep it as a DNS entry forever.
                    if (dictionary.DnsBibs.Contains(read.Bib))
                    {
                        if (read.Status != Constants.Timing.CHIPREAD_STATUS_DNS)
                        {
                            read.Status = Constants.Timing.CHIPREAD_STATUS_AFTER_DNS;
                        }
                        else
                        {
                            bibDnsDictionary.TryAdd(read.Bib, read);
                        }
                    }
                    else switch (read.Status)
                    {
                        // if we process all the used reads before putting them in the list
                        // we can ensure that all the reads we process are STATUS_NONE,
                        // and then we can verify that we aren't inserting results BEFORE
                        // results we've already calculated
                        case Constants.Timing.CHIPREAD_STATUS_USED:
                        {
                            if (!bibLastReadDictionary.TryGetValue((read.Bib, read.LocationId), out (ChipRead Read, int Occurrence) bLastReads))
                            {
                                bLastReads = (read, 0);
                            }
                            bibLastReadDictionary[(read.Bib, read.LocationId)] = (read, bLastReads.Occurrence + 1);
                            break;
                        }
                        case Constants.Timing.CHIPREAD_STATUS_STARTTIME when
                            (Constants.Timing.LOCATION_START == read.LocationId ||
                             (Constants.Timing.LOCATION_FINISH == read.LocationId && theEvent.CommonStartFinish)):
                            // If we haven't found anything, let us know what our start time was
                            bibLastReadDictionary.TryAdd((read.Bib, read.LocationId), (read, 0));
                            break;
                        case Constants.Timing.CHIPREAD_STATUS_DNF:
                            bibDnfDictionary[read.Bib] = read;
                            break;
                        default:
                        {
                            if (!bibReadPairs.TryGetValue(read.Bib, out List<ChipRead>? bibReads))
                            {
                                bibReads = [];
                                bibReadPairs[read.Bib] = bibReads;
                            }
                            bibReads.Add(read);
                            break;
                        }
                    }
                }
                else if (read.ChipNumber != Constants.Timing.CHIPREAD_DUMMYCHIP)
                {
                    // Start by checking if we've got a record of the person not starting.
                    // If they are, we set them to AFTER_DNS.
                    // This status can be ignored later and won't be changed to DNS_IGNORE
                    // which would keep it as a DNS entry forever.
                    if (dictionary.DnsChips.Contains(read.ChipNumber))
                    {
                        if (read.Status != Constants.Timing.CHIPREAD_STATUS_DNS)
                        {
                            read.Status = Constants.Timing.CHIPREAD_STATUS_AFTER_DNS;
                        }
                        else
                        {
                            chipDnsDictionary.TryAdd(read.ChipNumber, read);
                        }
                    }
                    else switch (read.Status)
                    {
                        // Otherwise check the status and everything as we did for Bib reads.
                        case Constants.Timing.CHIPREAD_STATUS_USED:
                        {
                            if (!chipLastReadDictionary.TryGetValue((read.ChipNumber, read.LocationId), out (ChipRead Read, int Occurrence) cLastReads))
                            {
                                cLastReads = (read, 0);
                            }
                            chipLastReadDictionary[(read.ChipNumber, read.LocationId)] = (read, cLastReads.Occurrence + 1);
                            break;
                        }
                        case Constants.Timing.CHIPREAD_STATUS_STARTTIME when
                            (Constants.Timing.LOCATION_START == read.LocationId ||
                             (Constants.Timing.LOCATION_FINISH == read.LocationId && theEvent.CommonStartFinish)):
                            // If we haven't found anything, let us know what our start time was
                            chipLastReadDictionary.TryAdd((read.ChipNumber, read.LocationId), (read, 0));
                            break;
                        case Constants.Timing.CHIPREAD_STATUS_DNF:
                            chipDnfDictionary[read.ChipNumber] = read;
                            break;
                        case Constants.Timing.CHIPREAD_STATUS_DNS:
                            dictionary.DnsChips.Add(read.ChipNumber);
                            break;
                        default:
                        {
                            if (!chipReadPairs.TryGetValue(read.ChipNumber, out List<ChipRead>? chipReads))
                            {
                                chipReads = [];
                                chipReadPairs[read.ChipNumber] = chipReads;
                            }
                            chipReads.Add(read);
                            break;
                        }
                    }
                }
                else
                {
                    setUnknown.Add(read);
                }
            }
            // Go through each chip read for a single person.
            List<TimeResult> newResults = [];
            // Keep a list of participants to update.
            HashSet<Participant> updateParticipants = [];
            // process reads that have a bib
            foreach (string bib in bibReadPairs.Keys)
            {
                Participant? part = dictionary.ParticipantBibDictionary.GetValueOrDefault(bib);
                Distance? d = part != null ? dictionary.DistanceDictionary[part.EventSpecific.DistanceIdentifier] : null;
                long startSeconds;
                int startMilliseconds;
                TimeResult? startResult = startTimes.GetValueOrDefault(TimeResult.BibToIdentifier(bib));
                if (d == null || !dictionary.DistanceStartDict.TryGetValue(d.Identifier, out (long Seconds, int Milliseconds) timeValue))
                {
                    startSeconds = dictionary.DistanceStartDict[0].Seconds;
                    startMilliseconds = dictionary.DistanceStartDict[0].Milliseconds;
                }
                else
                { // HERE WE WORK
                    startSeconds = timeValue.Seconds;
                    startMilliseconds = timeValue.Milliseconds;
                }
                long maxStartSeconds = startSeconds + theEvent.StartWindow;
                foreach (ChipRead read in bibReadPairs[bib].Where(read => Constants.Timing.CHIPREAD_STATUS_NONE == read.Status))
                {
                    // Check if we're before the start time.
                    if (read.TimeSeconds < startSeconds || (read.TimeSeconds == startSeconds && read.TimeMilliseconds < startMilliseconds))
                    {
                        read.Status = Constants.Timing.CHIPREAD_STATUS_PRESTART;
                    }
                    else
                    {
                        // If we're within the start period
                        // And the location is the Start, or we've got a combined start finish location
                        if ((read.TimeSeconds < maxStartSeconds || (read.TimeSeconds == maxStartSeconds && read.TimeMilliseconds <= startMilliseconds))
                            && (Constants.Timing.LOCATION_START == read.LocationId || (Constants.Timing.LOCATION_FINISH == read.LocationId && theEvent.CommonStartFinish)))
                        {
                            // check if we've stored a chip read as the start chip read, update it to unused if so
                            if (bibLastReadDictionary.TryGetValue((bib, read.LocationId), out (ChipRead Read, int Occurrence) bLastReads))
                            {
                                bLastReads.Read.Status = Constants.Timing.CHIPREAD_STATUS_UNUSEDSTART;
                            }
                            // Update the last read we've seen at this location
                            bibLastReadDictionary[(bib, read.LocationId)] = (read, 0);
                            // Check if we previously had a TimeResult for the start.
                            if (startResult != null && newResults.Contains(startResult))
                            {
                                // Remove it if so.
                                newResults.Remove(startResult);
                            }
                            // Create a result for the start value.
                            long secondsDiff = read.TimeSeconds - startSeconds;
                            int millisecondsDiff = read.TimeMilliseconds - startMilliseconds;
                            // If the distance is linked as a late distance, use the linked distance's start time as the gun time.
                            if (d is { Type: Constants.Timing.DISTANCE_TYPE_LATE }
                                && d.LinkedDistance != Constants.Timing.DISTANCE_DUMMYIDENTIFIER
                                && dictionary.DistanceStartDict.TryGetValue(d.LinkedDistance, out (long sec, int mill) linkedStart))
                            {
                                secondsDiff = read.TimeSeconds - linkedStart.sec;
                                millisecondsDiff = read.TimeMilliseconds - linkedStart.mill;
                            }
                            while (millisecondsDiff < 0)
                            {
                                secondsDiff--;
                                millisecondsDiff += 1000;
                            }
                            while (millisecondsDiff >= 1000)
                            {
                                secondsDiff++;
                                millisecondsDiff -= 1000;
                            }
                            startResult = new TimeResult(theEvent.Identifier,
                                read.ReadId,
                                part == null ? Constants.Timing.TIMERESULT_DUMMYPERSON : part.EventSpecific.Identifier,
                                read.LocationId,
                                Constants.Timing.SEGMENT_START,
                                0, // start reads are not an occurrence at the start line
                                secondsDiff,
                                millisecondsDiff,
                                TimeResult.BibToIdentifier(bib),
                                0,
                                0,
                                read.Time,
                                bib,
                                Constants.Timing.TIMERESULT_STATUS_NONE,
                                part == null ? "" : part.EventSpecific.Division
                            );
                            startTimes[startResult.Identifier] = startResult;
                            newResults.Add(startResult);
                            if (part is { Status: Constants.Timing.EVENTSPECIFIC_UNKNOWN }
                                && !bibDnfDictionary.ContainsKey(bib))
                            {
                                part.Status = Constants.Timing.EVENTSPECIFIC_STARTED;
                                updateParticipants.Add(part);
                            }
                            // Finally, set the chip read status to START TIME.
                            read.Status = Constants.Timing.CHIPREAD_STATUS_STARTTIME;
                        }
                        // Possible reads at this point:
                        //      Start Location reads past the StartWindow
                        //      Start/Finish Location reads past the StartWindow (Valid reads)
                        //          These could be BEFORE or AFTER the last occurrence at this spot
                        //      Reads at any other location
                        else
                        {
                            int maxOccurrences = 0;
                            switch (read.LocationId)
                            {
                                case Constants.Timing.LOCATION_FINISH:
                                    maxOccurrences = theEvent.FinishMaxOccurrences;
                                    break;
                                case Constants.Timing.LOCATION_START:
                                    maxOccurrences = theEvent.StartMaxOccurrences - 1;
                                    break;
                                default:
                                {
                                    if (dictionary.LocationDictionary.TryGetValue(read.LocationId, out TimingLocation? locationValue))
                                    {
                                        maxOccurrences = locationValue.MaxOccurrences;
                                    }
                                    else
                                    {
                                        Log.E("Timing.Routines.DistanceRoutine", "Somehow the location was not found.");
                                    }

                                    break;
                                }
                            }
                            int occurrence = 1;
                            int occursWithin = 0;
                            // Use the finish location ignore within parameter for redundant start reads since it's normal value is the start window.
                            if (read.LocationId is Constants.Timing.LOCATION_FINISH or Constants.Timing.LOCATION_START)
                            {
                                occursWithin = theEvent.FinishIgnoreWithin;
                            }
                            else if (dictionary.LocationDictionary.TryGetValue(read.LocationId, out TimingLocation? loc))
                            {
                                occursWithin = loc.IgnoreWithin;
                            }
                            // Minimum Time Value required to actually create a result
                            long minSeconds = startSeconds;
                            int minMilliseconds = startMilliseconds;
                            // Check if there's a previous read at this location.
                            if (bibLastReadDictionary.TryGetValue((bib, read.LocationId), out (ChipRead Read, int Occurrence) bLastReads))
                            {
                                occurrence = bLastReads.Occurrence + 1;
                                minSeconds = bLastReads.Read.TimeSeconds + occursWithin;
                                minMilliseconds = bLastReads.Read.TimeMilliseconds;
                            }
                            // Ensure when there's separate start finish lines that there is no finish within the ignore period after a start.
                            else if (!theEvent.CommonStartFinish && Constants.Timing.LOCATION_FINISH == read.LocationId && startResult != null)
                            {
                                minSeconds += startResult.Seconds + occursWithin;
                                minMilliseconds += startResult.Milliseconds;
                                if (minMilliseconds > 1000)
                                {
                                    minSeconds += 1;
                                    minMilliseconds -= 1000;
                                }
                            }
                            // Ensure that there are no reads within the StartWindow or the IgnoreWithin period after the start of a race.
                            else if (read.LocationId is Constants.Timing.LOCATION_FINISH or Constants.Timing.LOCATION_START)
                            {
                                // If no previous entry at this location, ensure we don't let a time pop up 
                                minSeconds += occursWithin;
                            }
                            // Verify we know which occurrence we're supposed to be at
                            if (part != null && d != null)
                            {
                                // The distanceId is either the participant's current distance
                                int distanceId = d.Identifier;
                                // the common distance ID
                                if (!theEvent.DistanceSpecificSegments)
                                {
                                    distanceId = Constants.Timing.COMMON_SEGMENTS_DISTANCEID;
                                }
                                // or the main (linked) distance for the person's distance
                                else if (dictionary.LinkedDistanceIdentifierDictionary.TryGetValue(distanceId, out int linkedDistance))
                                {
                                    distanceId = linkedDistance;
                                }
                                // Check if we know the last time the person was seen
                                if (lastSeen.TryGetValue(part.EventSpecific.Identifier, out TimeResult? lastResult)
                                    && dictionary.DistanceSegmentOrder.TryGetValue(distanceId, out List<Segment>? distanceSegments)
                                    && dictionary.SegmentByIdDictionary.TryGetValue(lastResult.SegmentId, out Segment? otherSeg))
                                {
                                    foreach (Segment seg in distanceSegments.Where(seg => seg.LocationId == read.LocationId && seg.CumulativeDistance > otherSeg.CumulativeDistance))
                                    {
                                        // if we are set to set the occurrence too low
                                        if (occurrence < seg.Occurrence)
                                        {
                                            // set it properly
                                            occurrence = seg.Occurrence;
                                        }
                                        // break the loop since the occurrence is correct
                                        break;
                                    }
                                }
                            }
                            // Check if we're past the max occurrences allowed for this spot.
                            // Also check if we've passed the finish occurrence for the finish line and that distance
                            // which requires an active distance and the person's information
                            if (occurrence > maxOccurrences ||
                                (d != null && Constants.Timing.LOCATION_FINISH == read.LocationId && occurrence > d.FinishOccurrence))
                            {
                                read.Status = Constants.Timing.CHIPREAD_STATUS_OVERMAX;
                            }
                            // occurrence is in [1,maxOccurrences], but can't be used because it's in the
                            // ignore period
                            else if (read.TimeSeconds < minSeconds || (read.TimeSeconds == minSeconds && read.TimeMilliseconds <= minMilliseconds))
                            {
                                read.Status = Constants.Timing.CHIPREAD_STATUS_WITHINIGN;
                            }
                            // Check if part of the DNF list
                            // And if the read is AFTER they were marked as DNF
                            else if (bibDnfDictionary.TryGetValue(bib, out ChipRead? dnfRead)
                                     && (dnfRead.TimeSeconds < read.TimeSeconds ||
                                         (dnfRead.TimeSeconds == read.TimeSeconds && dnfRead.TimeMilliseconds < read.TimeMilliseconds)))
                            {
                                Log.D("Timing.Routines.DistanceRoutine", $"bibDNFDictionary contains DNF for bib {bib}");
                                read.Status = Constants.Timing.CHIPREAD_STATUS_OVERMAX;
                            }
                            // occurrence is in [1, maxOccurrences] but not in the ignore period
                            else
                            {
                                bibLastReadDictionary[(bib, read.LocationId)] = (read, occurrence);
                                // Find if there's a segment associated with this combination
                                int segId = Constants.Timing.SEGMENT_NONE;
                                // With linked distances we want to ensure we use the Finish Occurence and Segments from the linked
                                // distance instead of the actual distance since those aren't set.
                                int distanceId = d?.Identifier ?? 0, distanceFinOcc = d?.FinishOccurrence ?? 0;
                                if (d is { LinkedDistance: > 0 })
                                {
                                    distanceId = d.LinkedDistance;
                                    distanceFinOcc = dictionary.DistanceDictionary.TryGetValue(d.LinkedDistance, out Distance? oDist) ? oDist.FinishOccurrence : d.FinishOccurrence;
                                }
                                // First check if we're using Distance specific segments
                                if (!theEvent.DistanceSpecificSegments && dictionary.SegmentDictionary.TryGetValue((Constants.Timing.COMMON_SEGMENTS_DISTANCEID, read.LocationId, occurrence), out Segment? oSeg))
                                {
                                    segId = oSeg.Identifier;
                                }
                                // Then check if we can find a segment
                                else if (d != null && dictionary.SegmentDictionary.TryGetValue((distanceId, read.LocationId, occurrence), out Segment? dSeg))
                                {
                                    segId = dSeg.Identifier;
                                }
                                // then check if it's the finish occurence. obviously this doesn't work if we can't find the distance
                                else if (d != null && occurrence == distanceFinOcc && Constants.Timing.LOCATION_FINISH == read.LocationId)
                                {
                                    segId = Constants.Timing.SEGMENT_FINISH;
                                }
                                string identifier = TimeResult.BibToIdentifier(bib);
                                // Create a result for the start value.
                                long secondsDiff = read.TimeSeconds - startSeconds;
                                int millisecondsDiff = read.TimeMilliseconds - startMilliseconds;
                                // If the distance is linked as a late distance, use the linked distance's start time as the gun time.
                                if (d is { Type: Constants.Timing.DISTANCE_TYPE_LATE }
                                    && d.LinkedDistance != Constants.Timing.DISTANCE_DUMMYIDENTIFIER
                                    && dictionary.DistanceStartDict.TryGetValue(d.LinkedDistance, out (long sec, int mill) linkedStart))
                                {
                                    secondsDiff = read.TimeSeconds - linkedStart.sec;
                                    millisecondsDiff = read.TimeMilliseconds - linkedStart.mill;
                                }
                                while (millisecondsDiff < 0)
                                {
                                    secondsDiff--;
                                    millisecondsDiff += 1000;
                                }
                                while (millisecondsDiff >= 1000)
                                {
                                    secondsDiff++;
                                    millisecondsDiff -= 1000;
                                }
                                bool startResExists = startTimes.TryGetValue(identifier, out TimeResult? startRes);
                                long chipSecondsDiff = read.TimeSeconds - (startResExists ? Constants.Timing.RfidDateToEpoch(startRes!.SystemTime) : startSeconds);
                                int chipMillisecondsDiff = read.TimeMilliseconds - (startResExists ? startRes!.SystemTime.Millisecond : startMilliseconds);
                                while (chipMillisecondsDiff < 0)
                                {
                                    chipSecondsDiff--;
                                    chipMillisecondsDiff += 1000;
                                }
                                while (chipMillisecondsDiff >= 1000)
                                {
                                    chipSecondsDiff++;
                                    chipMillisecondsDiff -= 1000;
                                }
                                // Check that we're not adding a finish time for a DNF person, we can use any other times
                                // for information for that person.
                                if (Constants.Timing.SEGMENT_FINISH != segId || !bibDnfDictionary.ContainsKey(bib))
                                {
                                    TimeResult newResult = new(theEvent.Identifier,
                                        read.ReadId,
                                        part == null ? Constants.Timing.TIMERESULT_DUMMYPERSON : part.EventSpecific.Identifier,
                                        read.LocationId,
                                        segId,
                                        occurrence,
                                        secondsDiff,
                                        millisecondsDiff,
                                        identifier,
                                        chipSecondsDiff,
                                        chipMillisecondsDiff,
                                        read.Time,
                                        bib,
                                        Constants.Timing.TIMERESULT_STATUS_NONE,
                                        part == null ? "" : part.EventSpecific.Division
                                    );
                                    newResults.Add(newResult);
                                    if (Constants.Timing.SEGMENT_FINISH == segId)
                                    {
                                        finishTimes[identifier] = newResult;
                                    }
                                    if (part != null)
                                    {
                                        lastSeen[part.EventSpecific.Identifier] = newResult;
                                        // If they've finished, mark them as such.
                                        if (Constants.Timing.SEGMENT_FINISH == segId
                                            && !bibDnfDictionary.ContainsKey(bib))
                                        {
                                            part.Status = Constants.Timing.EVENTSPECIFIC_FINISHED;
                                            updateParticipants.Add(part);
                                        }
                                        // If they were marked as no show previously, mark them as started
                                        else if (Constants.Timing.EVENTSPECIFIC_UNKNOWN == part.Status
                                                 && !bibDnfDictionary.ContainsKey(bib))
                                        {
                                            part.Status = Constants.Timing.EVENTSPECIFIC_STARTED;
                                            updateParticipants.Add(part);
                                        }
                                    }
                                }
                                read.Status = Constants.Timing.CHIPREAD_STATUS_USED;
                            }
                        }
                    }
                }
            }
            // process reads that have a chip
            foreach (string chip in chipReadPairs.Keys)
            {
                (long startSeconds, int startMilliseconds) = dictionary.DistanceStartDict[0];
                long maxStartSeconds = startSeconds + theEvent.StartWindow;
                TimeResult? startResult = null;
                foreach (ChipRead read in chipReadPairs[chip].Where(read => Constants.Timing.CHIPREAD_STATUS_NONE == read.Status))
                {
                    // Check if we're before the start time.
                    if (read.TimeSeconds < startSeconds || (read.TimeSeconds == startSeconds && read.TimeMilliseconds < startMilliseconds))
                    {
                        read.Status = Constants.Timing.CHIPREAD_STATUS_PRESTART;
                    }
                    else
                    {
                        // If we're within the start period
                        // And the location is the Start, or we've got a combined start finish location
                        if ((read.TimeSeconds < maxStartSeconds || (read.TimeSeconds == maxStartSeconds && read.TimeMilliseconds <= startMilliseconds)) &&
                            (Constants.Timing.LOCATION_START == read.LocationId
                             || (Constants.Timing.LOCATION_FINISH == read.LocationId
                                 && theEvent.CommonStartFinish)))
                        {
                            // check if we've stored a chip read as the start chip read, update it to unused if so
                            if (chipLastReadDictionary.TryGetValue((chip, read.LocationId), out (ChipRead Read, int Occurrence) cLastReads))
                            {
                                cLastReads.Read.Status = Constants.Timing.CHIPREAD_STATUS_UNUSEDSTART;
                            }
                            // Update the last read we've seen at this location
                            chipLastReadDictionary[(chip, read.LocationId)] = (read, 0);
                            // Check if we previously had a TimeResult for the start.
                            if (startResult != null)
                            {
                                // Remove it if so.
                                newResults.Remove(startResult);
                            }
                            string identifier = TimeResult.ChipToIdentifier(chip);
                            // Create a result for the start value.
                            long secondsDiff = read.TimeSeconds - startSeconds;
                            int millisecondsDiff = read.TimeMilliseconds - startMilliseconds;
                            while (millisecondsDiff < 0)
                            {
                                secondsDiff--;
                                millisecondsDiff += 1000;
                            }
                            while (millisecondsDiff >= 1000)
                            {
                                secondsDiff++;
                                millisecondsDiff -= 1000;
                            }
                            startResult = new TimeResult(theEvent.Identifier,
                                read.ReadId,
                                Constants.Timing.TIMERESULT_DUMMYPERSON,
                                read.LocationId,
                                Constants.Timing.SEGMENT_START,
                                0, // start reads are not an occurrence at the start line
                                secondsDiff,
                                millisecondsDiff,
                                identifier,
                                0,
                                0,
                                read.Time,
                                read.ChipBib == Constants.Timing.CHIPREAD_DUMMYBIB ? read.ReadBib : read.ChipBib,
                                Constants.Timing.TIMERESULT_STATUS_NONE,
                                ""
                            );
                            newResults.Add(startResult);
                            startTimes[startResult.Identifier] = startResult;
                            // Finally, set the chip read status to USED.
                            read.Status = Constants.Timing.CHIPREAD_STATUS_USED;
                        }
                        // Possible reads at this point:
                        //      Start Location reads past the StartWindow
                        //      Start/Finish Location reads past the StartWindow (Valid reads)
                        //      Reads at any other location
                        else
                        {
                            int maxOccurrences = 0;
                            switch (read.LocationId)
                            {
                                case Constants.Timing.LOCATION_FINISH:
                                    maxOccurrences = theEvent.FinishMaxOccurrences;
                                    break;
                                case Constants.Timing.LOCATION_START:
                                    maxOccurrences = theEvent.StartMaxOccurrences - 1;
                                    break;
                                default:
                                {
                                    if (!dictionary.LocationDictionary.TryGetValue(read.LocationId, out TimingLocation? oLoc))
                                    {
                                        Log.E("Timing.Routines.DistanceRoutine", "Somehow the location was not found.");
                                    }
                                    else
                                    {
                                        maxOccurrences = oLoc.MaxOccurrences;
                                    }

                                    break;
                                }
                            }
                            int occurrence = 1;
                            int occursWithin = 0;
                            if (read.LocationId is Constants.Timing.LOCATION_FINISH or Constants.Timing.LOCATION_START)
                            {
                                occursWithin = theEvent.FinishIgnoreWithin;
                            }
                            else if (dictionary.LocationDictionary.TryGetValue(read.LocationId, out TimingLocation? tLoc))
                            {
                                occursWithin = tLoc.IgnoreWithin;
                            }
                            // Minimum Time Value required to actually create a result
                            long minSeconds = startSeconds;
                            int minMilliseconds = startMilliseconds;
                            // Check if there's a previous read at this location.
                            if (chipLastReadDictionary.TryGetValue((chip, read.LocationId), out (ChipRead Read, int Occurrence) cLastReads))
                            {
                                occurrence = cLastReads.Occurrence + 1;
                                minSeconds = cLastReads.Read.TimeSeconds + occursWithin;
                                minMilliseconds = cLastReads.Read.TimeMilliseconds;
                            }
                            // Ensure when there's separate start finish lines that there is no finish within the ignore period after a start.
                            else if (!theEvent.CommonStartFinish && Constants.Timing.LOCATION_FINISH == read.LocationId && startResult != null)
                            {
                                minSeconds += startResult.Seconds + occursWithin;
                                minMilliseconds += startResult.Milliseconds;
                                if (minMilliseconds > 1000)
                                {
                                    minSeconds += 1;
                                    minMilliseconds -= 1000;
                                }
                            }
                            // Ensure that there are no reads within the StartWindow or the IgnoreWithin period after the start of a race.
                            else if (read.LocationId is Constants.Timing.LOCATION_FINISH or Constants.Timing.LOCATION_START)
                            {
                                // If no previous entry at this location, ensure we don't let a time pop up 
                                minSeconds += occursWithin;
                            }
                            // Check if we're past the max occurrences allowed for this spot.
                            if (occurrence > maxOccurrences)
                            {
                                read.Status = Constants.Timing.CHIPREAD_STATUS_OVERMAX;
                            }
                            // occurrence is in [1,maxOccurrences], but can't be used because it's in the
                            // ignore period
                            else if (read.TimeSeconds < minSeconds || (read.TimeSeconds == minSeconds && read.TimeMilliseconds < minMilliseconds))
                            {
                                read.Status = Constants.Timing.CHIPREAD_STATUS_WITHINIGN;
                            }
                            // Check if part of the DNF list
                            // And if the read is AFTER they were marked as DNF
                            else if (chipDnfDictionary.TryGetValue(chip, out ChipRead? dnfRead)
                                     && (dnfRead.TimeSeconds < read.TimeSeconds ||
                                         (dnfRead.TimeSeconds == read.TimeSeconds && dnfRead.TimeMilliseconds < read.TimeMilliseconds)))
                            {
                                Log.D("Timing.Routines.DistanceRoutine", $"chipDNFDictionary contains DNF for chip {chip}");
                                read.Status = Constants.Timing.CHIPREAD_STATUS_OVERMAX;
                            }
                            // occurrence is in [1, maxOccurrences] but not in the ignore period
                            else
                            {
                                chipLastReadDictionary[(chip, read.LocationId)] = (read, occurrence);
                                // Find if there's a segment associated with this combination
                                int segId = Constants.Timing.SEGMENT_NONE;
                                // First check if we're using Distance specific segments
                                if (!theEvent.DistanceSpecificSegments && dictionary.SegmentDictionary.TryGetValue((Constants.Timing.COMMON_SEGMENTS_DISTANCEID, read.LocationId, occurrence), out Segment? oSeg))
                                {
                                    segId = oSeg.Identifier;
                                }
                                string identifier = TimeResult.ChipToIdentifier(chip);
                                // Create a result for the start value.
                                long secondsDiff = read.TimeSeconds - startSeconds;
                                int millisecondsDiff = read.TimeMilliseconds - startMilliseconds;
                                while (millisecondsDiff < 0)
                                {
                                    secondsDiff--;
                                    millisecondsDiff += 1000;
                                }
                                while (millisecondsDiff >= 1000)
                                {
                                    secondsDiff++;
                                    millisecondsDiff -= 1000;
                                }
                                bool startResExists = startTimes.TryGetValue(identifier, out TimeResult? oStartRes);
                                long chipSecondsDiff = read.TimeSeconds - (startResExists ? Constants.Timing.RfidDateToEpoch(oStartRes!.SystemTime) : startSeconds);
                                int chipMillisecondsDiff = read.TimeMilliseconds - (startResExists ? oStartRes!.SystemTime.Millisecond : startMilliseconds);
                                while (chipMillisecondsDiff < 0)
                                {
                                    chipSecondsDiff--;
                                    chipMillisecondsDiff += 1000;
                                }
                                while (chipMillisecondsDiff >= 1000)
                                {
                                    chipSecondsDiff++;
                                    chipMillisecondsDiff -= 1000;
                                }
                                // Check that we're not adding a finish time for a DNF person, we can use any other times
                                // for information for that person.
                                if (Constants.Timing.SEGMENT_FINISH == segId && chipDnfDictionary.ContainsKey(chip))
                                    continue;
                                TimeResult newResult = new(theEvent.Identifier,
                                    read.ReadId,
                                    Constants.Timing.TIMERESULT_DUMMYPERSON,
                                    read.LocationId,
                                    segId,
                                    occurrence,
                                    secondsDiff,
                                    millisecondsDiff,
                                    identifier,
                                    chipSecondsDiff,
                                    chipMillisecondsDiff,
                                    read.Time,
                                    read.ChipBib == Constants.Timing.CHIPREAD_DUMMYBIB ? read.ReadBib : read.ChipBib,
                                    Constants.Timing.TIMERESULT_STATUS_NONE,
                                    ""
                                );
                                read.Status = Constants.Timing.CHIPREAD_STATUS_USED;
                                newResults.Add(newResult);
                                if (Constants.Timing.SEGMENT_FINISH == segId)
                                {
                                    finishTimes[identifier] = newResult;
                                }
                            }
                        }
                    }
                }
            }
            // Process the intersection of unknown DNF people and Finish results:
            foreach (string chip in chipDnfDictionary.Keys)
            {
                if (finishTimes.TryGetValue(TimeResult.ChipToIdentifier(chip), out TimeResult? finish))
                {
                    finish.ReadId = chipDnfDictionary[chip].ReadId;
                    finish.LocationId = chipDnfDictionary[chip].LocationId;
                    finish.Status = Constants.Timing.TIMERESULT_STATUS_DNF;
                    finish.Occurrence = theEvent.FinishMaxOccurrences;
                }
                else
                {
                    ChipRead dnfRead = chipDnfDictionary[chip];
                    finish = new TimeResult(theEvent.Identifier,
                        dnfRead.ReadId,
                        Constants.Timing.TIMERESULT_DUMMYPERSON,
                        dnfRead.LocationId,
                        Constants.Timing.SEGMENT_FINISH,
                        chipLastReadDictionary.TryGetValue((chip, Constants.Timing.LOCATION_FINISH), out (ChipRead Read, int Occurrence) chipLastReads) ? chipLastReads.Occurrence + 1 : 1,
                        0,
                        0,
                        TimeResult.ChipToIdentifier(chip),
                        0,
                        0,
                        dnfRead.Time,
                        dnfRead.ChipBib == Constants.Timing.CHIPREAD_DUMMYBIB ? dnfRead.ReadBib : dnfRead.ChipBib,
                        Constants.Timing.TIMERESULT_STATUS_DNF,
                        ""
                        );
                }
                finishTimes[TimeResult.ChipToIdentifier(chip)] = finish;
                newResults.Add(finish);
            }
            // Process the intersection of known DNF people and Finish results:
            foreach (string bib in bibDnfDictionary.Keys)
            {
                Participant? part = dictionary.ParticipantBibDictionary.GetValueOrDefault(bib);
                if (part != null)
                {
                    part.Status = Constants.Timing.EVENTSPECIFIC_DNF;
                    updateParticipants.Add(part);
                }
                int occurrence = part == null ? 1 : dictionary.DistanceDictionary.TryGetValue(part.EventSpecific.DistanceIdentifier, out Distance? oDist) ? oDist.FinishOccurrence : 1;
                if (finishTimes.TryGetValue(TimeResult.BibToIdentifier(bib), out TimeResult? finish))
                {
                    finish.ReadId = bibDnfDictionary[bib].ReadId;
                    finish.LocationId = bibDnfDictionary[bib].LocationId;
                    finish.Status = Constants.Timing.TIMERESULT_STATUS_DNF;
                    finish.Occurrence = occurrence;
                }
                else
                {
                    ChipRead bibDns = bibDnfDictionary[bib];
                    finish = new TimeResult(theEvent.Identifier,
                        bibDns.ReadId,
                        part == null ? Constants.Timing.TIMERESULT_DUMMYPERSON : part.EventSpecific.Identifier,
                        bibDns.LocationId,
                        Constants.Timing.SEGMENT_FINISH,
                        occurrence,
                        0,
                        0,
                        TimeResult.BibToIdentifier(bib),
                        0,
                        0,
                        bibDns.Time,
                        bib,
                        Constants.Timing.TIMERESULT_STATUS_DNF,
                        part == null ? "" : part.EventSpecific.Division
                        );
                }
                finishTimes[TimeResult.BibToIdentifier(bib)] = finish;
                newResults.Add(finish);
            }
            // Process the intersection of unknown DNS people and Finish results:
            foreach (string chip in chipDnsDictionary.Keys)
            {
                if (finishTimes.TryGetValue(TimeResult.ChipToIdentifier(chip), out TimeResult? finish))
                {
                    finish.ReadId = chipDnsDictionary[chip].ReadId;
                    finish.Status = Constants.Timing.TIMERESULT_STATUS_DNS;
                    finish.Occurrence = theEvent.FinishMaxOccurrences;
                }
                else
                {
                    ChipRead chipDns = chipDnsDictionary[chip];
                    finish = new TimeResult(theEvent.Identifier,
                        chipDns.ReadId,
                        Constants.Timing.TIMERESULT_DUMMYPERSON,
                        Constants.Timing.LOCATION_FINISH,
                        Constants.Timing.SEGMENT_FINISH,
                        chipLastReadDictionary.TryGetValue((chip, Constants.Timing.LOCATION_FINISH), out (ChipRead Read, int Occurrence) cLastReads) ? cLastReads.Occurrence + 1 : 1,
                        0,
                        0,
                        TimeResult.ChipToIdentifier(chip),
                        0,
                        0,
                        chipDns.Time,
                        chipDns.ChipBib == Constants.Timing.CHIPREAD_DUMMYBIB ? chipDns.ReadBib : chipDns.ChipBib,
                        Constants.Timing.TIMERESULT_STATUS_DNS,
                        ""
                        );
                }
                finishTimes[TimeResult.ChipToIdentifier(chip)] = finish;
                newResults.Add(finish);
            }
            // Process the intersection of known DNS people and Finish results:
            foreach (string bib in bibDnsDictionary.Keys)
            {
                Participant? part = dictionary.ParticipantBibDictionary.GetValueOrDefault(bib);
                if (part != null)
                {
                    part.Status = Constants.Timing.EVENTSPECIFIC_DNS;
                    updateParticipants.Add(part);
                }
                int occurrence = part == null ? 1 : dictionary.DistanceDictionary.TryGetValue(part.EventSpecific.DistanceIdentifier, out Distance? oDist) ? oDist.FinishOccurrence : 1;
                if (finishTimes.TryGetValue(TimeResult.BibToIdentifier(bib), out TimeResult? finish))
                {
                    finish.ReadId = bibDnsDictionary[bib].ReadId;
                    finish.Status = Constants.Timing.TIMERESULT_STATUS_DNS;
                    finish.Occurrence = occurrence;
                }
                else
                {
                    ChipRead bibDns = bibDnsDictionary[bib];
                    finish = new TimeResult(theEvent.Identifier,
                        bibDns.ReadId,
                        part == null ? Constants.Timing.TIMERESULT_DUMMYPERSON : part.EventSpecific.Identifier,
                        Constants.Timing.LOCATION_FINISH,
                        Constants.Timing.SEGMENT_FINISH,
                        occurrence,
                        0,
                        0,
                        TimeResult.BibToIdentifier(bib),
                        0,
                        0,
                        bibDns.Time,
                        bib,
                        Constants.Timing.TIMERESULT_STATUS_DNS,
                        part == null ? "" : part.EventSpecific.Division
                        );
                }
                finishTimes[TimeResult.BibToIdentifier(bib)] = finish;
                newResults.Add(finish);
            }
            // process reads that need to be set to ignore
            foreach (ChipRead read in setUnknown)
            {
                read.Status = Constants.Timing.CHIPREAD_STATUS_UNKNOWN;
            }
            // remove any results past the finish time
            List<TimeResult> toRemove = [];
            foreach (TimeResult res in newResults)
            {
                // Set all results that come after the finish to be removed -- Use SystemTime because DNF results set seconds to 0
                if (!finishTimes.TryGetValue(res.UnknownId, out TimeResult? finish)
                    || finish.SystemTime >= res.SystemTime) continue;
                toRemove.Add(res);
                if (chipReadDict.TryGetValue(res.ReadId, out ChipRead? oldRead))
                {
                    oldRead.Status = Constants.Timing.CHIPREAD_STATUS_AFTER_FINISH;
                }
            }
            newResults.RemoveAll(toRemove.Contains);
            // Update database with information.
            database.AddTimingResults(newResults);
            database.SetChipReadStatuses(allChipReads);
            database.UpdateParticipants([.. updateParticipants]);
            return newResults;
        }

        // Process timing placements for a distance based event.
        public static List<TimeResult> ProcessPlacements(Event theEvent, IdbInterface database, TimingDictionary dictionary)
        {
            List<TimeResult> output = [];
            // Create a dictionary so we can check if placements have changed. (place, location, occurrence, distance)
            Dictionary<(int, int, int, string), TimeResult> placementDictionary = [];
            // Get a list of all segments
            List<Segment> segments = database.GetSegments(theEvent.Identifier);
            Dictionary<int, List<TimeResult>> segmentDictionary = [];
            foreach (TimeResult result in database.GetTimingResults(theEvent.Identifier))
            {
                // We probably have unprocessed results in there, so only worry about results with a place set.
                // Make sure we're checking based on segmentId as well.
                if (result.Place > 0)
                {
                    placementDictionary[(result.Place, result.LocationId, result.Occurrence, result.DistanceName)] = result;
                }
                if (!segmentDictionary.TryGetValue(result.SegmentId, out List<TimeResult>? oSegList))
                {
                    oSegList = [];
                    segmentDictionary[result.SegmentId] = oSegList;
                }

                oSegList.Add(result);
            }
            // process results based upon the segment they're in
            foreach (Segment segment in segments)
            {
                Log.D("Timing.Routines.DistanceRoutine", $"Processing segment {segment.Name}");
                if (segmentDictionary.TryGetValue(segment.Identifier, out List<TimeResult>? oSegResults))
                {
                    output.AddRange(ProcessSegmentPlacements(theEvent, oSegResults, dictionary));
                }
            }
            Log.D("Timing.Routines.DistanceRoutine", "Processing finish results");
            if (segmentDictionary.TryGetValue(Constants.Timing.SEGMENT_FINISH, out List<TimeResult>? tSegResults))
            {
                output.AddRange(ProcessSegmentPlacements(theEvent, tSegResults, dictionary));
            }
            // Check if we should be re-uploading results because placements have changed.
            List<TimeResult> reUpload = [];
            Log.D("Timing.Routines.DistanceRoutine", "Checking for outdated placements.");
            foreach (TimeResult result in output)
            {
                if (!placementDictionary.TryGetValue(
                        (result.Place, result.LocationId, result.Occurrence, result.DistanceName),
                        out TimeResult? oRes) || oRes.Bib == result.Bib) continue;
                Log.D("Timing.Routines.DistanceRoutine", $"Outdated placement found. {result.ParticipantName} && {oRes.ParticipantName}");
                result.Uploaded = Constants.Timing.TIMERESULT_UPLOADED_FALSE;
                oRes.Uploaded = Constants.Timing.TIMERESULT_UPLOADED_FALSE;
                reUpload.Add(result);
                reUpload.Add(oRes);
            }
            database.AddTimingResults(output);
            database.SetUploadedTimingResults(reUpload);
            return output;
        }

        // Process segment placements.
        private static List<TimeResult> ProcessSegmentPlacements(Event theEvent,
            List<TimeResult> segmentResults,
            TimingDictionary dictionary)
        {
            // Clock and Mixed share the top three in each category but chip and mixed (sort of) share all the other rankings.
            if (theEvent.RankedBy != RankingType.Chip)
            {
                segmentResults.Sort((x1, x2) =>
                {
                    return CompareByClockTime(x1, x2, dictionary);
                });
            }
            else
            {
                segmentResults.Sort((x1, x2) =>
                {
                    return CompareByChipTime(x1, x2, dictionary);
                });
            }
            List<TimeResult> dnfResults = segmentResults.FindAll(x => x.IsDnf());
            foreach (TimeResult res in dnfResults)
            {
                res.Place = Constants.Timing.TIMERESULT_DUMMYPLACE;
                res.AgePlace = Constants.Timing.TIMERESULT_DUMMYPLACE;
                res.GenderPlace = Constants.Timing.TIMERESULT_DUMMYPLACE;
            }
            int removed = segmentResults.RemoveAll(x => x.IsDnf());
            Log.D("Timing.Routines.DistanceRoutine", $"{dnfResults.Count} Result(s) in DNFResults - {removed} Result(s) removed from segmentResults");
            // Get Dictionaries for storing ranked results (division, age group, gender, overall)
            // The key is as follows: (Distance ID, Division)
            Dictionary<(int, string), List<TimeResult>> divisionPlaceDictionary = [];
            // The key is as follows: (Distance ID, Age Group ID, Gender)
            Dictionary<(int, int, string), List<TimeResult>> ageGroupPlaceDictionary = [];
            // The key is as follows: (Distance ID, Gender)
            Dictionary<(int, string), List<TimeResult>> genderPlaceDictionary = [];
            // The key is as follows: (Distance ID)
            Dictionary<int, List<TimeResult>> placeDictionary = [];
            foreach (TimeResult result in segmentResults)
            {
                // Check if we know who the person is. Can't rank them if we don't know
                // what distance they're in, their age, or their gender
                if (dictionary.ParticipantEventSpecificDictionary.TryGetValue(result.EventSpecificId, out Participant? person))
                {
                    // Use a linked distance ID for ranking instead of a specific distance id.
                    if (!dictionary.LinkedDistanceIdentifierDictionary.TryGetValue(person.EventSpecific.DistanceIdentifier, out int distanceId))
                    {
                        distanceId = person.EventSpecific.DistanceIdentifier;
                    }
                    string gender = person.Gender.ToLower();
                    if (gender.Length < 1)
                    {
                        gender = "not specified";
                    }
                    int ageGroupId = person.EventSpecific.AgeGroupId;
                    // Results are sorted before the start. If no value found in the dictionary then none exist.
                    if (!placeDictionary.TryGetValue(distanceId, out List<TimeResult>? overallRankingList))
                    {
                        overallRankingList = [];
                    }
                    result.Place = overallRankingList.Count + 1;
                    overallRankingList.Add(result);
                    placeDictionary[distanceId] = overallRankingList;
                    if (!genderPlaceDictionary.TryGetValue((distanceId, gender), out List<TimeResult>? genderRankingList))
                    {
                        genderRankingList = [];
                    }
                    result.GenderPlace = genderRankingList.Count + 1;
                    genderRankingList.Add(result);
                    genderPlaceDictionary[(distanceId, gender)] = genderRankingList;
                    result.AgePlace = -1;
                    if (ageGroupId != Constants.Timing.TIMERESULT_DUMMYAGEGROUP)
                    {
                        if (!ageGroupPlaceDictionary.TryGetValue((distanceId, ageGroupId, gender), out List<TimeResult>? ageRankingList))
                        {
                            ageRankingList = [];
                        }
                        result.AgePlace = ageRankingList.Count + 1;
                        ageRankingList.Add(result);
                        ageGroupPlaceDictionary[(distanceId, ageGroupId, gender)] = ageRankingList;
                    }
                    string division = person.EventSpecific.Division.ToLower();
                    if (division.Length > 0)
                    {
                        if (!divisionPlaceDictionary.TryGetValue((distanceId, division), out List<TimeResult>? divisionRankingList))
                        {
                            divisionRankingList = [];
                        }
                        result.DivisionPlace = divisionRankingList.Count + 1;
                        divisionRankingList.Add(result);
                        divisionPlaceDictionary[(distanceId, division)] = divisionRankingList;
                    }
                }
                result.Status = Constants.Timing.TIMERESULT_STATUS_PROCESSED;
            }
            // Check if mixed type ranking -- if so shove the top three down as long as they're not the fastest by clock time
            if (theEvent.RankedBy == RankingType.Mixed)
            {
                foreach (List<TimeResult> results in placeDictionary.Values)
                {
                    // Remove top three - rankings don't change there.
                    if (results.Count <= 3) continue;
                    results.RemoveRange(0, 3);
                    results.Sort((x1, x2) =>
                    {
                        return CompareByChipTime(x1, x2, dictionary);
                    });
                    int place = 4;
                    foreach (TimeResult res in results)
                    {
                        res.Place = place;
                        place++;
                    }
                }
                foreach (List<TimeResult> results in genderPlaceDictionary.Values)
                {
                    // Remove top three - rankings don't change there.
                    if (results.Count <= 3) continue;
                    results.RemoveRange(0, 3);
                    results.Sort((x1, x2) =>
                    {
                        return CompareByChipTime(x1, x2, dictionary);
                    });
                    int place = 4;
                    foreach (TimeResult res in results)
                    {
                        res.GenderPlace = place;
                        place++;
                    }
                }
                foreach (List<TimeResult> results in ageGroupPlaceDictionary.Values)
                {
                    // Remove top three - rankings don't change there.
                    if (results.Count <= 3) continue;
                    results.RemoveRange(0, 3);
                    results.Sort((x1, x2) =>
                    {
                        return CompareByChipTime(x1, x2, dictionary);
                    });
                    int place = 4;
                    foreach (TimeResult res in results)
                    {
                        res.AgePlace = place;
                        place++;
                    }
                }
                foreach (List<TimeResult> results in divisionPlaceDictionary.Values)
                {
                    // Remove top three - rankings don't change there.
                    if (results.Count <= 3) continue;
                    results.RemoveRange(0, 3);
                    results.Sort((x1, x2) =>
                    {
                        return CompareByChipTime(x1, x2, dictionary);
                    });
                    int place = 4;
                    foreach (TimeResult res in results)
                    {
                        res.DivisionPlace = place;
                        place++;
                    }
                }
            }
            segmentResults.AddRange(dnfResults);
            return segmentResults;
        }

        public static int CompareByChipTime(TimeResult one, TimeResult two, TimingDictionary dictionary)
        {
            Distance? distance1 = null, distance2 = null;
            int rank1 = 0, rank2 = 0;
            // Get *linked* distances. (Could be that specific distance)
            if (dictionary.LinkedDistanceDictionary.TryGetValue(one.RealDistanceName, out (Distance, int) oneDistance))
            {
                (distance1, rank1) = oneDistance;
            }
            if (dictionary.LinkedDistanceDictionary.TryGetValue(one.RealDistanceName, out (Distance, int) twoDistance))
            {
                (distance2, rank2) = twoDistance;
            }
            // Check if they're in the same distance or a linked distance.
            if (distance1 == null || distance2 == null || distance1.Identifier != distance2.Identifier)
                return string.Compare(one.DistanceName, two.DistanceName, StringComparison.Ordinal);
            // Sort based on rank.  This is the linked distance new sorting item.
            if (rank1 != rank2) return rank1.CompareTo(rank2);
            return one.CompareChip(two);
        }

        public static int CompareByClockTime(TimeResult one, TimeResult two, TimingDictionary dictionary)
        {
            Distance? distance1 = null, distance2 = null;
            int rank1 = 0, rank2 = 0;
            // Get *linked* distances. (Could be that specific distance)
            if (dictionary.LinkedDistanceDictionary.TryGetValue(one.RealDistanceName, out (Distance, int) oneDistance))
            {
                (distance1, rank1) = oneDistance;
            }
            if (dictionary.LinkedDistanceDictionary.TryGetValue(one.RealDistanceName, out (Distance, int) twoDistance))
            {
                (distance2, rank2) = twoDistance;
            }
            // Check if they're in the same distance or a linked distance.
            if (distance1 == null || distance2 == null || distance1.Identifier != distance2.Identifier)
                return string.Compare(one.DistanceName, two.DistanceName, StringComparison.Ordinal);
            // Sort based on rank.  This is the linked distance new sorting item.
            if (rank1 != rank2) return rank1.CompareTo(rank2);
            return one.CompareClock(two);
        }
    }
}

