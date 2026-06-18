using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using System;
using System.Collections.Generic;

namespace Chronokeep.Timing.Routines
{
    internal static class TimeRoutine
    {
        public static List<TimeResult> ProcessRace(Event theEvent, IdbInterface database, TimingDictionary dictionary, IMainWindow window)
        {
            Log.D("Timing.TimingWorker", "Processing chip reads for a time based event.");
            // Check if there's anything to process.
            // Get start TimeResults
            Dictionary<string, TimeResult> startTimes = [];
            foreach (TimeResult result in database.GetStartTimes(theEvent.Identifier))
            {
                startTimes[result.Identifier] = result;
            }
            // Dictionary of TimeResults for a specific identifier
            Dictionary<string, List<TimeResult>> finishTimes = [];
            foreach (TimeResult result in database.GetFinishTimes(theEvent.Identifier))
            {
                if (!finishTimes.TryGetValue(result.Identifier, out List<TimeResult>? finResults))
                {
                    finResults = [];
                    finishTimes[result.Identifier] = finResults;
                }
                finResults.Add(result);
            }
            // Get all the Chip Reads we find useful (Unprocessed, and those used
            // as results.) and sort them into groups based upon Bib, Chip, or put them
            // in the ignore pile if no chip/bib found.
            Dictionary<string, List<ChipRead>> bibReadPairs = [];
            Dictionary<string, List<ChipRead>> chipReadPairs = [];
            // Make sure we keep track of the last occurrence for a person at a location.
            // (Bib, Location), Last Chip Read
            Dictionary<(string, int), (ChipRead Read, int Occurrence)> bibLastReadDictionary = [];
            Dictionary<string, ChipRead> bibStartReadDictionary = [];
            Dictionary<(string, int), (ChipRead Read, int Occurrence)> chipLastReadDictionary = [];
            Dictionary<string, ChipRead> chipStartReadDictionary = [];
            // Keep a list of DNS participants so we can mark them as DNS in results.
            // Keep a record of the DNS chip read so we can link it with the TimeResult
            Dictionary<string, ChipRead> bibDnsDictionary = [];
            Dictionary<string, ChipRead> chipDnsDictionary = [];


            List<ChipRead> allChipReads = database.GetUsefulChipReads(theEvent.Identifier);
            allChipReads.Sort();
            List<ChipRead> setUnknown = [];

            // Get some variables to check if we need to sound an alarm.
            // Get a time value to check to ensure the chip read isn't too far in the past.
            DateTime before = DateTime.Now.AddMinutes(-5);
            (Dictionary<string, Alarm> bibAlarms, Dictionary<string, Alarm> chipAlarms) = Alarm.GetAlarmDictionaries();

            foreach (ChipRead read in allChipReads)
            {
                // Check to set off an alarm.
                if (read.Time > before)
                {
                    // Bib set on the read, alarm exists, and it has not went off.
                    if (read.Bib != Constants.Timing.CHIPREAD_DUMMYBIB
                        && bibAlarms.TryGetValue(read.Bib, out Alarm? bibAlarm)
                        && bibAlarm.Enabled)
                    {
                        window.NotifyAlarm(read.Bib, "");
                    }
                    // Bib not set, chip is set, alarm exists, and it has not went off.
                    else if (read.ChipNumber != Constants.Timing.CHIPREAD_DUMMYCHIP
                        && chipAlarms.TryGetValue(read.ChipNumber, out Alarm? chipAlarm)
                        && chipAlarm.Enabled)
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
                        // if we process all the used reads before putting them in the list we can
                        // ensure that all the reads we process are STATUS_NONE, and then we can
                        // verify that we aren't inserting results BEFORE results we've already calculated.
                        case Constants.Timing.CHIPREAD_STATUS_USED:
                        {
                            if (!bibLastReadDictionary.TryGetValue((read.Bib, read.LocationId), out (ChipRead Read, int Occurrence) bLastReads))
                            {
                                bLastReads = (read, 0);
                            }
                            bibLastReadDictionary[(read.Bib, read.LocationId)] = (read, bLastReads.Occurrence + 1);
                            break;
                        }
                        case Constants.Timing.CHIPREAD_STATUS_STARTTIME when (
                            Constants.Timing.LOCATION_START == read.LocationId ||
                            (Constants.Timing.LOCATION_FINISH == read.LocationId && theEvent.CommonStartFinish)):
                            bibStartReadDictionary[read.Bib] = read;
                            break;
                        case Constants.Timing.CHIPREAD_STATUS_NONE:
                        case Constants.Timing.CHIPREAD_STATUS_DNF:
                        {
                            if (!bibReadPairs.TryGetValue(read.Bib, out List<ChipRead>? bReads))
                            {
                                bReads = [];
                                bibReadPairs[read.Bib] = bReads;
                            }

                            bReads.Add(read);
                            break;
                        }
                    }
                }
                else if (Constants.Timing.CHIPREAD_DUMMYCHIP != read.ChipNumber)
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
                        case Constants.Timing.CHIPREAD_STATUS_STARTTIME
                            when (Constants.Timing.LOCATION_START == read.LocationId ||
                                  (Constants.Timing.LOCATION_FINISH == read.LocationId && theEvent.CommonStartFinish)):
                            chipStartReadDictionary[read.ChipNumber] = read;
                            break;
                        case Constants.Timing.CHIPREAD_STATUS_NONE:
                        case Constants.Timing.CHIPREAD_STATUS_DNF:
                        {
                            if (!chipReadPairs.TryGetValue(read.ChipNumber, out List<ChipRead>? cReads))
                            {
                                cReads = [];
                            }
                            cReads.Add(read);
                            chipReadPairs[read.ChipNumber] = cReads;
                            break;
                        }
                    }
                }
                else
                {
                    setUnknown.Add(read);
                }
            }
            // Go through all the chip reads we've marked and create new results.
            List<TimeResult> newResults = [];
            List<Participant> updateParticipants = [];
            // start with bibs
            foreach (string bib in bibReadPairs.Keys)
            {
                Participant? part = dictionary.ParticipantBibDictionary.GetValueOrDefault(bib);
                if (part != null)
                {
                    part.Status = Constants.Timing.EVENTSPECIFIC_STARTED;
                    updateParticipants.Add(part);
                }
                Distance? d = part != null ?
                    dictionary.DistanceDictionary[part.EventSpecific.DistanceIdentifier] :
                    null;
                long startSeconds, endSeconds;
                int startMilliseconds;
                TimeResult? startResult = null;
                if (d == null || !dictionary.DistanceStartDict.TryGetValue(d.Identifier, out (long Seconds, int Milliseconds) oStart) || !dictionary.DistanceEndDict.TryGetValue(d.Identifier, out (long Seconds, int Milliseconds) oEnd))
                {
                    (startSeconds, startMilliseconds) = dictionary.DistanceStartDict[0];
                    endSeconds = dictionary.DistanceEndDict[0].Seconds;
                }
                else
                {
                    (startSeconds, startMilliseconds) = oStart;
                    endSeconds = oEnd.Seconds;
                }
                long maxStartSeconds = startSeconds + theEvent.StartWindow;
                bool finished = false;
                foreach (ChipRead read in bibReadPairs[bib])
                {
                    // pre-start
                    if (read.TimeSeconds < startSeconds || (read.TimeSeconds == startSeconds && read.TimeMilliseconds < startMilliseconds))
                    {
                        read.Status = Constants.Timing.CHIPREAD_STATUS_PRESTART;
                    }
                    else if (read.TimeSeconds > endSeconds || (read.TimeSeconds == endSeconds && read.TimeMilliseconds > startMilliseconds))
                    {
                        read.Status = Constants.Timing.CHIPREAD_STATUS_OVERMAX;
                    }
                    else
                    {
                        // check if we're in the starting window and at the start line
                        if ((read.TimeSeconds < maxStartSeconds || (read.TimeSeconds == maxStartSeconds && read.TimeMilliseconds <= startMilliseconds)) &&
                            (Constants.Timing.LOCATION_START == read.LocationId ||
                            (Constants.Timing.LOCATION_FINISH == read.LocationId
                            && theEvent.CommonStartFinish)))
                        {
                            // check if we've stored a chip read as the start chip read, update it to unused if so
                            if (bibStartReadDictionary.TryGetValue(bib, out ChipRead? oStartRead))
                            {
                                oStartRead.Status = Constants.Timing.CHIPREAD_STATUS_UNUSEDSTART;
                            }
                            // Update the last read we've seen at this location
                            bibStartReadDictionary[bib] = read;
                            if (startResult != null)
                            {
                                newResults.Remove(startResult);
                            }
                            // Create a result for the start time.
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
                            read.Status = Constants.Timing.CHIPREAD_STATUS_STARTTIME;
                        }
                        // Possible reads at this point:
                        //      Start Location reads past the StartWindow (IGNORE)
                        //      Start/Finish Location reads past the StartWindow (valid reads)
                        //          These could be BEFORE or AFTER the last occurrence at this spot
                        //      Reads at any other location
                        else if (Constants.Timing.LOCATION_START != read.LocationId)
                        {
                            int occurrence = 1;
                            int occursWithin = 0;
                            if (Constants.Timing.LOCATION_FINISH == read.LocationId)
                            {
                                occursWithin = theEvent.FinishIgnoreWithin;
                            }
                            else if (dictionary.LocationDictionary.TryGetValue(read.LocationId, out TimingLocation? oLoc))
                            {
                                occursWithin = oLoc.IgnoreWithin;
                            }
                            // Minimum time to create a result.
                            long minSeconds = startSeconds;
                            int minMilliseconds = startMilliseconds;
                            if (bibLastReadDictionary.TryGetValue((bib, read.LocationId), out (ChipRead Read, int Occurrence) bLastReads))
                            {
                                occurrence = bLastReads.Occurrence + 1;
                                minSeconds = bLastReads.Read.TimeSeconds + occursWithin;
                                minMilliseconds = bLastReads.Read.TimeMilliseconds;
                            }
                            // Check if this is a 'dnf' read.  If it is we set the flag to the previous finish time and
                            // IGNORE ANY SUBSEQUENT READS. PERIOD.
                            if (Constants.Timing.CHIPREAD_STATUS_DNF == read.Status)
                            {
                                finished = true;
                            }
                            // Check if we've marked the person as finished;
                            if (finished)
                            {
                                read.Status = Constants.Timing.CHIPREAD_STATUS_DNF == read.Status ? read.Status : Constants.Timing.CHIPREAD_STATUS_OVERMAX;
                            }
                            // Check if we're in the ignore within period.
                            else if (read.TimeSeconds < minSeconds || (read.TimeSeconds == minSeconds && read.TimeMilliseconds < minMilliseconds))
                            {
                                read.Status = Constants.Timing.CHIPREAD_STATUS_WITHINIGN;
                            }
                            else
                            {
                                bibLastReadDictionary[(bib, read.LocationId)] = (read, occurrence);
                                int segId = Constants.Timing.SEGMENT_NONE;
                                // Check for linked distance and set distanceId to the linked distance, or to the actual distance id.
                                // Segments are based on the linked distance.
                                int distanceId = d == null ? 0 : d.LinkedDistance > 0 ? d.LinkedDistance : d.Identifier;
                                // Check for Distance specific segments (Occurrence is always 1 for time based)
                                if (!theEvent.DistanceSpecificSegments && dictionary.SegmentDictionary.TryGetValue((Constants.Timing.COMMON_SEGMENTS_DISTANCEID, read.LocationId, 1), out Segment? oSeg))
                                {
                                    segId = oSeg.Identifier;
                                }
                                // Distance specific segments
                                else if (d != null && dictionary.SegmentDictionary.TryGetValue((distanceId, read.LocationId, 1), out Segment? tSeg))
                                {
                                    segId = tSeg.Identifier;
                                }
                                else if (Constants.Timing.LOCATION_FINISH == read.LocationId)
                                {
                                    segId = Constants.Timing.SEGMENT_FINISH;
                                }
                                string identifier = TimeResult.BibToIdentifier(bib);
                                // Create a result for the start value
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
                                bool startExists = startTimes.TryGetValue(identifier, out TimeResult? startRes);
                                long chipSecondsDiff = read.TimeSeconds - (startExists ? Constants.Timing.RfidDateToEpoch(startRes!.SystemTime) : startSeconds);
                                int chipMillisecondsDiff = read.TimeMilliseconds - (startExists ? startRes!.SystemTime.Millisecond : startMilliseconds);
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
                                newResults.Add(new(theEvent.Identifier,
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
                                    ));
                                read.Status = Constants.Timing.CHIPREAD_STATUS_USED;
                            }
                        }
                        // Possible reads at this point:
                        //      Start Location reads past the StartWindow (set status to ignore)
                        else
                        {
                            read.Status = Constants.Timing.CHIPREAD_STATUS_UNUSEDSTART;
                        }
                    }
                }
            }
            foreach (string chip in chipReadPairs.Keys)
            {
                (long startSeconds, int startMilliseconds) = dictionary.DistanceStartDict[0];
                long endSeconds = dictionary.DistanceEndDict[0].Seconds;
                long maxStartSeconds = startSeconds + theEvent.StartWindow;
                TimeResult? startResult = null;
                // keep a boolean so we can notify ourselves if we've marked a person as finished
                bool finished = false;
                foreach (ChipRead read in chipReadPairs[chip])
                {
                    // pre-start
                    if (read.TimeSeconds < startSeconds || (read.TimeSeconds == startSeconds && read.TimeMilliseconds < startMilliseconds))
                    {
                        read.Status = Constants.Timing.CHIPREAD_STATUS_PRESTART;
                    }
                    else if (read.TimeSeconds > endSeconds || (read.TimeSeconds == endSeconds && read.TimeMilliseconds > startMilliseconds))
                    {
                        read.Status = Constants.Timing.CHIPREAD_STATUS_OVERMAX;
                    }
                    else
                    {
                        // check if we're in the starting window and at the start line
                        if ((read.TimeSeconds < maxStartSeconds || (read.TimeSeconds == maxStartSeconds && read.TimeMilliseconds <= startMilliseconds)) &&
                            (Constants.Timing.LOCATION_START == read.LocationId ||
                            (Constants.Timing.LOCATION_FINISH == read.LocationId
                            && theEvent.CommonStartFinish)))
                        {
                            // check if we've stored a chip read as the start chip read, update it to unused if so
                            if (chipStartReadDictionary.TryGetValue(chip, out ChipRead? oStartRead))
                            {
                                oStartRead.Status = Constants.Timing.CHIPREAD_STATUS_UNUSEDSTART;
                            }
                            // Update the last read we've seen at this location
                            chipStartReadDictionary[chip] = read;
                            if (startResult != null)
                            {
                                newResults.Remove(startResult);
                            }
                            // Create a result for the start time.
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
                                TimeResult.ChipToIdentifier(chip),
                                0,
                                0,
                                read.Time,
                                read.ChipBib == Constants.Timing.CHIPREAD_DUMMYBIB ? read.ReadBib : read.ChipBib,
                                Constants.Timing.TIMERESULT_STATUS_NONE,
                                ""
                                );
                            startTimes[startResult.Identifier] = startResult;
                            newResults.Add(startResult);
                            read.Status = Constants.Timing.CHIPREAD_STATUS_STARTTIME;
                        }
                        // Possible reads at this point:
                        //      Start Location reads past the StartWindow (IGNORE)
                        //      Start/Finish Location reads past the StartWindow (valid reads)
                        //          These could be BEFORE or AFTER the last occurrence at this spot
                        //      Reads at any other location
                        else if (Constants.Timing.LOCATION_START != read.LocationId)
                        {
                            int occurrence = 1;
                            int occursWithin = 0;
                            if (Constants.Timing.LOCATION_FINISH == read.LocationId)
                            {
                                occursWithin = theEvent.FinishIgnoreWithin;
                            }
                            else if (dictionary.LocationDictionary.TryGetValue(read.LocationId, out TimingLocation? oLoc))
                            {
                                occursWithin = oLoc.IgnoreWithin;
                            }
                            // Minimum time to create a result.
                            long minSeconds = startSeconds;
                            int minMilliseconds = startMilliseconds;
                            if (chipLastReadDictionary.TryGetValue((chip, read.LocationId), out (ChipRead Read, int Occurrence) cLastReads))
                            {
                                occurrence = cLastReads.Occurrence + 1;
                                minSeconds = cLastReads.Read.TimeSeconds + occursWithin;
                                minMilliseconds = cLastReads.Read.TimeMilliseconds;
                            }
                            // Check if this is a 'dnf' read.  If it is we set the flag to the previous finish time and
                            // IGNORE ANY SUBSEQUENT READS. PERIOD.
                            if (Constants.Timing.CHIPREAD_STATUS_DNF == read.Status)
                            {
                                finished = true;
                            }
                            // Check if we've marked the person as finished;
                            if (finished)
                            {
                                read.Status = Constants.Timing.CHIPREAD_STATUS_DNF == read.Status ? read.Status : Constants.Timing.CHIPREAD_STATUS_OVERMAX;
                            }
                            // Check if we're in the ignore within period.
                            else if (read.TimeSeconds < minSeconds || (read.TimeSeconds == minSeconds && read.TimeMilliseconds < minMilliseconds))
                            {
                                read.Status = Constants.Timing.CHIPREAD_STATUS_WITHINIGN;
                            }
                            else
                            {
                                chipLastReadDictionary[(chip, read.LocationId)] = (read, occurrence);
                                int segId = Constants.Timing.SEGMENT_NONE;
                                // Check for Distance specific segments (Occurrence is always 1 for time based)
                                if (!theEvent.DistanceSpecificSegments && dictionary.SegmentDictionary.TryGetValue((Constants.Timing.COMMON_SEGMENTS_DISTANCEID, read.LocationId, 1), out Segment? oSeg))
                                {
                                    segId = oSeg.Identifier;
                                }
                                else if (Constants.Timing.LOCATION_FINISH == read.LocationId)
                                {
                                    segId = Constants.Timing.SEGMENT_FINISH;
                                }
                                string identifier = TimeResult.ChipToIdentifier(chip);
                                // Create a result for the start value
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
                                bool startExists = startTimes.TryGetValue(identifier, out TimeResult? startRes);
                                long chipSecDiff = read.TimeSeconds - (startExists ? Constants.Timing.RfidDateToEpoch(startRes!.SystemTime) : startSeconds);
                                int chipMillisecondsDiff = read.TimeMilliseconds - (startExists ? startRes!.SystemTime.Millisecond : startMilliseconds);
                                while (chipMillisecondsDiff < 0)
                                {
                                    chipSecDiff--;
                                    chipMillisecondsDiff += 1000;
                                }
                                while (chipMillisecondsDiff >= 1000)
                                {
                                    chipSecDiff++;
                                    chipMillisecondsDiff -= 1000;
                                }
                                newResults.Add(new(theEvent.Identifier,
                                    read.ReadId,
                                    Constants.Timing.TIMERESULT_DUMMYPERSON,
                                    read.LocationId,
                                    segId,
                                    occurrence,
                                    secondsDiff,
                                    millisecondsDiff,
                                    identifier,
                                    chipSecDiff,
                                    chipMillisecondsDiff,
                                    read.Time,
                                    read.ChipBib == Constants.Timing.CHIPREAD_DUMMYBIB ? read.ReadBib : read.ChipBib,
                                    Constants.Timing.TIMERESULT_STATUS_NONE,
                                    ""
                                    ));
                                read.Status = Constants.Timing.CHIPREAD_STATUS_USED;
                            }
                        }
                        // Possible reads at this point:
                        //      Start Location reads past the StartWindow (set status to ignore)
                        else
                        {
                            read.Status = Constants.Timing.CHIPREAD_STATUS_UNUSEDSTART;
                        }
                    }
                }
            }
            // Process the intersection of unknown DNS people and Finish results:
            foreach (string chip in chipDnsDictionary.Keys)
            {
                if (finishTimes.TryGetValue(TimeResult.ChipToIdentifier(chip), out List<TimeResult>? finResults))
                {
                    int dnsId = chipDnsDictionary[chip].ReadId;
                    foreach (TimeResult finish in finResults)
                    {
                        finish.ReadId = dnsId;
                        finish.Time = "DNS";
                        finish.ChipTime = "DNS";
                        finish.Status = Constants.Timing.TIMERESULT_STATUS_DNS;
                        finish.Occurrence = theEvent.FinishMaxOccurrences;
                        newResults.Add(finish);
                    }
                }
                else
                {
                    ChipRead chipDns = chipDnsDictionary[chip];
                    newResults.Add(new TimeResult(theEvent.Identifier,
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
                int occurrence = part == null ? 1 : dictionary.DistanceDictionary.TryGetValue(part.EventSpecific.DistanceIdentifier, out Distance? oDist) ? oDist.FinishOccurrence : 1;
                if (finishTimes.TryGetValue(TimeResult.BibToIdentifier(bib), out List<TimeResult>? finResults))
                {
                    int dnsId = bibDnsDictionary[bib].ReadId;
                    foreach (TimeResult finish in finResults)
                    {
                        finish.ReadId = dnsId;
                        finish.Time = "DNS";
                        finish.ChipTime = "DNS";
                        finish.Status = Constants.Timing.TIMERESULT_STATUS_DNS;
                        finish.Occurrence = occurrence;
                        newResults.Add(finish);
                    }
                }
                else
                {
                    ChipRead bibDns = bibDnsDictionary[bib];
                    newResults.Add(new TimeResult(theEvent.Identifier,
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
                        ));
                }
            }
            // process reads that need to be set to ignore
            foreach (ChipRead read in setUnknown)
            {
                read.Status = Constants.Timing.CHIPREAD_STATUS_UNKNOWN;
            }
            // Update database with information.
            database.AddTimingResults(newResults);
            database.SetChipReadStatuses(allChipReads);
            database.UpdateParticipants(updateParticipants);
            return newResults;
        }

        // Process lap times.
        public static void ProcessLapTimes(Event theEvent, IdbInterface database)
        {
            Dictionary<(string, int), TimeResult> raceResults = [];
            foreach (TimeResult startTime in database.GetSegmentTimes(theEvent.Identifier, Constants.Timing.SEGMENT_START))
            {
                raceResults[(startTime.Identifier, 0)] = startTime;
            }
            List<TimeResult> laps = database.GetSegmentTimes(theEvent.Identifier, Constants.Timing.SEGMENT_FINISH);
            laps.Sort((x1, x2) => x1.Identifier.Equals(x2.Identifier) ? x1.Occurrence.CompareTo(x2.Occurrence) : string.Compare(x1.Identifier, x2.Identifier, StringComparison.Ordinal));
            foreach (TimeResult currentLap in laps)
            {
                raceResults[(currentLap.Identifier, currentLap.Occurrence)] = currentLap;
                long sec = 0;
                int mill = 0;
                if (raceResults.TryGetValue((currentLap.Identifier, currentLap.Occurrence - 1), out TimeResult? prevRes))
                {
                    sec = prevRes.ChipSeconds;
                    mill = prevRes.ChipMilliseconds;
                }
                sec = currentLap.ChipSeconds - sec;
                mill = currentLap.ChipMilliseconds - mill;
                while (mill < 0)
                {
                    sec--;
                    mill += 1000;
                }
                while (mill >= 1000)
                {
                    sec++;
                    mill -= 1000;
                }
                currentLap.LapTime = Constants.Timing.ToTime((int)sec, mill);
            }
            database.AddTimingResults(laps);
        }

        // Process placements for a time based race.
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
                if (!segmentDictionary.TryGetValue(result.SegmentId, out List<TimeResult>? oSegResults))
                {
                    oSegResults = [];
                    segmentDictionary[result.SegmentId] = oSegResults;
                }
                oSegResults.Add(result);
            }
            // process results based upon the segment they're in
            foreach (Segment segment in segments)
            {
                Log.D("Timing.TimingWorker", $"Processing segment {segment.Name}");
                if (segmentDictionary.TryGetValue(segment.Identifier, out List<TimeResult>? tSegResults))
                {
                    output.AddRange(ProcessSegmentPlacements(theEvent, tSegResults, dictionary));
                }
            }
            Log.D("Timing.TimingWorker", "Processing finish results");
            if (segmentDictionary.TryGetValue(Constants.Timing.SEGMENT_FINISH, out List<TimeResult>? finSegResults))
            {
                output.AddRange(ProcessSegmentPlacements(theEvent, finSegResults, dictionary));
            }
            // Check if we should be re-uploading results because placements have changed.
            List<TimeResult> reUpload = [];
            Log.D("Timing.TimingWorker", "Checking for outdated placements.");
            foreach (TimeResult result in output)
            {
                if (!placementDictionary.TryGetValue(
                        (result.Place, result.LocationId, result.Occurrence, result.DistanceName),
                        out TimeResult? plResult) || plResult.Bib == result.Bib) continue;
                Log.D("Timing.TimingWorker", $"Outdated placement found. {result.ParticipantName} && {plResult.ParticipantName}");
                result.Uploaded = Constants.Timing.TIMERESULT_UPLOADED_FALSE;
                plResult.Uploaded = Constants.Timing.TIMERESULT_UPLOADED_FALSE;
                reUpload.Add(result);
                reUpload.Add(plResult);
            }
            database.AddTimingResults(output);
            database.SetUploadedTimingResults(reUpload);
            return output;
        }

        // Process segment placement times.
        private static List<TimeResult> ProcessSegmentPlacements(Event theEvent,
            List<TimeResult> segmentResults, TimingDictionary dictionary)
        {
            Dictionary<int, List<TimeResult>> personResults = [];
            Dictionary<int, TimeResult> personLastResult = [];
            foreach (TimeResult result in segmentResults)
            {
                // If we don't have a Top Result for the person, or the result we have
                // is lesser than the one we're looking at, set it to the best
                if (!personLastResult.TryGetValue(result.EventSpecificId, out TimeResult? pLastRes) || pLastRes.Occurrence < result.Occurrence)
                {
                    pLastRes = result;
                    personLastResult[result.EventSpecificId] = pLastRes;
                }
                // Store a person's results
                if (!personResults.TryGetValue(result.EventSpecificId, out List<TimeResult>? pResults))
                {
                    pResults = [];
                    personResults[result.EventSpecificId] = pResults;
                }
                pResults.Add(result);
                // Change any TIMERESULT_STATUS_NONE to TIMERESULT_STATUS_PROCESSED
                if (Constants.Timing.TIMERESULT_STATUS_NONE == result.Status)
                {
                    result.Status = Constants.Timing.TIMERESULT_STATUS_PROCESSED;
                }
            }
            // Get Dictionaries for storing the last known place (division, age group, gender, overall)
            // The key is as follows: (Distance ID, Division)
            Dictionary<(int, string), int> divisionPlaceDictionary = [];
            // The key is as follows: (Distance ID, Age Group ID, int - Gender)
            Dictionary<(int, int, string), int> ageGroupPlaceDictionary = [];
            // The key is as follows: (Distance ID, Gender)
            Dictionary<(int, string), int> genderPlaceDictionary = [];
            // The key is as follows: (Distance ID)
            Dictionary<int, int> placeDictionary = [];
            List<TimeResult> topResults = [.. personLastResult.Values];
            topResults.Sort((x1, x2) =>
            {
                int rank1 = 0, rank2 = 0;
                // Get *linked* distances. (Could be that specific distance)
                if (dictionary.LinkedDistanceDictionary.TryGetValue(x1.RealDistanceName, out (Distance, int) value1))
                {
                    (_, rank1) = value1;
                }
                if (dictionary.LinkedDistanceDictionary.TryGetValue(x2.RealDistanceName, out (Distance, int) value2))
                {
                    (_, rank2) = value2;
                }
                Log.D("Timing.Routines.TimeRoutine", $"rank 1 {rank1} - rank 2 {rank2}");
                if (rank1 == rank2)
                {
                    if (x1.Occurrence == x2.Occurrence)
                    {
                        if (theEvent.RankByGun)
                        {
                            if (x1.Seconds == x2.Seconds)
                            {
                                return x1.Milliseconds.CompareTo(x2.Milliseconds);
                            }
                            Log.D("Timing.Routines.TimeRoutine", "By Clock");
                            return x1.Seconds.CompareTo(x2.Seconds);
                        }
                        else
                        {
                            if (x1.ChipSeconds == x2.ChipSeconds)
                            {
                                return x1.ChipMilliseconds.CompareTo(x2.ChipMilliseconds);
                            }
                            Log.D("Timing.Routines.TimeRoutine", "By Chip");
                            return x1.ChipSeconds.CompareTo(x2.ChipSeconds);
                        }
                    }
                    Log.D("Timing.Routines.TimeRoutine", "By Occurrence");
                    return x2.Occurrence.CompareTo(x1.Occurrence);
                }
                Log.D("Timing.Routines.TimeRoutine", "By Rank");
                return rank1.CompareTo(rank2);
            });
            foreach (TimeResult result in topResults)
            {
                // Make sure we know who we're looking at. Can't rank otherwise.
                if (!dictionary.ParticipantEventSpecificDictionary.TryGetValue(result.EventSpecificId,
                        out Participant? person)) continue;
                // Use a linked distance ID for ranking instead of a specific distance id.
                if (!dictionary.LinkedDistanceIdentifierDictionary.TryGetValue(person.EventSpecific.DistanceIdentifier, out int distanceId))
                {
                    distanceId = person.EventSpecific.DistanceIdentifier;
                }
                // Since Results were sorted before we started, let's assume that the first item
                // is the fastest/best and if we can't find the key, add one starting at 0
                int pl = placeDictionary.GetValueOrDefault(distanceId, 0);
                result.Place = ++pl;
                placeDictionary[distanceId] = pl;
                string gender = person.Gender.ToLower();
                if (gender.Length < 1)
                {
                    gender = "not specified";
                }
                int ageGroupId = person.EventSpecific.AgeGroupId;
                int genderPl = genderPlaceDictionary.GetValueOrDefault((distanceId, gender), 0);
                result.GenderPlace = ++genderPl;
                genderPlaceDictionary[(distanceId, gender)] = genderPl;
                if (ageGroupId != Constants.Timing.TIMERESULT_DUMMYAGEGROUP)
                {
                    int agPl = ageGroupPlaceDictionary.GetValueOrDefault((distanceId, ageGroupId, gender), 0);
                    result.AgePlace = ++agPl;
                    ageGroupPlaceDictionary[(distanceId, ageGroupId, gender)] = agPl;
                }
                string division = person.EventSpecific.Division.ToLower();
                if (division.Length > 0)
                {
                    int divPl = divisionPlaceDictionary.GetValueOrDefault((distanceId, division), 0);
                    result.DivisionPlace = ++divPl;
                    divisionPlaceDictionary[(distanceId, division)] = divPl;
                }
                foreach (TimeResult otherResult in personResults[result.EventSpecificId])
                {
                    otherResult.Place = result.Place;
                    otherResult.GenderPlace = result.GenderPlace;
                    otherResult.AgePlace = result.AgePlace;
                    otherResult.DivisionPlace = result.DivisionPlace;
                }
            }
            return segmentResults;
        }
    }
}
