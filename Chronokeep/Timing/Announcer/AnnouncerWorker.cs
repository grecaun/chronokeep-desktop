using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
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
        private readonly IMainWindow window;
        private static AnnouncerWorker? announcer;

        private static readonly Semaphore Semaphore = new(0, 2);
        private static readonly Lock AnLock = new();

        private static bool quittingTime;
        private static readonly List<AnnouncerParticipant> Participants = [];
        private static readonly Dictionary<string, DateTime> BibSeen = [];

        private const int SeenWindow = 5; // minutes

        private AnnouncerWorker(IMainWindow window, IdbInterface database)
        {
            this.window = window;
            this.database = database;
        }

        public static AnnouncerWorker NewAnnouncer(IMainWindow window, IdbInterface database)
        {
            announcer ??= new AnnouncerWorker(window, database);
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
            bool newParticipants = false;
            DateTime timeRightNow = DateTime.Now;
            foreach (ChipRead read in announcerReads)
            {
                // Check to ensure we know the bib of this person
                if (read.Bib != Constants.Timing.CHIPREAD_DUMMYBIB)
                {
                    // Check if we've already seen the bib (or we haven't seen the bib in seenWindow minutes).
                    // Only work if we've not seen it before.
                    if ((!BibSeen.TryGetValue(read.Bib, out DateTime lastSeen)
                        || lastSeen.AddMinutes(SeenWindow).CompareTo(timeRightNow) < 0)
                        && participantBibDictionary.TryGetValue(read.Bib, out Participant? part))
                    {
                        newParticipants = true;
                        BibSeen.Add(read.Bib, timeRightNow);
                        Participants.Add(new AnnouncerParticipant(part, read.Seconds));
                        // Mark this chipread as USED
                        read.Status = Constants.Timing.CHIPREAD_STATUS_ANNOUNCER_USED;
                    }
                }
                // Don't clobber over ANNOUNCER_USED statuses.
                if (read.Status != Constants.Timing.CHIPREAD_STATUS_ANNOUNCER_USED)
                {
                    read.Status = Constants.Timing.CHIPREAD_STATUS_ANNOUNCER_SEEN;
                }
            }
            database.UpdateChipReads(announcerReads);
            return newParticipants;
        }

        public void Run()
        {
            // Get the event we're looking at and fill the participant bib dictionary.
            Event theEvent = database.GetCurrentEvent()!;
            Dictionary<string, Participant> participantBibDictionary = [];
            foreach (Participant part in database.GetParticipants(theEvent.Identifier).Where(part => !participantBibDictionary.TryAdd(part.Bib, part)))
            {
                Log.D("Timing.Announcer.AnnouncerWorker", "Multiples of a Bib found in participants set. " + part.Bib);
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
                    if (notified)
                    {
                        Log.D("Timing.Announcer.AnnouncerWorker", "New chip reads found!");
                        Event ev2 = database.GetCurrentEvent()!;
                        // verify that we both ev2 and theevent are not null and they match
                        if (ev2.Identifier != theEvent.Identifier)
                        {
                            quittingTime = true;
                            Log.D("Timing.Announcer.AnnouncerWorker", "The event changed while the announcer window is open.");
                            return;
                        }
                        // Ensure the event exists.
                        if (theEvent.Identifier == -1) continue;
                        // If we've seen new participants update the window.
                        if (!ProcessReads(database.GetAnnouncerChipReads(theEvent.Identifier),
                                participantBibDictionary)) continue;
                        Log.D("Timing.Announcer.AnnouncerWorker", "There are people to announce.");
                    }
                    else
                    {
                        Log.D("Timing.Announcer.AnnouncerWorker", "Update window expired.");
                    }
                    window.UpdateAnnouncerWindow();
                }
                catch (Exception e)
                {
                    Log.E("AnnouncerWindow", $"Error processing announcer reads. {e}");
                }
            }
        }
    }
}
