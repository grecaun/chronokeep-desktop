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
using Chronokeep.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Chronokeep.Timing.Announcer
{
    public class AnnouncerWorker
    {
        private readonly IdbInterface database;
        private static AnnouncerWorker? announcer;

        private static readonly Semaphore Semaphore = new(0, 2);
        private static readonly Lock AnLock = new();

        private static bool quittingTime = true;
        private static readonly List<AnnouncerParticipant> Participants = [];
        private static long version;
        private static readonly Dictionary<string, DateTime> BibSeen = [];

        private AnnouncerWorker(IdbInterface database)
        {
            this.database = database;
        }

        public static AnnouncerWorker NewAnnouncer(IdbInterface database)
        {
            announcer ??= new AnnouncerWorker(database);
            quittingTime = false;
            return announcer;
        }

        public static void Shutdown()
        {
            if (!AnLock.TryEnter(3000)) return;
            try
            {
                quittingTime = true;
            }
            finally
            {
                AnLock.Exit();
            }
        }

        public static List<AnnouncerParticipant> GetList()
        {
            List<AnnouncerParticipant> output = [];
            if (AnLock.TryEnter(3000))
            {
                try
                {
                    output.AddRange(Participants);
                }
                finally
                {
                    AnLock.Exit();
                }
            }
            Log.D("Timing.Announcer.AnnouncerWorker", $"Returning {output.Count} participants to announce.");
            return output;
        }

        /// <summary>Goes up every time the list changes, so readers can tell when to build anything from it again.</summary>
        public static long Version
        {
            get
            {
                lock (AnLock)
                {
                    return version;
                }
            }
        }

        public static bool Running()
        {
            bool output = false;
            if (!AnLock.TryEnter(3000)) return output;
            try
            {
                output = !quittingTime;
            }
            finally
            {
                AnLock.Exit();
            }
            return output;
        }

        public static void Notify()
        {
            try
            {
                Semaphore.Release();
            }
            catch
            {
                Log.D("Timing.Announcer.AnnouncerWorker", "Unable to release, release is most likely full.");
            }
        }

        private bool ProcessReads(List<ChipRead> announcerReads, Dictionary<string, Participant> participantBibDictionary)
        {
            Log.D("Timing.Announcer.AnnouncerWorker", "Processing chip reads.");
            List<AnnouncerParticipant> found = [];
            DateTime timeRightNow = DateTime.Now;
            foreach (ChipRead read in announcerReads)
            {
                // Check to ensure we know the bib of this person. Bibs nobody is registered with are kept too,
                // the announcer window decides whether to show them.
                // Only work if we've not seen the bib before, or not in SeenWindow minutes (SeenWindow of 0 means only show once).
                if (read.Bib != Constants.Timing.CHIPREAD_DUMMYBIB
                    && (!BibSeen.TryGetValue(read.Bib, out DateTime lastSeen)
                        || (Globals.SeenWindow != 0 && lastSeen.AddMinutes(Globals.SeenWindow).CompareTo(timeRightNow) < 0)))
                {
                    // Set rather than Add: a runner seen again after the seen window is already in the dictionary.
                    BibSeen[read.Bib] = timeRightNow;
                    found.Add(new AnnouncerParticipant(read.Bib, participantBibDictionary.GetValueOrDefault(read.Bib), read.Seconds));
                    // Mark this chipread as USED
                    read.Status = Constants.Timing.CHIPREAD_STATUS_ANNOUNCER_USED;
                }
                // Don't clobber over ANNOUNCER_USED statuses.
                if (read.Status != Constants.Timing.CHIPREAD_STATUS_ANNOUNCER_USED)
                {
                    read.Status = Constants.Timing.CHIPREAD_STATUS_ANNOUNCER_SEEN;
                }
            }
            database.UpdateChipReads(announcerReads);
            // The web server reads the list from its own thread.
            lock (AnLock)
            {
                Participants.AddRange(found);
                if (found.Count > 0) version++;
            }
            return found.Count > 0;
        }

        public void Run()
        {
            // Get the event we're looking at and fill the participant bib dictionary.
            Event theEvent = database.GetCurrentEvent()!;
            Dictionary<string, Participant> participantBibDictionary = [];
            foreach (Participant part in database.GetParticipants(theEvent.Identifier).Where(part => !participantBibDictionary.TryAdd(part.Bib, part)))
            {
                Log.D("Timing.Announcer.AnnouncerWorker", $"Multiples of a Bib found in participants set. {part.Bib}");
            }
            // Start from nothing; a previous run may have been for another event.
            lock (AnLock)
            {
                Participants.Clear();
                version++;
                BibSeen.Clear();
            }
            // Process any announcer reads that we've already used so we don't announce them later.
            ProcessReads(database.GetAnnouncerUsedChipReads(theEvent.Identifier), participantBibDictionary);
            // Loop while waiting for work.
            while (true)
            {
                try
                {
                    bool notified = Semaphore.WaitOne(1000 * Constants.Timing.ANNOUNCER_LOOP_TIMER);
                    if (AnLock.TryEnter(3000))
                    {
                        try
                        {
                            if (quittingTime)
                            {
                                Log.D("Timing.Announcer.AnnouncerWorker", "Exiting announcer thread.");
                                return;
                            }
                        }
                        finally
                        {
                            AnLock.Exit();
                        }
                    }
                    if (!notified) continue;
                    Log.D("Timing.Announcer.AnnouncerWorker", "New chip reads found!");
                    // Quit if the event changed; the announcer page is started again for the new one.
                    if (database.GetCurrentEvent()!.Identifier != theEvent.Identifier)
                    {
                        quittingTime = true;
                        Log.D("Timing.Announcer.AnnouncerWorker", "The event changed while the announcer was running.");
                        return;
                    }
                    // Ensure the event exists.
                    if (theEvent.Identifier == -1) continue;
                    // The announcer page asks for the list itself, so there's nothing to tell once the reads are processed.
                    ProcessReads(database.GetAnnouncerChipReads(theEvent.Identifier), participantBibDictionary);
                }
                catch (Exception e)
                {
                    Log.E("Timing.Announcer.AnnouncerWorker", $"Error processing announcer reads. {e}");
                }
            }
        }
    }
}

