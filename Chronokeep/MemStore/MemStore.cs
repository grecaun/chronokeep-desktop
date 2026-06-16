using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronoKeepAPI;
using Chronokeep.Objects.ChronokeepRemote;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Chronokeep.MemStore
{
    internal partial class MemStore : IDBInterface
    {
        internal class ChronoLockException(string message) : Exception(message) { }

        internal class InvalidEventID(string message) : Exception(message) { }

        private readonly int lockTimeout = 5000;

        // Singleton
        private static MemStore? instance;

        private static readonly Lock memStoreLock = new();

        // Items that don't rely on a specific Event
        // key == setting name
        private static readonly Dictionary<string, AppSetting> settings = [];
        // key == api id
        private static readonly Dictionary<int, ApiObject> apis = [];
        // key == ip
        private static readonly Dictionary<string, TimingSystem> timingSystems = [];
        private static readonly HashSet<string> bannedPhones = [];
        private static readonly HashSet<string> bannedEmails = [];

        // Event and the related fields
        private static readonly List<Event> allEvents = [];
        private static Event? theEvent;

        // key == distance id
        private static readonly Dictionary<int, Distance> distances = [];
        private static readonly Dictionary<string, Distance> distanceNameDict = [];
        // key == location id
        private static readonly Dictionary<int, TimingLocation> locations = [];
        // key == segment id
        private static readonly Dictionary<int, Segment> segments = [];
        // key == event specific id
        private static readonly Dictionary<int, Participant> participants = [];
        // key == chip
        private static readonly Dictionary<string, BibChipAssociation> chipToBibAssociations = [];
        private static readonly Dictionary<string, Dictionary<string, BibChipAssociation>> bibToChipAssociations = [];
        // ignored chips
        private static readonly Dictionary<string, BibChipAssociation> ignoredChips = [];
        // key == distanceId
        private static readonly Dictionary<int, List<AgeGroup>> ageGroups = [];
        private static readonly Dictionary<(int, int), AgeGroup> currentAgeGroups = [];
        private static readonly Dictionary<int, AgeGroup> lastAgeGroup = [];
        // key == (identifier)
        private static readonly List<Alarm> alarms = [];
        private static readonly List<RemoteReader> remoteReaders = [];
        private static readonly HashSet<(int, int)> smsAlerts = [];
        private static readonly HashSet<int> emailAlerts = [];
        private static readonly List<ApiSmsSubscription> smsSubscriptions = [];
        // key = (eventspecific_id, location_id, occurrence, unknown_id)
        private static readonly Dictionary<(int, int, int, string), TimeResult> timingResults = [];
        // Chip Read data
        private static readonly Dictionary<int, ChipRead> chipReads = [];
        // Clock data
        private static readonly Dictionary<int, Chronoclock> clocks = [];

        // Local variables
        private readonly IDBInterface database;

        private MemStore(IDBInterface database)
        {
            this.database = database;
        }

        public static MemStore GetMemStore(IDBInterface database)
        {
            instance ??= new(database);
            return instance;
        }

        public void LoadEvent()
        {
            // Load event
            theEvent = database.GetCurrentEvent();
            distances.Clear();
            distanceNameDict.Clear();
            locations.Clear();
            segments.Clear();
            participants.Clear();
            chipToBibAssociations.Clear();
            bibToChipAssociations.Clear();
            ignoredChips.Clear();
            ageGroups.Clear();
            alarms.Clear();
            remoteReaders.Clear();
            smsAlerts.Clear();
            emailAlerts.Clear();
            smsSubscriptions.Clear();
            timingResults.Clear();
            chipReads.Clear();
            clocks.Clear();
            if (theEvent == null || theEvent.Identifier < 0)
            {
                return;
            }
            // load distances
            foreach (Distance dist in database.GetDistances(theEvent.Identifier))
            {
                distances[dist.Identifier] = dist;
                distanceNameDict[dist.Name] = dist;
            }
            // load locations
            if (!theEvent.CommonStartFinish)
            {
                locations[Constants.Timing.LOCATION_ANNOUNCER] = new(Constants.Timing.LOCATION_ANNOUNCER, theEvent.Identifier, "Announcer", 0, 0);
                locations[Constants.Timing.LOCATION_FINISH] = new(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Finish", theEvent.FinishMaxOccurrences, theEvent.FinishIgnoreWithin);
                locations[Constants.Timing.LOCATION_START] = new(Constants.Timing.LOCATION_START, theEvent.Identifier, "Start", 0, theEvent.StartWindow);
            }
            else
            {
                locations[Constants.Timing.LOCATION_ANNOUNCER] = new(Constants.Timing.LOCATION_ANNOUNCER, theEvent.Identifier, "Announcer", 0, 0);
                locations[Constants.Timing.LOCATION_FINISH] = new(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Start/Finish", theEvent.FinishMaxOccurrences, theEvent.FinishIgnoreWithin);
            }
            foreach (TimingLocation loc in database.GetTimingLocations(theEvent.Identifier))
            {
                locations[loc.Identifier] = loc;
            }
            // load segments
            foreach (Segment seg in database.GetSegments(theEvent.Identifier))
            {
                segments[seg.Identifier] = seg;
            }
            // load participants
            foreach (Participant part in database.GetParticipants(theEvent.Identifier))
            {
                participants[part.EventSpecific.Identifier] = part;
            }
            // load bibchipassociations
            foreach (BibChipAssociation assoc in database.GetBibChips(theEvent.Identifier))
            {
                chipToBibAssociations[assoc.Chip] = assoc;
                if (!bibToChipAssociations.TryGetValue(assoc.Bib, out Dictionary<string, BibChipAssociation>? chipDict))
                {
                    chipDict = [];
                    bibToChipAssociations[assoc.Bib] = chipDict;
                }
                chipDict[assoc.Chip] = assoc;
            }
            foreach (BibChipAssociation assoc in database.GetBibChips(-1))
            {
                ignoredChips[assoc.Chip] = assoc;
            }
            // load age groups
            foreach (AgeGroup group in database.GetAgeGroups(theEvent.Identifier))
            {
                if (!ageGroups.TryGetValue(group.DistanceId, out List<AgeGroup>? value))
                {
                    value = [];
                    ageGroups[group.DistanceId] = value;
                }
                value.Add(group);
            }
            SetAgeGroups();
            // load alarms
            alarms.AddRange(database.GetAlarms(theEvent.Identifier));
            // load remote readers
            remoteReaders.AddRange(database.GetRemoteReaders(theEvent.Identifier));
            // load sms alerts sent
            foreach ((int, int) alert in database.GetSMSAlerts(theEvent.Identifier))
            {
                smsAlerts.Add(alert);
            }
            // load email alerts sent
            foreach (int alert in database.GetEmailAlerts(theEvent.Identifier))
            {
                emailAlerts.Add(alert);
            }
            // load sms subscriptions
            smsSubscriptions.AddRange(database.GetSmsSubscriptions(theEvent.Identifier));
            // load timingresults
            foreach (TimeResult result in database.GetTimingResults(theEvent.Identifier))
            {
                timingResults[(result.EventSpecificId, result.LocationId, result.Occurrence, result.UnknownId)] = result;
            }
            // load chipreads
            foreach (ChipRead read in database.GetChipReads(theEvent.Identifier))
            {
                chipReads[read.ReadId] = read;
            }
            // load clocks
            foreach (Chronoclock clock in database.GetClocks())
            {
                clocks[clock.Identifier] = clock;
            }
        }

        /**
         * Base Database Functions
         */

        internal static void ResetVariables()
        {
            // app setting
            settings.Clear();
            // api
            apis.Clear();
            // timing system
            timingSystems.Clear();
            // banned
            bannedEmails.Clear();
            bannedPhones.Clear();
            // distances
            distances.Clear();
            distanceNameDict.Clear();
            // locations
            locations.Clear();
            // segments
            segments.Clear();
            // participants
            participants.Clear();
            // age groups
            ageGroups.Clear();
            currentAgeGroups.Clear();
            lastAgeGroup.Clear();
            // alarms
            alarms.Clear();
            // alerts
            emailAlerts.Clear();
            smsAlerts.Clear();
            // bib chips
            chipToBibAssociations.Clear();
            bibToChipAssociations.Clear();
            // remote reader
            remoteReaders.Clear();
            // results
            timingResults.Clear();
            // chipread
            chipReads.Clear();
            clocks.Clear();
            // event
            theEvent = null;
        }

        public void HardResetDatabase()
        {
            Log.D("MemStore", "HardResetDatabase");
            database.HardResetDatabase();
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        ResetVariables();
                    }
                    finally
                    {
                        memStoreLock.Exit();
                    }
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronoLockException($"memStoreLock {e.Message}");
            }
        }

        public void Initialize()
        {
            database.Initialize();
            // Use eventLock to ensure nothing reads from the database.
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        // Load settings
                        // Settings 1
                        settings[Constants.Settings.SERVER_NAME] = database.GetAppSetting(Constants.Settings.SERVER_NAME)!;
                        settings[Constants.Settings.DATABASE_VERSION] = database.GetAppSetting(Constants.Settings.DATABASE_VERSION)!;
                        settings[Constants.Settings.HARDWARE_IDENTIFIER] = database.GetAppSetting(Constants.Settings.HARDWARE_IDENTIFIER)!;
                        settings[Constants.Settings.PROGRAM_VERSION] = database.GetAppSetting(Constants.Settings.PROGRAM_VERSION)!;
                        settings[Constants.Settings.AUTO_SHOW_CHANGELOG] = database.GetAppSetting(Constants.Settings.AUTO_SHOW_CHANGELOG)!;
                        // Settings 2
                        settings[Constants.Settings.DEFAULT_EXPORT_DIR] = database.GetAppSetting(Constants.Settings.DEFAULT_EXPORT_DIR)!;
                        settings[Constants.Settings.DEFAULT_TIMING_SYSTEM] = database.GetAppSetting(Constants.Settings.DEFAULT_TIMING_SYSTEM)!;
                        settings[Constants.Settings.CURRENT_EVENT] = database.GetAppSetting(Constants.Settings.CURRENT_EVENT)!;
                        settings[Constants.Settings.COMPANY_NAME] = database.GetAppSetting(Constants.Settings.COMPANY_NAME)!;
                        settings[Constants.Settings.CONTACT_EMAIL] = database.GetAppSetting(Constants.Settings.CONTACT_EMAIL)!;
                        // Settings 3
                        settings[Constants.Settings.UPDATE_ON_PAGE_CHANGE] = database.GetAppSetting(Constants.Settings.UPDATE_ON_PAGE_CHANGE)!;
                        settings[Constants.Settings.EXIT_NO_PROMPT] = database.GetAppSetting(Constants.Settings.EXIT_NO_PROMPT)!;
                        settings[Constants.Settings.DEFAULT_CHIP_TYPE] = database.GetAppSetting(Constants.Settings.DEFAULT_CHIP_TYPE)!;
                        settings[Constants.Settings.LAST_USED_API_ID] = database.GetAppSetting(Constants.Settings.LAST_USED_API_ID)!;
                        settings[Constants.Settings.CHECK_UPDATES] = database.GetAppSetting(Constants.Settings.CHECK_UPDATES)!;
                        settings[Constants.Settings.CURRENT_THEME] = database.GetAppSetting(Constants.Settings.CURRENT_THEME)!;
                        // Settings 4
                        settings[Constants.Settings.UPLOAD_INTERVAL] = database.GetAppSetting(Constants.Settings.UPLOAD_INTERVAL)!;
                        settings[Constants.Settings.DOWNLOAD_INTERVAL] = database.GetAppSetting(Constants.Settings.DOWNLOAD_INTERVAL)!;
                        settings[Constants.Settings.ANNOUNCER_WINDOW] = database.GetAppSetting(Constants.Settings.ANNOUNCER_WINDOW)!;
                        settings[Constants.Settings.ALARM_SOUND] = database.GetAppSetting(Constants.Settings.ALARM_SOUND)!;
                        settings[Constants.Settings.MINIMUM_COMPATIBLE_DATABASE] = database.GetAppSetting(Constants.Settings.MINIMUM_COMPATIBLE_DATABASE)!;
                        // Settings 5
                        settings[Constants.Settings.PROGRAM_UNIQUE_MODIFIER] = database.GetAppSetting(Constants.Settings.PROGRAM_UNIQUE_MODIFIER)!;
                        // Twilio
                        settings[Constants.Settings.TWILIO_ACCOUNT_SID] = database.GetAppSetting(Constants.Settings.TWILIO_ACCOUNT_SID)!;
                        settings[Constants.Settings.TWILIO_AUTH_TOKEN] = database.GetAppSetting(Constants.Settings.TWILIO_AUTH_TOKEN)!;
                        settings[Constants.Settings.TWILIO_PHONE_NUMBER] = database.GetAppSetting(Constants.Settings.TWILIO_PHONE_NUMBER)!;
                        // Mailgun
                        settings[Constants.Settings.MAILGUN_FROM_NAME] = database.GetAppSetting(Constants.Settings.MAILGUN_FROM_NAME)!;
                        settings[Constants.Settings.MAILGUN_FROM_EMAIL] = database.GetAppSetting(Constants.Settings.MAILGUN_FROM_EMAIL)!;
                        settings[Constants.Settings.MAILGUN_API_KEY] = database.GetAppSetting(Constants.Settings.MAILGUN_API_KEY)!;
                        settings[Constants.Settings.MAILGUN_API_URL] = database.GetAppSetting(Constants.Settings.MAILGUN_API_URL)!;
                        // Load apis
                        foreach (ApiObject api in database.GetAllAPI())
                        {
                            apis[api.Identifier] = api;
                        }
                        // load timingsystems
                        foreach (TimingSystem system in database.GetTimingSystems())
                        {
                            timingSystems[system.IpAddress.Trim()] = system;
                        }
                        // load banned phones
                        foreach (string phone in database.GetBannedPhones())
                        {
                            bannedPhones.Add(phone);
                        }
                        // load banned emails
                        foreach (string email in database.GetBannedEmails())
                        {
                            bannedEmails.Add(email);
                        }
                        // Load events
                        allEvents.Clear();
                        allEvents.AddRange(database.GetEvents());
                        // Load event data
                        LoadEvent();
                    }
                    finally
                    {
                        memStoreLock.Exit();
                    }
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronoLockException($"memStoreLock {e.Message}");
            }
        }

        public void ResetDatabase()
        {
            Log.D("MemStore", "ResetDatabase");
            database.ResetDatabase();
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        ResetVariables();
                    }
                    finally
                    {
                        memStoreLock.Exit();
                    }
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronoLockException($"memStoreLock {e.Message}");
            }
        }
    }
}
