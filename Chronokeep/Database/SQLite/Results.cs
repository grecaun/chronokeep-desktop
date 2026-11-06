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

using Chronokeep.Objects;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;

namespace Chronokeep.Database.SQLite
{
    internal static class Results
    {
        /// <summary>
        /// Everything needed to turn time_results rows into TimeResult objects for one event.
        /// Built per query from the event being read, so it never depends on which event is current.
        /// </summary>
        private sealed class ResultContext
        {
            public required Event Event { get; init; }
            public Dictionary<int, ChipRead> ChipReads { get; } = [];
            public Dictionary<int, Participant> Participants { get; } = [];
            public Dictionary<int, Distance> DistancesById { get; } = [];
            public Dictionary<string, Distance> DistancesByName { get; } = [];
            public Dictionary<int, TimingLocation> Locations { get; } = [];
            public Dictionary<int, Segment> Segments { get; } = [];
        }

        private static ResultContext? LoadContext(int eventId, SQLiteConnection connection)
        {
            Event? theEvent = Events.GetEvent(eventId, connection);
            if (theEvent == null)
            {
                return null;
            }
            ResultContext context = new() { Event = theEvent };
            foreach (ChipRead cr in ChipReads.GetChipReads(eventId, theEvent, connection))
            {
                context.ChipReads[cr.ReadId] = cr;
            }
            foreach (Participant p in Participants.GetParticipants(eventId, connection))
            {
                context.Participants[p.EventSpecific.Identifier] = p;
            }
            foreach (Distance d in Distances.GetDistances(eventId, connection))
            {
                context.DistancesById[d.Identifier] = d;
                context.DistancesByName[d.Name] = d;
            }
            if (!theEvent.CommonStartFinish)
            {
                context.Locations[Constants.Timing.LOCATION_FINISH] = new TimingLocation(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Finish", theEvent.FinishMaxOccurrences, theEvent.FinishIgnoreWithin);
                context.Locations[Constants.Timing.LOCATION_START] = new TimingLocation(Constants.Timing.LOCATION_START, theEvent.Identifier, "Start", 0, theEvent.StartWindow);
            }
            else
            {
                context.Locations[Constants.Timing.LOCATION_FINISH] = new TimingLocation(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Start/Finish", theEvent.FinishMaxOccurrences, theEvent.FinishIgnoreWithin);
            }
            foreach (TimingLocation loc in TimingLocations.GetTimingLocations(eventId, connection))
            {
                context.Locations[loc.Identifier] = loc;
            }
            foreach (Segment seg in Segments.GetSegments(eventId, connection))
            {
                context.Segments[seg.Identifier] = seg;
            }
            return context;
        }

        internal static void AddTimingResult(TimeResult tr, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandText = "INSERT INTO time_results (event_id, eventspecific_id, location_id, segment_id, " +
                "timeresult_occurance, timeresult_time, timeresult_unknown_id, read_id, timeresult_chiptime," +
                "timeresult_place, timeresult_age_place, timeresult_gender_place," +
                "timeresult_status, timeresult_splittime, timeresult_uploaded, timeresult_division_place)" +
                " VALUES (@event,@specific,@location,@segment,@occ,@time,@unknown,@read,@chip,@place,@agplace," +
                "@gendplace,@status,@split,@uploaded,@divPlace)";
            command.Parameters.AddRange([
                new SQLiteParameter("@event", tr.EventIdentifier),
                new SQLiteParameter("@specific", tr.EventSpecificId),
                new SQLiteParameter("@location", tr.LocationId),
                new SQLiteParameter("@segment", tr.SegmentId),
                new SQLiteParameter("@occ", tr.Occurrence),
                new SQLiteParameter("@time", tr.Time),
                new SQLiteParameter("@unknown", tr.UnknownId),
                new SQLiteParameter("@read", tr.ReadId),
                new SQLiteParameter("@chip", tr.ChipTime),
                new SQLiteParameter("@place", tr.Place),
                new SQLiteParameter("@agplace", tr.AgePlace),
                new SQLiteParameter("@gendplace", tr.GenderPlace),
                new SQLiteParameter("@status", tr.Status),
                new SQLiteParameter("@split", tr.LapTime),
                new SQLiteParameter("@uploaded", tr.Uploaded),
                new SQLiteParameter("@divPlace", tr.DivisionPlace)
            ]);
            command.ExecuteNonQuery();
        }

        internal static void RemoveTimingResult(TimeResult tr, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandText = "DELETE FROM time_results WHERE eventspecific_id=@event AND location_id=@location AND " +
                "segment_id=@segment AND timeresult_occurance=@occurance";
            command.Parameters.AddRange([
                new SQLiteParameter("@event", tr.EventSpecificId),
                new SQLiteParameter("@segment", tr.SegmentId),
                new SQLiteParameter("@occurance", tr.Occurrence),
                new SQLiteParameter("@location", tr.LocationId) ]);
            command.ExecuteNonQuery();
        }

        private static List<TimeResult> GetResultsInternal(SQLiteDataReader reader, ResultContext context)
        {
            List<TimeResult> output = [];
            while (reader.Read())
            {
                int eventSpecificId = reader["eventspecific_id"] == DBNull.Value ? -1 : Convert.ToInt32(reader["eventspecific_id"]);
                int readId = Convert.ToInt32(reader["read_id"]);
                int distanceId = -1;
                string bib = "";
                Participant? part = null;
                context.ChipReads.TryGetValue(readId, out ChipRead? chipRead);
                if (eventSpecificId > -1 && context.Participants.TryGetValue(eventSpecificId, out part))
                {
                    bib = part.Bib;
                    distanceId = part.EventSpecific.DistanceIdentifier;
                }
                else if (chipRead != null)
                {
                    bib = chipRead.Bib;
                }
                bool knownDist = context.DistancesById.TryGetValue(distanceId, out Distance? di);
                output.Add(new TimeResult(
                    reader["event_id"] == DBNull.Value ? -1 : Convert.ToInt32(reader["event_id"]),
                    reader["eventspecific_id"] == DBNull.Value ? -1 : Convert.ToInt32(reader["eventspecific_id"]),
                    Convert.ToInt32(reader["location_id"]),
                    Convert.ToInt32(reader["segment_id"]),
                    reader["timeresult_time"].ToString()!,
                    Convert.ToInt32(reader["timeresult_occurance"]),
                    part != null ? part.FirstName : "",
                    part != null ? part.LastName : "",
                    knownDist ? di!.Name : "",
                    bib,
                    readId,
                    reader["timeresult_unknown_id"].ToString()!,
                    chipRead?.TimeSeconds ?? 0,
                    chipRead?.TimeMilliseconds ?? 0,
                    reader["timeresult_chiptime"].ToString()!,
                    Convert.ToInt32(reader["timeresult_place"]),
                    Convert.ToInt32(reader["timeresult_age_place"]),
                    Convert.ToInt32(reader["timeresult_gender_place"]),
                    part != null ? part.Gender : "",
                    Convert.ToInt32(reader["timeresult_status"]),
                    reader["timeresult_splittime"].ToString()!,
                    part != null ? part.EventSpecific.AgeGroupId : -1,
                    part != null ? part.EventSpecific.AgeGroupName : "",
                    Convert.ToInt32(reader["timeresult_uploaded"]),
                    part != null ? part.Birthdate : "",
                    knownDist ? di!.Type : Constants.Timing.DISTANCE_TYPE_NORMAL,
                    knownDist && context.DistancesById.TryGetValue(di!.LinkedDistance, out Distance? linked) ? linked.Name : "",
                    chipRead != null ? chipRead.ChipNumber : "",
                    part is { EventSpecific.Anonymous: true },
                    part != null ? part.Identifier.ToString() : "",
                    context.Locations,
                    context.Segments,
                    context.DistancesByName,
                    context.Event,
                    part != null ? part.EventSpecific.Division : "",
                    Convert.ToInt32(reader["timeresult_division_place"])
                    ));
            }
            reader.Close();
            return output;
        }

        internal static List<TimeResult> GetTimingResults(int eventId, SQLiteConnection connection)
        {
            ResultContext? context = LoadContext(eventId, connection);
            if (context == null)
            {
                return [];
            }
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * " +
                "FROM time_results r " +
                "WHERE r.event_id=@eventid";
            command.Parameters.Add(new SQLiteParameter("@eventid", eventId));
            SQLiteDataReader reader = command.ExecuteReader();
            List<TimeResult> output = GetResultsInternal(reader, context);
            return output;
        }

        internal static List<TimeResult> GetLastSeenResults(int eventId, SQLiteConnection connection)
        {
            ResultContext? context = LoadContext(eventId, connection);
            if (context == null)
            {
                return [];
            }
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * " +
                "FROM time_results r " +
                "WHERE r.event_id=@eventid " +
                "GROUP BY eventspecific_id;";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@eventid", eventId),
            ]);
            SQLiteDataReader reader = command.ExecuteReader();
            List<TimeResult> output = GetResultsInternal(reader, context);
            return output;
        }

        internal static List<TimeResult> GetFinishTimes(int eventId, SQLiteConnection connection)
        {
            return GetSegmentTimes(eventId, Constants.Timing.SEGMENT_FINISH, connection);
        }

        internal static List<TimeResult> GetStartTimes(int eventId, SQLiteConnection connection)
        {
            return GetSegmentTimes(eventId, Constants.Timing.SEGMENT_START, connection);
        }

        internal static List<TimeResult> GetSegmentTimes(int eventId, int segmentId, SQLiteConnection connection)
        {
            ResultContext? context = LoadContext(eventId, connection);
            if (context == null)
            {
                return [];
            }
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * " +
                "FROM time_results r " +
                "WHERE r.event_id=@eventid AND r.segment_id=@segment;";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@eventid", eventId),
                new SQLiteParameter("@segment", segmentId)
            ]);
            SQLiteDataReader reader = command.ExecuteReader();
            List<TimeResult> output = GetResultsInternal(reader, context);
            return output;
        }

        internal static void UpdateTimingResult(TimeResult oldResult, string newTime, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandText = "UPDATE time_results SET timeresult_time=@time WHERE event_id=@event AND eventspecific_id=@eventspecific AND location_id=@location AND timeresult_occurance=@occurance";
            command.Parameters.AddRange([
                new SQLiteParameter("@time", newTime),
                new SQLiteParameter("@event", oldResult.EventIdentifier),
                new SQLiteParameter("@eventspecific", oldResult.EventSpecificId),
                new SQLiteParameter("@location", oldResult.LocationId),
                new SQLiteParameter("@occurance", oldResult.Occurrence)]);
            command.ExecuteNonQuery();
        }

        internal static void SetUploadedTimingResults(List<TimeResult> results, SQLiteConnection connection)
        {
            using SQLiteTransaction? transaction = connection.BeginTransaction();
            foreach (TimeResult result in results)
            {
                SQLiteCommand command = connection.CreateCommand();
                command.CommandType = CommandType.Text;
                command.CommandText = "UPDATE time_results SET timeresult_uploaded=@uploaded WHERE event_id=@event AND eventspecific_id=@eventspecific AND location_id=@location AND timeresult_occurance=@occurance";
                command.Parameters.AddRange([
                    new SQLiteParameter("@uploaded", result.Uploaded),
                    new SQLiteParameter("@event", result.EventIdentifier),
                    new SQLiteParameter("@eventspecific", result.EventSpecificId),
                    new SQLiteParameter("@location", result.LocationId),
                    new SQLiteParameter("@occurance", result.Occurrence)]
                );
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        internal static List<TimeResult> GetNonUploadedResults(int eventId, SQLiteConnection connection)
        {
            ResultContext? context = LoadContext(eventId, connection);
            if (context == null)
            {
                return [];
            }
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * " +
                "FROM time_results r " +
                "WHERE r.event_id=@eventid AND r.timeresult_uploaded=@uploaded;";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@eventid", eventId),
                new SQLiteParameter("@uploaded") { Value = Constants.Timing.TIMERESULT_UPLOADED_FALSE }
            ]);
            SQLiteDataReader reader = command.ExecuteReader();
            List<TimeResult> output = GetResultsInternal(reader, context);
            return output;
        }

        internal static long UnprocessedResultsExist(int eventId, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM time_results " +
                "WHERE event_id=@event AND timeresult_status=@status " +
                "AND segment_id<>@start AND segment_id<>@none " +
                "AND eventspecific_id<>@dummy;";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@event", eventId),
                new SQLiteParameter("@status") { Value = Constants.Timing.TIMERESULT_STATUS_NONE },
                new SQLiteParameter("@start", Constants.Timing.SEGMENT_START),
                new SQLiteParameter("@dummy", Constants.Timing.TIMERESULT_DUMMYPERSON),
                new SQLiteParameter("@none", Constants.Timing.SEGMENT_NONE)
            ]);
            SQLiteDataReader reader = command.ExecuteReader();
            reader.Read();
            long output = reader.GetInt64(0);
            reader.Close();
            return output;
        }

        /*
         * Reset options for time_results and chipreads
         */

        internal static void ResetTimingResultsEvent(int eventId, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM time_results WHERE event_id=@event;" +
                "UPDATE chipreads SET read_status=@status WHERE event_id=@event AND read_status!=@ignore " +
                "AND read_status!=@dnf AND read_status!=@dnf_ignore AND read_status!=@dns AND read_status!=@dns_ignore;" +
                "UPDATE eventspecific SET eventspecific_status=@estatus WHERE event_id=@event;";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@event", eventId),
                new SQLiteParameter("@status") { Value = Constants.Timing.CHIPREAD_STATUS_NONE },
                new SQLiteParameter("@ignore", Constants.Timing.CHIPREAD_STATUS_IGNORE),
                new SQLiteParameter("@dnf", Constants.Timing.CHIPREAD_STATUS_DNF),
                new SQLiteParameter("@dnf_ignore", Constants.Timing.CHIPREAD_STATUS_DNF_IGNORE),
                new SQLiteParameter("@dns", Constants.Timing.CHIPREAD_STATUS_DNS),
                new SQLiteParameter("@dns_ignore", Constants.Timing.CHIPREAD_STATUS_DNS_IGNORE),
                new SQLiteParameter("@estatus") { Value = Constants.Timing.EVENTSPECIFIC_UNKNOWN }
            ]);
            command.ExecuteNonQuery();
        }

        internal static void ResetTimingResultsPlacements(int eventId, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE time_results SET timeresult_status=@status WHERE event_id=@event;";
            command.Parameters.AddRange(
            [
                new SQLiteParameter("@event", eventId),
                new SQLiteParameter("@status") { Value = Constants.Timing.CHIPREAD_STATUS_NONE }
            ]);
            command.ExecuteNonQuery();
        }
    }
}
