using Chronokeep.Objects;
using Chronokeep.Objects.ChronoKeepAPI;
using Chronokeep.Objects.ChronokeepRemote;
using System.Collections.Generic;

namespace Chronokeep.Database
{
    public interface IDBInterface
    {
        // Database Functions
        void Initialize();
        void ResetDatabase();
        void HardResetDatabase();

        // Settings functions
        AppSetting? GetAppSetting(string name);
        void SetAppSetting(string name, string value);
        void SetAppSetting(AppSetting setting);

        // Results API Functions
        int AddAPI(ApiObject anAPI);
        void UpdateAPI(ApiObject anAPI);
        void RemoveAPI(int identifier);
        ApiObject? GetAPI(int identifier);
        List<ApiObject> GetAllAPI();

        // Event Functions
        int AddEvent(Event anEvent);
        void RemoveEvent(int identifier);
        void RemoveEvent(Event anEvent);
        void UpdateEvent(Event anEvent);
        int GetEventID(Event anEvent);
        void SetStartOptions(Event anEvent);
        void SetFinishOptions(Event anEvent);
        void SetStartFinishOptions(Event anEvent);
        Event? GetCurrentEvent();
        void SetCurrentEvent(int eventID);
        Event? GetEvent(int id);
        List<Event> GetEvents();
        void UpdateDivisionsEnabled();
        void UpdateStart();

        // Distance Functions
        int AddDistance(Distance div);
        List<Distance> AddDistances(List<Distance> distances);
        void RemoveDistance(int identifier);
        void RemoveDistance(Distance div);
        void UpdateDistance(Distance div);
        int GetDistanceID(Distance div);
        List<Distance> GetDistances(int eventId);
        Distance? GetDistance(int divId);
        void SetWaveTimes(int eventId, int wave, long seconds, int milliseconds);

        // Timing Location Functions
        int AddTimingLocation(TimingLocation tp);
        List<TimingLocation> AddTimingLocations(List<TimingLocation> locations);
        void RemoveTimingLocation(TimingLocation tp);
        void RemoveTimingLocation(int identifier);
        void UpdateTimingLocation(TimingLocation tp);
        int GetTimingLocationID(TimingLocation tp);
        List<TimingLocation> GetTimingLocations(int eventId);

        // Segment Functions
        int AddSegment(Segment seg);
        List<Segment> AddSegments(List<Segment> segments);
        void RemoveSegment(Segment seg);
        void RemoveSegment(int identifier);
        void RemoveSegments(List<Segment> segments);
        void UpdateSegment(Segment seg);
        void UpdateSegments(List<Segment> segments);
        int GetSegmentId(Segment seg);
        List<Segment> GetSegments(int eventId);
        void ResetSegments(int eventId);
        int GetMaxSegments(int eventId);

        // Participant Functions
        Participant? AddParticipant(Participant person);
        List<Participant> AddParticipants(List<Participant> people);
        void RemoveParticipant(int identifier);
        void RemoveParticipantEntry(Participant person);
        void RemoveParticipantEntries(List<Participant> participants);
        void UpdateParticipant(Participant person);
        void UpdateParticipants(List<Participant> participants);
        int GetParticipantID(Participant person);
        List<Participant> GetParticipants();
        List<Participant> GetParticipants(int eventId);
        List<Participant> GetParticipants(int eventId, int distanceId);
        Participant? GetParticipant(int eventIdentifier, int identifier);
        Participant? GetParticipant(int eventIdentifier, Participant unknown);
        Participant? GetParticipantEventSpecific(int eventIdentifier, int eventSpecificId);
        Participant? GetParticipantBib(int eventIdentifier, string bib);
        Participant? GetParticipantChip(int eventIdentifier, string chip);
        List<string> GetDivisions(int eventIdentifier);

        // Bib Chip Association Functions
        void AddBibChipAssociation(int eventId, List<BibChipAssociation> assoc);
        List<BibChipAssociation> GetBibChips();
        List<BibChipAssociation> GetBibChips(int eventId);
        void RemoveBibChipAssociation(int eventId, string chip);
        void RemoveBibChipAssociation(BibChipAssociation assoc);
        void RemoveBibChipAssociations(List<BibChipAssociation> assocs);

        // Timing Result Functions
        void AddTimingResult(TimeResult tr);
        void AddTimingResults(List<TimeResult> results);
        void RemoveTimingResult(TimeResult tr);
        List<TimeResult> GetTimingResults(int eventId);
        List<TimeResult> GetStartTimes(int eventId);
        List<TimeResult> GetFinishTimes(int eventId);
        List<TimeResult> GetSegmentTimes(int eventId, int segmentId);
        List<TimeResult> GetLastSeenResults(int eventId);
        bool UnprocessedReadsExist(int eventId);
        bool UnprocessedResultsExist(int eventId);
        void SetUploadedTimingResults(List<TimeResult> results);
        List<TimeResult> GetNonUploadedResults(int eventId);

        // Timing analytics... sort of
        List<DistanceStat> GetDistanceStats(int eventId, bool condense = false);
        Dictionary<int, List<Participant>> GetDistanceParticipantsStatus(int eventId, int distanceId);

        // Reset functions for ChipReads/TimeResults
        void ResetTimingResultsEvent(int eventId);                      // event based reset
        void ResetTimingResultsPlacements(int eventId);

        // Chip Read Functions
        int AddChipRead(ChipRead read);
        List<ChipRead> AddChipReads(List<ChipRead> reads);
        void UpdateChipRead(ChipRead read);
        void UpdateChipReads(List<ChipRead> reads);
        void SetChipReadStatus(ChipRead read);
        void SetChipReadStatuses(List<ChipRead> reads);
        void DeleteChipReads(List<ChipRead> reads);
        List<ChipRead> GetChipReadsSafemode(int eventId);
        List<ChipRead> GetChipReads();
        List<ChipRead> GetChipReads(int eventId);
        List<ChipRead> GetUsefulChipReads(int eventId);
        List<ChipRead> GetAnnouncerChipReads(int eventId);
        List<ChipRead> GetAnnouncerUsedChipReads(int eventId);
        List<ChipRead> GetDNSChipReads(int eventId);

        // Age Group Functions
        int AddAgeGroup(AgeGroup group);
        List<AgeGroup> AddAgeGroups(List<AgeGroup> groups);
        void UpdateAgeGroup(AgeGroup group);
        void RemoveAgeGroup(AgeGroup group);
        void RemoveAgeGroups(int eventId, int distanceId);
        void RemoveAgeGroups(List<AgeGroup> groups);
        void ResetAgeGroups(int eventId);
        List<AgeGroup> GetAgeGroups(int eventId);
        List<AgeGroup> GetAgeGroups(int eventId, int distanceId);

        // Timing Systems
        int AddTimingSystem(TimingSystem system);
        void UpdateTimingSystem(TimingSystem system);
        void SetTimingSystems(List<TimingSystem> systems);
        void RemoveTimingSystem(TimingSystem system);
        void RemoveTimingSystem(int systemId);
        List<TimingSystem> GetTimingSystems();

        // Alarms
        List<Alarm> SaveAlarms(int eventId, List<Alarm> alarms);
        int SaveAlarm(int eventId, Alarm alarm);
        List<Alarm> GetAlarms(int eventId);
        void DeleteAlarms(int eventId);
        void DeleteAlarm(Alarm alarm);

        // Remote Readers
        void AddRemoteReaders(int eventId, List<RemoteReader> readers);
        void DeleteRemoteReaders(int eventId, List<RemoteReader> readers);
        void DeleteRemoteReader(int eventId, RemoteReader reader);
        List<RemoteReader> GetRemoteReaders(int eventId);

        // SMS Alerts
        void AddSMSAlert(int eventId, int eventspecific_id, int segment_id);
        List<(int, int)> GetSMSAlerts(int eventId);

        // Email Alerts
        void AddEmailAlert(int eventId, int eventspecific_id);
        List<int> GetEmailAlerts(int eventId);

        // Banned phones/emails functions
        List<string> GetBannedPhones();
        void AddBannedPhone(string phone);
        void AddBannedPhones(List<string> phones);
        List<string> GetBannedEmails();
        void AddBannedEmail(string email);
        void AddBannedEmails(List<string> emails);
        void RemoveBannedEmail(string email);
        void RemoveBannedEmails(List<string> emails);
        void RemoveBannedPhone(string phone);
        void RemoveBannedPhones(List<string> phones);
        void ClearBannedEmails();
        void ClearBannedPhones();

        // SMS Subscription functions
        List<ApiSmsSubscription> GetSmsSubscriptions(int eventId);
        void AddSmsSubscriptions(int eventId, List<ApiSmsSubscription> subscriptions);
        void DeleteSmsSubscriptions(int eventId);

        // Chronoclock functions
        List<Chronoclock> GetClocks();
        int AddClock(Chronoclock clock);
        void UpdateClock(Chronoclock clock);
        void RemoveClocks(List<Chronoclock> clocks);
    }
}
