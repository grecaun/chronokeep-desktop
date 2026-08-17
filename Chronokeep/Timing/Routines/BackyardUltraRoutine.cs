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
    internal static class BackyardUltraRoutine
    {
        private const int DEFAULT_INTERVAL = 3600;
        private const int DEFAULT_MAX_INTERVALS = -1;

        // Process chip reads
        public static List<TimeResult> ProcessRace(Event theEvent, IdbInterface database, TimingDictionary dictionary, IMainWindow window)
        {
            Log.D("Timing.Routines.BackyardUltraRoutine", "Processing chip reads for a backyard ultra.");
            int interval = DEFAULT_INTERVAL;
            int maxIntervals = DEFAULT_MAX_INTERVALS;
            if (dictionary.DistanceDictionary.Count == 1)
            {
                foreach (Distance dist in dictionary.DistanceDictionary.Values)
                {
                    if (dist.StartOffsetSeconds > 0)
                    {
                        interval = dist.StartOffsetSeconds;
                    }
                    if (dist.EndSeconds > 0)
                    {
                        maxIntervals = dist.EndSeconds / interval;
                    }
                }
            }
            Log.D("Timing.Routines.BackyardUltraRoutine", $"Interval - {interval} // MaxIntervals - {maxIntervals}");
            // Pre-process information we'll need to fully process chip reads
            // Create a dictionary for hour starts and ends. (hour, identifier)
            Dictionary<(int, string), (TimeResult? start, TimeResult? end)> backyardResultDictionary = [];
            // The initial start times will always be hour 0 start times.
            foreach (TimeResult result in database.GetStartTimes(theEvent.Identifier))
            {
                // odd occurrences are going to be starts, divide by two to get the hour value
                backyardResultDictionary[(result.Occurrence / 2, result.Identifier)] = (start: result, end: null);
            }
            // Dictionary of time results for a specific identifier
            Dictionary<string, List<TimeResult>> finishTimes = [];
            // Keep track of the last LAP FINISH time for each person.
            Dictionary<string, TimeResult> bibLastLoopFinishDictionary = [];
            Dictionary<string, TimeResult> chipLastLoopFinishDictionary = [];
            // Keep track of the last completed hour per chip/bib
            Dictionary<string, int> bibLastFinishedHour = [];
            Dictionary<string, int> chipLastFinishedHour = [];
            List<TimeResult> toRemove = [];
            // Get the rest of the times.
            foreach (TimeResult result in database.GetFinishTimes(theEvent.Identifier))
            {
                // Keep track of all finish times based upon an identifier.
                if (!finishTimes.TryGetValue(result.Identifier, out List<TimeResult>? fTimes))
                {
                    fTimes = [];
                    finishTimes[result.Identifier] = fTimes;
                }
                fTimes.Add(result);
                if (result.Bib.Length > 0)
                {
                    if (!bibLastLoopFinishDictionary.TryGetValue(result.Bib, out TimeResult? res))
                    {
                        res = result;
                        bibLastLoopFinishDictionary[result.Bib] = res;
                    }
                    if (result.Occurrence > res.Occurrence)
                    {
                        bibLastLoopFinishDictionary[result.Bib] = result;
                    }
                    if (!bibLastFinishedHour.TryGetValue(result.Bib, out int hour) || hour < result.Occurrence / 2)
                    {
                        bibLastFinishedHour[result.Bib] = result.Occurrence / 2;
                    }
                }
                if (result.Chip.Length > 0)
                {
                    if (!chipLastLoopFinishDictionary.TryGetValue(result.Chip, out TimeResult? res))
                    {
                        res = result;
                        chipLastLoopFinishDictionary[result.Chip] = res;
                    }
                    if (result.Occurrence > res.Occurrence)
                    {
                        chipLastLoopFinishDictionary[result.Chip] = result;
                    }
                    if (!chipLastFinishedHour.TryGetValue(result.Chip, out int hour) || hour < result.Occurrence / 2)
                    {
                        chipLastFinishedHour[result.Chip] = result.Occurrence / 2;
                    }
                }
                // Pull out old results if they're in the dictionary already.
                if (!backyardResultDictionary.TryGetValue((result.Occurrence / 2, result.Identifier), out (TimeResult? start, TimeResult? end) tmpRes))
                {
                    tmpRes = (null, null);
                }
                switch (result.Occurrence % 2)
                {
                    // If start time
                    case 0:
                    {
                        if (tmpRes.start != null)
                        {
                            Log.D("Timing.Routines.BackyardUltraRoutine", "Found a duplicate start time for an hour.");
                            toRemove.Add(tmpRes.start);
                        }
                        tmpRes.start = result;
                        break;
                    }
                    // If end time
                    case 1 when tmpRes.end == null:
                        tmpRes.end = result;
                        break;
                    case 1:
                        Log.D("Timing.Routines.BackyardUltraRoutine", "Found a duplicate end time for an hour.");
                        toRemove.Add(result);
                        break;
                    // Modification 2 should result in either a 0 or a 1, this code should be unreachable.
                    default:
                        Log.E("Timing.Routines.BackyardUltraRoutine", "Made it to code that should be unreachable somehow.");
                        break;
                }
                // Update dictionary.
                backyardResultDictionary[(result.Occurrence / 2, result.Identifier)] = tmpRes;
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
            // Keep a record of the DNF chip read so we can link it with the TimeResult.
            Dictionary<string, int> dnfHourDictionary = [];
            Dictionary<string, ChipRead> bibDnfDictionary = [];
            Dictionary<string, ChipRead> chipDnfDictionary = [];
            // Keep a list of DNS participants so we can mark them as DNS in results.
            // Keep a record of the DNS chip read so we can link it with the TimeResult
            Dictionary<string, ChipRead> bibDnsDictionary = [];
            Dictionary<string, ChipRead> chipDnsDictionary = [];

            // Get all useful chip reads.
            List<ChipRead> allChipReads = database.GetUsefulChipReads(theEvent.Identifier);
            allChipReads.Sort();
            List<ChipRead> setUnknown = [];

            // Get some variables to check if we need to sound an alarm.
            // Get a time value to check to ensure the chip read isn't too far in the past.
            DateTime before = DateTime.Now.AddMinutes(-5);
            (Dictionary<string, Alarm> bibAlarms, Dictionary<string, Alarm> chipAlarms) = Alarm.GetAlarmDictionaries();

            // Sort chip reads into proper piles.
            foreach (ChipRead read in allChipReads)
            {
                // This is a start time for a loop
                // Calculate the hour for getting the correct occurrence.
                long startSeconds = dictionary.DistanceStartDict[0].Seconds;
                int startMilliseconds = dictionary.DistanceStartDict[0].Milliseconds;
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
                int hour = (int)(secondsDiff / interval);
                // Check to set off an alarm.
                if (read.Time > before)
                {
                    // Bib set on the read, alarm exists, and it has not went off.
                    if (read.Bib != Constants.Timing.CHIPREAD_DUMMYBIB
                        && bibAlarms.TryGetValue(read.Bib, out Alarm? oAlarm)
                        && oAlarm.Enabled)
                    {
                        window.NotifyAlarm(read.Bib, "");
                    }
                    // Bib not set, chip is set, alarm exists, and it has not went off.
                    else if (read.ChipNumber != Constants.Timing.CHIPREAD_DUMMYCHIP
                        && chipAlarms.TryGetValue(read.ChipNumber, out Alarm? tAlarm)
                        && tAlarm.Enabled)
                    {
                        window.NotifyAlarm("", read.ChipNumber);
                    }
                }
                // Check if we're past the number of intervals (hours) the event is going to run for.
                if (maxIntervals > 0 && maxIntervals <= hour)
                {
                    // if so, set to...
                    read.Status = Constants.Timing.CHIPREAD_STATUS_AFTER_FINISH;
                }
                // Process reads with known bib numbers.
                else if (read.Bib != Constants.Timing.CHIPREAD_DUMMYBIB)
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
                        // results we've already calculated.
                        // Check if it's a read we've used for a finish read.
                        case Constants.Timing.CHIPREAD_STATUS_USED:
                        {
                            bibLastReadDictionary[(read.Bib, read.LocationId)] = (read, (hour * 2) + 1);
                            // Check if we know the last hour they finished, if we do not know it, or we do, and it's before this hour
                            // then update the hour value
                            if (bibLastFinishedHour.TryGetValue(read.Bib, out int lastHour) || lastHour < hour)
                            {
                                bibLastFinishedHour[read.Bib] = hour;
                            }

                            break;
                        }
                        // Otherwise if it's a start read at the proper location.
                        case Constants.Timing.CHIPREAD_STATUS_STARTTIME when
                            (Constants.Timing.LOCATION_START == read.LocationId ||
                             (Constants.Timing.LOCATION_FINISH == read.LocationId && theEvent.CommonStartFinish)):
                            // This is a start time for a loop
                            bibLastReadDictionary[(read.Bib, read.LocationId)] = (read, hour * 2);
                            break;
                        // If it's a DNF read
                        case Constants.Timing.CHIPREAD_STATUS_DNF:
                        case Constants.Timing.CHIPREAD_STATUS_AUTO_DNF:
                            bibDnfDictionary[read.Bib] = read;
                            dnfHourDictionary[TimeResult.BibToIdentifier(read.Bib)] = hour;
                            break;
                        default:
                        {
                            if (!bibReadPairs.TryGetValue(read.Bib, out List<ChipRead>? readPairs))
                            {
                                readPairs = [];
                                bibReadPairs[read.Bib] = readPairs;
                            }

                            readPairs.Add(read);
                            break;
                        }
                    }
                }
                // Process reads with unknown bib numbers but known chip numbers.
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
                            chipLastReadDictionary[(read.ChipNumber, read.LocationId)] = (read, (hour * 2) + 1);
                            // Check if we know the last hour they finished, if we do not know it, or we do, and it's before this hour
                            // then update the hour value
                            if (chipLastFinishedHour.TryGetValue(read.ChipNumber, out int lastHour) || lastHour < hour)
                            {
                                chipLastFinishedHour[read.ChipNumber] = hour;
                            }
                            break;
                        }
                        case Constants.Timing.CHIPREAD_STATUS_STARTTIME when
                            (Constants.Timing.LOCATION_START == read.LocationId ||
                             (Constants.Timing.LOCATION_FINISH == read.LocationId && theEvent.CommonStartFinish)):
                            chipLastReadDictionary[(read.ChipNumber, read.LocationId)] = (read, hour * 2);
                            break;
                        case Constants.Timing.CHIPREAD_STATUS_DNF:
                        case Constants.Timing.CHIPREAD_STATUS_AUTO_DNF:
                            chipDnfDictionary[read.ChipNumber] = read;
                            dnfHourDictionary[TimeResult.ChipToIdentifier(read.ChipNumber)] = hour;
                            break;
                        default:
                        {
                            if (!chipReadPairs.TryGetValue(read.ChipNumber, out List<ChipRead>? readPairs))
                            {
                                readPairs = [];
                                chipReadPairs[read.ChipNumber] = readPairs;
                            }
                            readPairs.Add(read);
                            break;
                        }
                    }
                }
                // Set all other reads to unknown.
                else
                {
                    setUnknown.Add(read);
                }
            }

            // Go through each chip read for a single person.
            // This algorithm assumes it is processing every chip read in chronological order.
            // Reads not input into the system in the correct order will require 
            List<TimeResult> newResults = [];
            // Keep a list of participants to update.
            HashSet<Participant> updateParticipants = [];
            // Process reads that have a bib
            foreach (string bib in bibReadPairs.Keys)
            {
                Participant? part = dictionary.ParticipantBibDictionary.GetValueOrDefault(bib);
                // Go through each chip read
                foreach (ChipRead read in bibReadPairs[bib])
                {
                    // Calculate the hour for getting the correct occurrence.
                    long startSeconds = dictionary.DistanceStartDict[0].Seconds;
                    int startMilliseconds = dictionary.DistanceStartDict[0].Milliseconds;
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
                    int hour = (int)(secondsDiff / interval);
                    // Check that we haven't processed the read yet
                    if (Constants.Timing.CHIPREAD_STATUS_NONE != read.Status) continue;
                    // Check if we're before the start time.
                    if (read.TimeSeconds < startSeconds || (read.TimeSeconds == startSeconds && read.TimeMilliseconds < startMilliseconds))
                    {
                        read.Status = Constants.Timing.CHIPREAD_STATUS_PRESTART;
                    }
                    else
                    {
                        long secondsNoHour = secondsDiff % interval;
                        // Check if we've already included them in the DNF pile, and we're past the hour when they DNF-ed.
                        // Process the reads if this isn't the case.
                        if (!dnfHourDictionary.TryGetValue(TimeResult.BibToIdentifier(bib), out int oHour) || oHour > hour)
                        {
                            // Check if we're at the starting point and within a starting window
                            if ((Constants.Timing.LOCATION_START == read.LocationId || (Constants.Timing.LOCATION_FINISH == read.LocationId && theEvent.CommonStartFinish))
                                && (secondsNoHour < theEvent.StartWindow || (secondsNoHour == startSeconds && millisecondsDiff <= startMilliseconds)))
                            {
                                // check for a stored start chip read with the correct occurence (hour start)
                                if (bibLastReadDictionary.TryGetValue((bib, read.LocationId), out (ChipRead Read, int Occurrence) oLastRead) && oLastRead.Occurrence == (hour * 2))
                                {
                                    oLastRead.Read.Status = Constants.Timing.CHIPREAD_STATUS_UNUSEDSTART;
                                }
                                // Update the last read we've seen at this location
                                bibLastReadDictionary[(bib, read.LocationId)] = (read, hour * 2);
                                // check for start results in our list that we're pushing to the database and remove it if it is there
                                TimeResult? startResult = null;
                                if (backyardResultDictionary.TryGetValue((hour, TimeResult.BibToIdentifier(bib)), out (TimeResult? start, TimeResult? end) hourRes))
                                {
                                    startResult = hourRes.start;
                                }
                                if (startResult != null && newResults.Contains(startResult))
                                {
                                    newResults.Remove(startResult);
                                }
                                long chipSecondsDiff = secondsNoHour;
                                int chipMillisecondsDiff = millisecondsDiff;
                                if (bibLastLoopFinishDictionary.TryGetValue(bib, out TimeResult? lastFin) && lastFin.Occurrence < (hour * 2))
                                {
                                    chipSecondsDiff += lastFin.ChipSeconds;
                                    chipMillisecondsDiff += lastFin.ChipMilliseconds;
                                }
                                if (chipMillisecondsDiff >= 1000)
                                {
                                    chipSecondsDiff++;
                                    chipMillisecondsDiff -= 1000;
                                }
                                // Create a result for the start value.
                                if (hour == 0 || (bibLastFinishedHour.TryGetValue(read.Bib, out int lastHour) && lastHour == hour - 1))
                                {
                                    startResult = new TimeResult(theEvent.Identifier,
                                        read.ReadId,
                                        part == null ? Constants.Timing.TIMERESULT_DUMMYPERSON : part.EventSpecific.Identifier,
                                        read.LocationId,
                                        Constants.Timing.SEGMENT_START,
                                        hour * 2, // start reads are always set at their hour * 2 for occurence (0, 2, 4, 6, etc.)
                                        secondsDiff,
                                        millisecondsDiff,
                                        TimeResult.BibToIdentifier(bib),
                                        chipSecondsDiff,
                                        chipMillisecondsDiff,
                                        read.Time,
                                        bib,
                                        Constants.Timing.TIMERESULT_STATUS_NONE,
                                        part == null ? "" : part.EventSpecific.Division
                                    );
                                    if (!backyardResultDictionary.TryGetValue((hour, startResult.Identifier), out (TimeResult? start, TimeResult? end) startRes))
                                    {
                                        startRes = (start: null, end: null);
                                    }
                                    startRes.start = startResult;
                                    backyardResultDictionary[(hour, startResult.Identifier)] = startRes;
                                    newResults.Add(startResult);
                                    // Check if we should update the status of the person.
                                    if (part is { Status: Constants.Timing.EVENTSPECIFIC_UNKNOWN }
                                        && !bibDnfDictionary.ContainsKey(bib))
                                    {
                                        part.Status = Constants.Timing.EVENTSPECIFIC_STARTED;
                                        updateParticipants.Add(part);
                                    }
                                    // Finally, set the chip read status to START TIME
                                    read.Status = Constants.Timing.CHIPREAD_STATUS_STARTTIME;
                                }
                                else
                                {
                                    // DNF
                                    TimeResult newResult = new(
                                        theEvent.Identifier,
                                        read.ReadId,
                                        part == null ? Constants.Timing.TIMERESULT_DUMMYPERSON : part.EventSpecific.Identifier,
                                        read.LocationId,
                                        Constants.Timing.SEGMENT_FINISH,
                                        (lastHour * 2) + 3,
                                        secondsDiff,
                                        millisecondsDiff,
                                        TimeResult.BibToIdentifier(bib),
                                        chipSecondsDiff,
                                        chipMillisecondsDiff,
                                        read.Time,
                                        bib,
                                        Constants.Timing.TIMERESULT_STATUS_DNF,
                                        part == null ? "" : part.EventSpecific.Division
                                    );
                                    newResults.Add(newResult);
                                    read.Status = Constants.Timing.CHIPREAD_STATUS_AUTO_DNF;
                                    if (part != null)
                                    {
                                        part.Status = Constants.Timing.EVENTSPECIFIC_DNF;
                                        updateParticipants.Add(part);
                                    }
                                    if (startResult != null)
                                    {
                                        toRemove.Add(startResult);
                                    }
                                }
                            }
                            // Possible reads at this point:
                            //      Reads at the start not within the StartWindow (IGNORE)
                            //      Finish Location reads past the StartWindow (Valid Reads)
                            //          These could be BEFORE or AFTER the last occurrence at this spot
                            //      Reads at any other location
                            else if (Constants.Timing.LOCATION_FINISH == read.LocationId)
                            {
                                // find the hour results
                                if (!backyardResultDictionary.TryGetValue((hour, TimeResult.BibToIdentifier(bib)), out (TimeResult? start, TimeResult? end) hourRes))
                                {
                                    hourRes = (null, null);
                                }
                                // Check if this person has already finished in this hour.
                                if (hourRes.end != null)
                                {
                                    read.Status = Constants.Timing.CHIPREAD_STATUS_WITHINIGN;
                                }
                                // Otherwise THIS is (potentially) a finish.
                                else
                                {
                                    long chipSecondsDiff = secondsNoHour;
                                    int chipMillisecondsDiff = millisecondsDiff;
                                    if (bibLastLoopFinishDictionary.TryGetValue(bib, out TimeResult? lastFin) && lastFin.Occurrence < (hour * 2))
                                    {
                                        chipSecondsDiff += lastFin.ChipSeconds;
                                        chipMillisecondsDiff += lastFin.ChipMilliseconds;
                                    }
                                    if (chipMillisecondsDiff >= 1000)
                                    {
                                        chipSecondsDiff++;
                                        chipMillisecondsDiff -= 1000;
                                    }
                                    // Check if they finished the last hour (or this is the first hour)
                                    if (hour == 0 || (bibLastFinishedHour.TryGetValue(read.Bib, out int lastHour) && lastHour == hour - 1))
                                    {
                                        bibLastFinishedHour[bib] = hour;
                                        bibLastReadDictionary[(bib, read.LocationId)] = (read, (hour * 2) + 1);
                                        TimeResult newResult = new(theEvent.Identifier,
                                            read.ReadId,
                                            part == null ? Constants.Timing.TIMERESULT_DUMMYPERSON : part.EventSpecific.Identifier,
                                            read.LocationId,
                                            Constants.Timing.SEGMENT_FINISH,
                                            (hour * 2) + 1,
                                            secondsDiff,
                                            millisecondsDiff,
                                            TimeResult.BibToIdentifier(bib),
                                            chipSecondsDiff,
                                            chipMillisecondsDiff,
                                            read.Time,
                                            bib,
                                            Constants.Timing.TIMERESULT_STATUS_NONE,
                                            part == null ? "" : part.EventSpecific.Division
                                        );
                                        hourRes.end = newResult;
                                        backyardResultDictionary[(hour, TimeResult.BibToIdentifier(bib))] = hourRes;
                                        // This is a finish time, so update the last loop finish time IF out last value was the value before this
                                        // (or this is the first value)
                                        if ((lastFin == null && hour == 0) || lastFin!.Occurrence + 1 == (hour * 2))
                                        {
                                            bibLastLoopFinishDictionary[bib] = newResult;
                                        }
                                        newResults.Add(newResult);
                                        if (part != null)
                                        {
                                            // If they were marked as no show previously, mark them as started
                                            if (Constants.Timing.EVENTSPECIFIC_UNKNOWN == part.Status
                                                && !bibDnfDictionary.ContainsKey(bib))
                                            {
                                                part.Status = Constants.Timing.EVENTSPECIFIC_STARTED;
                                                updateParticipants.Add(part);
                                            }
                                        }
                                        read.Status = Constants.Timing.CHIPREAD_STATUS_USED;
                                    }
                                    else
                                    {
                                        // DNF
                                        TimeResult newResult = new(
                                            theEvent.Identifier,
                                            read.ReadId,
                                            part == null ? Constants.Timing.TIMERESULT_DUMMYPERSON : part.EventSpecific.Identifier,
                                            read.LocationId,
                                            Constants.Timing.SEGMENT_FINISH,
                                            (lastHour * 2) + 3,
                                            secondsDiff,
                                            millisecondsDiff,
                                            TimeResult.BibToIdentifier(bib),
                                            chipSecondsDiff,
                                            chipMillisecondsDiff,
                                            read.Time,
                                            bib,
                                            Constants.Timing.TIMERESULT_STATUS_DNF,
                                            part == null ? "" : part.EventSpecific.Division
                                        );
                                        newResults.Add(newResult);
                                        read.Status = Constants.Timing.CHIPREAD_STATUS_AUTO_DNF;
                                        if (part != null)
                                        {
                                            part.Status = Constants.Timing.EVENTSPECIFIC_DNF;
                                            updateParticipants.Add(part);
                                        }
                                        dnfHourDictionary[TimeResult.BibToIdentifier(bib)] = lastHour + 1;
                                    }
                                }
                            }
                            // Possible reads at this point:
                            //      Start location reads not within a start window...
                            //      Reads at any other location
                            else if (Constants.Timing.LOCATION_FINISH != read.LocationId)
                            {
                                // find the hour results and check if they've already finished.
                                if (backyardResultDictionary.TryGetValue((hour, TimeResult.BibToIdentifier(bib)), out (TimeResult? start, TimeResult? end) hourRes)
                                    && hourRes.end != null)
                                {
                                    read.Status = Constants.Timing.CHIPREAD_STATUS_WITHINIGN;
                                }
                                // Otherwise assume this could be a result.
                                else
                                {
                                    int occurrence = 1;
                                    int ignoreWithin = 0;
                                    if (dictionary.LocationDictionary.TryGetValue(read.LocationId, out TimingLocation? loc))
                                    {
                                        ignoreWithin = loc.IgnoreWithin;
                                    }
                                    // Get the minimum number of seconds we want to enforce between sightings at a spot
                                    // Start with 0 because they may not have a start time for one reason or another
                                    long minSeconds = 0;
                                    long minMilliseconds = 0;
                                    if (bibLastReadDictionary.TryGetValue((bib, read.LocationId), out (ChipRead Read, int Occurrence) bLastRead))
                                    {
                                        occurrence = bLastRead.Occurrence + 1;
                                        minSeconds = bLastRead.Read.TimeSeconds + ignoreWithin;
                                        minMilliseconds = bLastRead.Read.Milliseconds;
                                    }
                                    // Check if we're within the ignore period
                                    if (read.TimeSeconds < minSeconds || (read.TimeSeconds == minSeconds && millisecondsDiff <= minMilliseconds))
                                    {
                                        // and set it to ignore it
                                        read.Status = Constants.Timing.CHIPREAD_STATUS_WITHINIGN;
                                    }
                                    else
                                    {
                                        // These are results that are NOT at the finish line and are NOT finish times.
                                        bibLastReadDictionary[(bib, read.LocationId)] = (read, occurrence);
                                        long chipSecondsDiff = secondsNoHour;
                                        int chipMillisecondsDiff = millisecondsDiff;
                                        if (bibLastLoopFinishDictionary.TryGetValue(bib, out TimeResult? lastFin) && lastFin.Occurrence < (hour * 2))
                                        {
                                            chipSecondsDiff += lastFin.ChipSeconds;
                                            chipMillisecondsDiff += lastFin.ChipMilliseconds;
                                        }
                                        if (chipMillisecondsDiff >= 1000)
                                        {
                                            chipSecondsDiff++;
                                            chipMillisecondsDiff -= 1000;
                                        }
                                        TimeResult newResult = new(theEvent.Identifier,
                                            read.ReadId,
                                            part == null ? Constants.Timing.TIMERESULT_DUMMYPERSON : part.EventSpecific.Identifier,
                                            read.LocationId,
                                            Constants.Timing.SEGMENT_NONE,
                                            occurrence,
                                            secondsDiff,
                                            millisecondsDiff,
                                            TimeResult.BibToIdentifier(bib),
                                            chipSecondsDiff,
                                            chipMillisecondsDiff,
                                            read.Time,
                                            bib,
                                            Constants.Timing.TIMERESULT_STATUS_NONE,
                                            part == null ? "" : part.EventSpecific.Division
                                        );
                                        newResults.Add(newResult);
                                        if (part != null)
                                        {
                                            // If they were marked as no show previously, mark them as started
                                            if (Constants.Timing.EVENTSPECIFIC_UNKNOWN == part.Status
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
                            // Possible reads at this point:
                            //      Start location reads not within a start window...
                            else
                            {
                                read.Status = Constants.Timing.CHIPREAD_STATUS_UNUSEDSTART;
                            }
                        }
                        // Otherwise just ignore the reads.
                        else
                        {
                            read.Status = Constants.Timing.CHIPREAD_STATUS_OVERMAX;
                        }
                    }
                }

            }
            // Process reads that don't have an associated bib but do have a chip.
            foreach (string chip in chipReadPairs.Keys)
            {
                foreach (ChipRead read in chipReadPairs[chip])
                {
                    // Calculate the hour for getting the correct occurrence.
                    long startSeconds = dictionary.DistanceStartDict[0].Seconds;
                    int startMilliseconds = dictionary.DistanceStartDict[0].Milliseconds;
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
                    int hour = (int)(secondsDiff / interval);
                    // Check that we haven't processed the read yet
                    if (Constants.Timing.CHIPREAD_STATUS_NONE != read.Status) continue;
                    // Check if we're before the start time.
                    if (read.TimeSeconds < startSeconds || (read.TimeSeconds == startSeconds && read.TimeMilliseconds < startMilliseconds))
                    {
                        read.Status = Constants.Timing.CHIPREAD_STATUS_PRESTART;
                    }
                    else
                    {
                        long secondsNoHour = secondsDiff % interval;
                        // Check if we've already included them in the DNF pile, and we're past the hour when they DNF-ed.
                        // Process the reads if this isn't the case.
                        if (!dnfHourDictionary.TryGetValue(TimeResult.ChipToIdentifier(chip), out int dnfHr) || dnfHr > hour)
                        {
                            // Check if we're at the starting point and within a starting window
                            if ((Constants.Timing.LOCATION_START == read.LocationId || (Constants.Timing.LOCATION_FINISH == read.LocationId && theEvent.CommonStartFinish))
                                && (secondsNoHour < theEvent.StartWindow || (secondsNoHour == theEvent.StartWindow && millisecondsDiff == 0)))
                            {
                                // check for a stored start chip read with the correct occurence (hour start)
                                if (chipLastReadDictionary.TryGetValue((chip, read.LocationId), out (ChipRead Read, int Occurrence) oLastRead) && oLastRead.Occurrence == (hour * 2))
                                {
                                    oLastRead.Read.Status = Constants.Timing.CHIPREAD_STATUS_UNUSEDSTART;
                                }
                                // Update the last read we've seen at this location
                                chipLastReadDictionary[(chip, read.LocationId)] = (Read: read, Occurrence: hour * 2);
                                // check for start results in our list that we're pushing to the database and remove it if it is there
                                if (backyardResultDictionary.TryGetValue((hour, TimeResult.ChipToIdentifier(chip)), out (TimeResult? start, TimeResult? end) known) && known.start != null && newResults.Contains(known.start))
                                {
                                    newResults.Remove(known.start);
                                }
                                long chipSecondsDiff = secondsNoHour;
                                int chipMillisecondsDiff = millisecondsDiff;
                                if (chipLastLoopFinishDictionary.TryGetValue(chip, out TimeResult? lastFin) && lastFin.Occurrence < (hour * 2))
                                {
                                    chipSecondsDiff += lastFin.ChipSeconds;
                                    chipMillisecondsDiff += lastFin.ChipMilliseconds;
                                }
                                if (chipMillisecondsDiff >= 1000)
                                {
                                    chipSecondsDiff++;
                                    chipMillisecondsDiff -= 1000;
                                }
                                // Create a result for the start value.
                                if (hour == 0 || (bibLastFinishedHour.TryGetValue(read.Bib, out int lastHour) && lastHour == hour - 1))
                                {
                                    // Create a result for the start value.
                                    TimeResult startResult = new(theEvent.Identifier,
                                        read.ReadId,
                                        Constants.Timing.TIMERESULT_DUMMYPERSON,
                                        read.LocationId,
                                        Constants.Timing.SEGMENT_START,
                                        hour * 2, // start reads are always set at their hour * 2 for occurence (0, 2, 4, 6, etc.)
                                        secondsDiff,
                                        millisecondsDiff,
                                        TimeResult.ChipToIdentifier(chip),
                                        chipSecondsDiff,
                                        chipMillisecondsDiff,
                                        read.Time,
                                        read.ChipBib == Constants.Timing.CHIPREAD_DUMMYBIB ? read.ReadBib : read.ChipBib,
                                        Constants.Timing.TIMERESULT_STATUS_NONE,
                                        ""
                                    );
                                    if (!backyardResultDictionary.TryGetValue((hour, startResult.Identifier), out (TimeResult? start, TimeResult? end) oPrevRes))
                                    {
                                        oPrevRes = (null, null);
                                    }
                                    oPrevRes.start = startResult;
                                    backyardResultDictionary[(hour, startResult.Identifier)] = oPrevRes;
                                    newResults.Add(startResult);
                                    // Finally, set the chip read status to START TIME
                                    read.Status = Constants.Timing.CHIPREAD_STATUS_STARTTIME;
                                }
                                else
                                {
                                    // DNF
                                    TimeResult newResult = new(
                                        theEvent.Identifier,
                                        read.ReadId,
                                        Constants.Timing.TIMERESULT_DUMMYPERSON,
                                        read.LocationId,
                                        Constants.Timing.SEGMENT_FINISH,
                                        (lastHour * 2) + 3,
                                        secondsDiff,
                                        millisecondsDiff,
                                        TimeResult.ChipToIdentifier(chip),
                                        chipSecondsDiff,
                                        chipMillisecondsDiff,
                                        read.Time,
                                        read.ChipBib == Constants.Timing.CHIPREAD_DUMMYBIB ? read.ReadBib : read.ChipBib,
                                        Constants.Timing.TIMERESULT_STATUS_DNF,
                                        ""
                                    );
                                    newResults.Add(newResult);
                                    read.Status = Constants.Timing.CHIPREAD_STATUS_AUTO_DNF;
                                    dnfHourDictionary[TimeResult.ChipToIdentifier(chip)] = lastHour + 1;
                                    if (known.start != null)
                                    {
                                        toRemove.Add(known.start);
                                    }
                                }
                            }
                            // Possible reads at this point:
                            //      Reads at the start not within the StartWindow (IGNORE)
                            //      Start/Finish Location reads past the StartWindow (Valid Reads)
                            //          These could be BEFORE or AFTER the last occurrence at this spot
                            //      Reads at any other location
                            else if (Constants.Timing.LOCATION_FINISH == read.LocationId)
                            {
                                // find the hour results && check if this person has already finished in this hour.
                                if (backyardResultDictionary.TryGetValue((hour, TimeResult.ChipToIdentifier(chip)), out (TimeResult? start, TimeResult? end) oByRes) && oByRes.end != null)
                                {
                                    read.Status = Constants.Timing.CHIPREAD_STATUS_WITHINIGN;
                                }
                                else
                                {
                                    long chipSecondsDiff = secondsNoHour;
                                    int chipMillisecondsDiff = millisecondsDiff;
                                    // Verify if there was a finish time before this one
                                    if (chipLastLoopFinishDictionary.TryGetValue(chip, out TimeResult? lastFin) && lastFin.Occurrence < (hour * 2))
                                    {
                                        chipSecondsDiff += lastFin.ChipSeconds;
                                        chipMillisecondsDiff += lastFin.ChipMilliseconds;
                                    }
                                    if (chipMillisecondsDiff >= 1000)
                                    {
                                        chipSecondsDiff++;
                                        chipMillisecondsDiff -= 1000;
                                    }
                                    if (hour == 0 || (bibLastFinishedHour.TryGetValue(read.Bib, out int lastHour) && lastHour == hour - 1))
                                    {
                                        chipLastFinishedHour[chip] = hour;
                                        chipLastReadDictionary[(chip, read.LocationId)] = (read, (hour * 2) + 1);
                                        TimeResult newResult = new(theEvent.Identifier,
                                            read.ReadId,
                                            Constants.Timing.TIMERESULT_DUMMYPERSON,
                                            read.LocationId,
                                            Constants.Timing.SEGMENT_FINISH,
                                            (hour * 2) + 1,
                                            secondsDiff,
                                            millisecondsDiff,
                                            TimeResult.ChipToIdentifier(chip),
                                            chipSecondsDiff,
                                            chipMillisecondsDiff,
                                            read.Time,
                                            read.ChipBib == Constants.Timing.CHIPREAD_DUMMYBIB ? read.ReadBib : read.ChipBib,
                                            Constants.Timing.TIMERESULT_STATUS_NONE,
                                            ""
                                        );
                                        oByRes.end = newResult;
                                        backyardResultDictionary[(hour, TimeResult.ChipToIdentifier(chip))] = oByRes;
                                        // This is a finish time, so update the last loop finish time IF out last value was before this one
                                        // (or this is the first value)
                                        if ((lastFin == null && hour == 0) || lastFin!.Occurrence + 1 == (hour * 2))
                                        {
                                            chipLastLoopFinishDictionary[chip] = newResult;
                                        }
                                        newResults.Add(newResult);
                                        read.Status = Constants.Timing.CHIPREAD_STATUS_USED;
                                    }
                                    else
                                    {
                                        // DNF
                                        TimeResult newResult = new(
                                            theEvent.Identifier,
                                            read.ReadId,
                                            Constants.Timing.TIMERESULT_DUMMYPERSON,
                                            read.LocationId,
                                            Constants.Timing.SEGMENT_FINISH,
                                            (lastHour * 2) + 3,
                                            secondsDiff,
                                            millisecondsDiff,
                                            TimeResult.ChipToIdentifier(chip),
                                            chipSecondsDiff,
                                            chipMillisecondsDiff,
                                            read.Time,
                                            read.ChipBib == Constants.Timing.CHIPREAD_DUMMYBIB ? read.ReadBib : read.ChipBib,
                                            Constants.Timing.TIMERESULT_STATUS_NONE,
                                            ""
                                        );
                                        newResults.Add(newResult);
                                        read.Status = Constants.Timing.CHIPREAD_STATUS_AUTO_DNF;
                                        dnfHourDictionary[TimeResult.ChipToIdentifier(chip)] = lastHour + 1;
                                    }
                                }
                            }
                            else if (Constants.Timing.LOCATION_FINISH != read.LocationId)
                            {
                                // find the hour results && check if this person has already finished in this hour.
                                if (backyardResultDictionary.TryGetValue((hour, TimeResult.ChipToIdentifier(chip)), out (TimeResult? start, TimeResult? end) oByRes) && oByRes.end != null)
                                {
                                    read.Status = Constants.Timing.CHIPREAD_STATUS_WITHINIGN;
                                }
                                else
                                {
                                    int occurrence = 1;
                                    int ignoreWithin = 0;
                                    if (dictionary.LocationDictionary.TryGetValue(read.LocationId, out TimingLocation? loc))
                                    {
                                        ignoreWithin = loc.IgnoreWithin;
                                    }
                                    // Get the minimum number of seconds we want to enforce between start time for a loop and finish time
                                    // Start with 0 because they may not have a start time for one reason or another
                                    // Make sure to remove the hour portion of the last read chip time
                                    long minSeconds = 0;
                                    long minMilliseconds = 0;
                                    if (chipLastReadDictionary.TryGetValue((chip, read.LocationId), out (ChipRead Read, int Occurrence) oChipLast))
                                    {
                                        occurrence = oChipLast.Occurrence + 1;
                                        minSeconds = oChipLast.Read.TimeSeconds + ignoreWithin;
                                        minMilliseconds = oChipLast.Read.Milliseconds;
                                    }
                                    // Check if we're within the ignore period
                                    if (read.TimeSeconds < minSeconds || (read.TimeSeconds == minSeconds && millisecondsDiff <= minMilliseconds))
                                    {
                                        // and set it to ignore it
                                        read.Status = Constants.Timing.CHIPREAD_STATUS_WITHINIGN;
                                    }
                                    else
                                    {
                                        chipLastReadDictionary[(chip, read.LocationId)] = (read, occurrence);
                                        long chipSecondsDiff = secondsNoHour;
                                        int chipMillisecondsDiff = millisecondsDiff;
                                        if (chipLastLoopFinishDictionary.TryGetValue(chip, out TimeResult? lastFin) && lastFin.Occurrence < (hour * 2))
                                        {
                                            chipSecondsDiff += lastFin.ChipSeconds;
                                            chipMillisecondsDiff += lastFin.ChipMilliseconds;
                                        }
                                        if (chipMillisecondsDiff >= 1000)
                                        {
                                            chipSecondsDiff++;
                                            chipMillisecondsDiff -= 1000;
                                        }
                                        TimeResult newResult = new(theEvent.Identifier,
                                            read.ReadId,
                                            Constants.Timing.TIMERESULT_DUMMYPERSON,
                                            read.LocationId,
                                            Constants.Timing.SEGMENT_NONE,
                                            occurrence,
                                            secondsDiff,
                                            millisecondsDiff,
                                            TimeResult.ChipToIdentifier(chip),
                                            chipSecondsDiff,
                                            chipMillisecondsDiff,
                                            read.Time,
                                            read.ChipBib == Constants.Timing.CHIPREAD_DUMMYBIB ? read.ReadBib : read.ChipBib,
                                            Constants.Timing.TIMERESULT_STATUS_NONE,
                                            ""
                                        );
                                        newResults.Add(newResult);
                                    }
                                    read.Status = Constants.Timing.CHIPREAD_STATUS_USED;
                                }
                            }
                            // Possible reads at this point:
                            //      Start location reads not within a start window...
                            else
                            {
                                read.Status = Constants.Timing.CHIPREAD_STATUS_UNUSEDSTART;
                            }
                        }
                        // Otherwise just ignore the reads.
                        else
                        {
                            read.Status = Constants.Timing.CHIPREAD_STATUS_OVERMAX;
                        }
                    }
                }
            }
            // Process the intersection of unknown DNF people and Finish results:
            foreach (string chip in chipDnfDictionary.Keys)
            {
                ChipRead read = chipDnfDictionary[chip];
                long startSeconds = dictionary.DistanceStartDict[0].Seconds;
                int startMilliseconds = dictionary.DistanceStartDict[0].Milliseconds;
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
                // Calculate the hour
                int hour = (int)(secondsDiff / interval);
                if (backyardResultDictionary.TryGetValue((hour, TimeResult.ChipToIdentifier(chip)), out (TimeResult? start, TimeResult? end) oByRes))
                {
                    TimeResult finish = oByRes.end!;
                    newResults.Remove(finish);
                    finish.ReadId = read.ReadId;
                    finish.Time = "DNF";
                    finish.ChipTime = "DNF";
                    finish.Status = Constants.Timing.TIMERESULT_STATUS_DNF;
                    newResults.Add(finish);
                }
                else
                {
                    newResults.Add(new TimeResult(theEvent.Identifier,
                        read.ReadId,
                        Constants.Timing.TIMERESULT_DUMMYPERSON,
                        Constants.Timing.LOCATION_FINISH,
                        Constants.Timing.SEGMENT_FINISH,
                        (hour * 2) + 1,
                        0,
                        0,
                        TimeResult.ChipToIdentifier(chip),
                        0,
                        0,
                        chipDnfDictionary[chip].Time,
                        chipDnfDictionary[chip].ChipBib == Constants.Timing.CHIPREAD_DUMMYBIB ? chipDnfDictionary[chip].ReadBib : chipDnfDictionary[chip].ChipBib,
                        Constants.Timing.TIMERESULT_STATUS_DNF,
                        ""
                        ));
                }
            }
            // Process the intersection of known DNF people and finish results.
            foreach (string bib in bibDnfDictionary.Keys)
            {
                Participant? part = dictionary.ParticipantBibDictionary.GetValueOrDefault(bib);
                if (part != null)
                {
                    part.Status = Constants.Timing.EVENTSPECIFIC_DNF;
                    updateParticipants.Add(part);
                }
                ChipRead read = bibDnfDictionary[bib];
                long startSeconds = dictionary.DistanceStartDict[0].Seconds;
                int startMilliseconds = dictionary.DistanceStartDict[0].Milliseconds;
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
                // Calculate the hour
                int hour = (int)(secondsDiff / interval);
                if (backyardResultDictionary.TryGetValue((hour, TimeResult.BibToIdentifier(bib)), out (TimeResult? start, TimeResult? end) oByRes))
                {
                    TimeResult finish = oByRes.end!;
                    newResults.Remove(finish);
                    finish.ReadId = read.ReadId;
                    finish.Time = "DNF";
                    finish.ChipTime = "DNF";
                    finish.Status = Constants.Timing.TIMERESULT_STATUS_DNF;
                    newResults.Add(finish);
                }
                else
                {
                    newResults.Add(new TimeResult(theEvent.Identifier,
                        read.ReadId,
                        Constants.Timing.TIMERESULT_DUMMYPERSON,
                        Constants.Timing.LOCATION_FINISH,
                        Constants.Timing.SEGMENT_FINISH,
                        (hour * 2) + 1,
                        0,
                        0,
                        TimeResult.BibToIdentifier(bib),
                        0,
                        0,
                        bibDnfDictionary[bib].Time,
                        bibDnfDictionary[bib].ChipBib == Constants.Timing.CHIPREAD_DUMMYBIB ? bibDnfDictionary[bib].ReadBib : bibDnfDictionary[bib].ChipBib,
                        Constants.Timing.TIMERESULT_STATUS_DNF,
                        ""
                        ));
                }
            }
            // Process the intersection of unknown DNS people and Finish results:
            foreach (string chip in chipDnsDictionary.Keys)
            {
                if (finishTimes.TryGetValue(TimeResult.ChipToIdentifier(chip), out List<TimeResult>? finResults))
                {
                    foreach (TimeResult finish in finResults)
                    {
                        finish.ReadId = chipDnsDictionary[chip].ReadId;
                        finish.Time = "DNS";
                        finish.ChipTime = "DNS";
                        finish.Status = Constants.Timing.TIMERESULT_STATUS_DNS;
                        finish.Occurrence = 0;
                        newResults.Add(finish);
                    }
                }
                else
                {
                    ChipRead tmpDns = chipDnsDictionary[chip];
                    newResults.Add(new TimeResult(theEvent.Identifier,
                        tmpDns.ReadId,
                        Constants.Timing.TIMERESULT_DUMMYPERSON,
                        Constants.Timing.LOCATION_FINISH,
                        Constants.Timing.SEGMENT_FINISH,
                        0,
                        0,
                        0,
                        TimeResult.ChipToIdentifier(chip),
                        0,
                        0,
                        tmpDns.Time,
                        tmpDns.ChipBib == Constants.Timing.CHIPREAD_DUMMYBIB ? tmpDns.ReadBib : tmpDns.ChipBib,
                        Constants.Timing.TIMERESULT_STATUS_DNS,
                        ""
                        ));
                }
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
                if (finishTimes.TryGetValue(TimeResult.BibToIdentifier(bib), out List<TimeResult>? finResults))
                {
                    foreach (TimeResult finish in finResults)
                    {
                        finish.ReadId = bibDnsDictionary[bib].ReadId;
                        finish.Time = "DNS";
                        finish.ChipTime = "DNS";
                        finish.Status = Constants.Timing.TIMERESULT_STATUS_DNS;
                        finish.Occurrence = 0;
                        newResults.Add(finish);
                    }
                }
                else
                {
                    ChipRead tmpDns = bibDnsDictionary[bib];
                    newResults.Add(new TimeResult(theEvent.Identifier,
                        tmpDns.ReadId,
                        part == null ? Constants.Timing.TIMERESULT_DUMMYPERSON : part.EventSpecific.Identifier,
                        Constants.Timing.LOCATION_FINISH,
                        Constants.Timing.SEGMENT_FINISH,
                        0,
                        0,
                        0,
                        TimeResult.BibToIdentifier(bib),
                        0,
                        0,
                        tmpDns.Time,
                        bib,
                        Constants.Timing.TIMERESULT_STATUS_DNS,
                        part == null ? "" : part.EventSpecific.Division
                        ));
                }
            }
            // Go through and process every result.
            // Separate results by identifier
            Dictionary<string, List<TimeResult>> resultDictionary = [];
            foreach (TimeResult res in newResults)
            {
                if (!resultDictionary.TryGetValue(res.Identifier, out List<TimeResult>? resultsList))
                {
                    resultsList = [];
                    resultDictionary[res.Identifier] = resultsList;
                }
                if (res.LocationId == Constants.Timing.LOCATION_FINISH)
                {
                    resultsList.Add(res);
                }
            }
            // process reads that need to be set to ignore
            foreach (ChipRead read in setUnknown)
            {
                read.Status = Constants.Timing.CHIPREAD_STATUS_UNKNOWN;
            }
            // Update database with information.
            foreach (TimeResult tRem in toRemove)
            {
                database.RemoveTimingResult(tRem);
            }
            database.AddTimingResults(newResults);
            database.SetChipReadStatuses(allChipReads);
            database.UpdateParticipants([.. updateParticipants]);
            return newResults;
        }


        public static List<TimeResult> ProcessPlacements(Event theEvent, IdbInterface database, TimingDictionary dictionary)
        {
            // Get results to process.
            List<TimeResult> output = database.GetTimingResults(theEvent.Identifier);
            Dictionary<string, TimeResult> lastResult = [];
            // Create a dictionary so we can check if placements have changed. (place, location, occurrence, distance)
            Dictionary<(int, int, int, string), TimeResult> placementDictionary = [];
            foreach (TimeResult result in output.Where(result => result.Place > 0))
            {
                placementDictionary[(result.Place, result.LocationId, result.Occurrence, result.DistanceName)] = result;
            }
            // This should sort so lower occurrences are first.
            output.Sort(TimeResult.CompareByOccurrence);
            foreach (TimeResult res in output.Where(res => Constants.Timing.SEGMENT_FINISH == res.SegmentId && Constants.Timing.TIMERESULT_STATUS_DNF != res.Status))
            {
                lastResult[res.Identifier] = res;
            }
            List<TimeResult> lastResultList = [.. lastResult.Values];
            // RankedBy.Clock is assumed to be rank by elapsed time
            // !RankedBy.Clock is rank by cumulative
            if (theEvent.RankedBy != RankingType.Clock)
            {
                lastResultList.Sort(TimeResult.CompareForBackyardCumulative);
            }
            else
            {
                lastResultList.Sort(TimeResult.CompareForBackyardElapsed);
            }
            // Get Dictionaries for storing the last known place (age group, gender)
            // The key is as follows: Division
            Dictionary<string, int> divisionPlaceDictionary = [];
            // The key is as follows: (Age Group ID, Gender)
            Dictionary<(int, string), int> ageGroupPlaceDictionary = [];
            // The key is as follows: Gender
            Dictionary<string, int> genderPlaceDictionary = [];
            int place = 0;
            // Use the sorted list of results to calculate placements
            foreach (TimeResult result in lastResultList)
            {
                if (!dictionary.ParticipantEventSpecificDictionary.TryGetValue(result.EventSpecificId,
                        out Participant? person)) continue;
                string gender = person.Gender.ToLower();
                if (gender.Length < 1)
                {
                    gender = "not specified";
                }
                result.Place = ++place;
                int genderPl = genderPlaceDictionary.GetValueOrDefault(gender, 0);
                result.GenderPlace = ++genderPl;
                genderPlaceDictionary[gender] = genderPl;
                int ageGroupId = person.EventSpecific.AgeGroupId;
                if (ageGroupId != Constants.Timing.TIMERESULT_DUMMYAGEGROUP)
                {
                    int agePl = ageGroupPlaceDictionary.GetValueOrDefault((ageGroupId, gender), 0);
                    result.AgePlace = ++agePl;
                    ageGroupPlaceDictionary[(ageGroupId, gender)] = agePl;
                }
                string division = person.EventSpecific.Division.ToLower();
                if (division.Length <= 0) continue;
                int divPl = divisionPlaceDictionary.GetValueOrDefault(division, 0);
                result.DivisionPlace = ++divPl;
                divisionPlaceDictionary[division] = divPl;
            }
            // Update every result we're outputting with calculated places.
            foreach (TimeResult result in output)
            {
                if (lastResult.TryGetValue(result.Identifier, out TimeResult? placeResult))
                {
                    result.Place = placeResult.Place;
                    result.GenderPlace = placeResult.GenderPlace;
                    result.AgePlace = placeResult.AgePlace;
                }
                // Change any TIMERESULT_STATUS_NONE to TIMERESULT_STATUS_PROCESSED
                if (Constants.Timing.TIMERESULT_STATUS_NONE == result.Status)
                {
                    result.Status = Constants.Timing.TIMERESULT_STATUS_PROCESSED;
                }
            }
            // Check if we should be re-uploading results because placements have changed.
            List<TimeResult> reUpload = [];
            Log.D("Timing.Routines.DistanceRoutine", "Checking for outdated placements.");
            foreach (TimeResult result in output)
            {
                if (!placementDictionary.TryGetValue(
                        (result.Place, result.LocationId, result.Occurrence, result.DistanceName),
                        out TimeResult? plRes) || plRes.Bib == result.Bib) continue;
                Log.D("Timing.Routines.DistanceRoutine", $"Outdated placement found. {result.ParticipantName} && {plRes.ParticipantName}");
                result.Uploaded = Constants.Timing.TIMERESULT_UPLOADED_FALSE;
                plRes.Uploaded = Constants.Timing.TIMERESULT_UPLOADED_FALSE;
                reUpload.Add(result);
                reUpload.Add(plRes);
            }
            database.AddTimingResults(output);
            database.SetUploadedTimingResults(reUpload);
            return output;
        }
    }
}

