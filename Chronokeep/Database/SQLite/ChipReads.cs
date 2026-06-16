using Chronokeep.Objects;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Chronokeep.Database.SQLite
{
    class ChipReads
    {
        internal static int AddChipRead(ChipRead read, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO chipreads (event_id, read_status, location_id, read_chipnumber, read_seconds, " +
                "read_milliseconds, read_antenna, read_reader, read_box, read_logindex, read_rssi, read_isrewind, read_readertime, " +
                "read_starttime, read_time_seconds, read_time_milliseconds, read_bib, read_type)" +
                " VALUES (@event, @status, @loc, @chip, @sec, @milli, @ant, @reader, @box, @logix, @rssi, @rewind, @readertime, " +
                "@starttime, @timesec, @timemill, @bib, @type);";
            command.Parameters.AddRange(
            [
                new("@event", read.EventId),
                new("@status", read.Status),
                new("@loc", read.LocationId),
                new("@chip", read.ChipNumber),
                new("@sec", read.Seconds),
                new("@milli", read.Milliseconds),
                new("@ant", read.Antenna),
                new("@reader", read.Reader),
                new("@box", read.Box),
                new("@logix", read.LogId),
                new("@rssi", read.Rssi),
                new("@rewind", read.IsRewind),
                new("@readertime", read.ReaderTime),
                new("@starttime", read.StartTime),
                new("@timesec", read.TimeSeconds),
                new("@timemill", read.TimeMilliseconds),
                new("@bib", read.ReadBib),
                new("@type", read.Type)
            ]);
            command.ExecuteNonQuery();
            command.CommandText = "SELECT read_id FROM chipreads " +
                "WHERE event_id=@event " +
                "AND read_chipnumber=@chip " +
                "AND read_bib=@bib " +
                "AND read_seconds=@sec " +
                "AND read_milliseconds=@milli;";
            command.Parameters.AddRange(
            [
                new("@event", read.EventId),
                new("@chip", read.ChipNumber),
                new("@sec", read.Seconds),
                new("@milli", read.Milliseconds),
                new("@bib", read.ReadBib)
            ]);
            SQLiteDataReader reader = command.ExecuteReader();
            int outVal = -1;
            if (reader.Read())
            {
                outVal = Convert.ToInt32(reader["read_id"]);
            }
            reader.Close();
            return outVal;
        }

        internal static void UpdateChipRead(ChipRead read, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE chipreads SET read_status=@status, read_time_seconds=@time, read_time_milliseconds=@mill WHERE read_id=@id;";
            command.Parameters.AddRange(
            [
                    new("@status", read.Status),
                    new("@id", read.ReadId),
                    new("@time", read.TimeSeconds),
                    new("@mill", read.TimeMilliseconds)
            ]);
            command.ExecuteNonQuery();
        }

        internal static void SetChipReadStatus(ChipRead read, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE chipreads SET read_status=@status WHERE read_id=@id;";
            command.Parameters.AddRange(
            [
                    new("@status", read.Status),
                    new("@id", read.ReadId)
            ]);
            command.ExecuteNonQuery();
        }

        internal static void DeleteChipReads(List<ChipRead> reads, SQLiteConnection connection)
        {
            if (reads.Count < 1) return;
            using var transaction = connection.BeginTransaction();
            foreach (ChipRead read in reads)
            {
                SQLiteCommand command = connection.CreateCommand();
                command.CommandText = "DELETE FROM chipreads WHERE read_id=@read;";
                command.Parameters.Add(new("@read", read.ReadId));
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        internal static List<ChipRead> GetChipReadsSafemode(int eventId, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM chipreads WHERE event_id=@event;";
            command.Parameters.Add(new("@event", eventId));
            SQLiteDataReader reader = command.ExecuteReader();
            List<ChipRead> output = [];
            while (reader.Read())
            {
                output.Add(new(
                    Convert.ToInt32(reader["read_id"]),
                    Convert.ToInt32(reader["event_id"]),
                    Convert.ToInt32(reader["read_status"]),
                    Convert.ToInt32(reader["location_id"]),
                    reader["read_chipnumber"] != DBNull.Value ? reader["read_chipnumber"].ToString()! : Constants.Timing.CHIPREAD_DUMMYCHIP,
                    Convert.ToInt64(reader["read_seconds"]),
                    Convert.ToInt32(reader["read_milliseconds"]),
                    Convert.ToInt32(reader["read_antenna"]),
                    reader["read_rssi"].ToString()!,
                    Convert.ToInt32(reader["read_isrewind"]),
                    reader["read_reader"].ToString()!,
                    reader["read_box"].ToString()!,
                    reader["read_readertime"].ToString()!,
                    Convert.ToInt32(reader["read_starttime"]),
                    Convert.ToInt32(reader["read_logindex"]),
                    Convert.ToInt64(reader["read_time_seconds"]),
                    Convert.ToInt32(reader["read_time_milliseconds"]),
                    reader["read_bib"] != DBNull.Value ? reader["read_bib"].ToString()! : Constants.Timing.CHIPREAD_DUMMYBIB,
                    Convert.ToInt32(reader["read_type"]),
                    Constants.Timing.CHIPREAD_DUMMYBIB,
                    "",
                    "",
                    DateTime.MinValue,
                    ""
                    ));
            }
            reader.Close();
            return output;
        }

        internal static List<ChipRead> GetChipReads(Event theEvent, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM chipreads c LEFT JOIN bib_chip_assoc b ON (c.read_chipnumber=b.chip AND c.event_id=b.event_id) ";
            SQLiteDataReader reader = command.ExecuteReader();
            List<ChipRead> output = GetChipReadsWorker(reader, theEvent, connection);
            return output;
        }

        internal static List<ChipRead> GetChipReads(int eventId, Event theEvent, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM chipreads c LEFT JOIN bib_chip_assoc b ON (c.read_chipnumber=b.chip AND c.event_id=b.event_id) " +
                "WHERE c.event_id=@event;";
            command.Parameters.Add(new("@event", eventId));
            SQLiteDataReader reader = command.ExecuteReader();
            List<ChipRead> output = GetChipReadsWorker(reader, theEvent, connection);
            return output;
        }

        internal static List<ChipRead> GetUsefulChipReads(int eventId, Event theEvent, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM chipreads c LEFT JOIN bib_chip_assoc b on (c.read_chipnumber=b.chip AND c.event_id=b.event_id) " +
                "WHERE c.event_id=@event AND " +
                "(read_status=@status OR read_status=@used OR read_status=@start OR read_status=@dnf OR read_status=@dns OR read_status=@autoDNF) " +
                "AND c.location_id!=@announcer;";
            command.Parameters.AddRange(
            [
                new("@event", eventId),
                new("@status", Constants.Timing.CHIPREAD_STATUS_NONE),
                new("@used", Constants.Timing.CHIPREAD_STATUS_USED),
                new("@start", Constants.Timing.CHIPREAD_STATUS_STARTTIME),
                new("@dnf", Constants.Timing.CHIPREAD_STATUS_DNF),
                new("@dns", Constants.Timing.CHIPREAD_STATUS_DNS),
                new("@announcer", Constants.Timing.LOCATION_ANNOUNCER),
                new("@autoDNF", Constants.Timing.CHIPREAD_STATUS_AUTO_DNF)
            ]);
            SQLiteDataReader reader = command.ExecuteReader();
            List<ChipRead> output = GetChipReadsWorker(reader, theEvent, connection);
            return output;
        }

        internal static List<ChipRead> GetAnnouncerChipReads(int eventId, Event theEvent, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM chipreads c LEFT JOIN bib_chip_assoc b on (c.read_chipnumber=b.chip AND c.event_id=b.event_id) " +
                "WHERE c.event_id=@event AND " +
                "c.location_id=@announcer AND read_status=@none;";
            command.Parameters.AddRange(
            [
                new("@event", eventId),
                new("@none", Constants.Timing.CHIPREAD_STATUS_NONE),
                new("@announcer", Constants.Timing.LOCATION_ANNOUNCER)
            ]);
            SQLiteDataReader reader = command.ExecuteReader();
            List<ChipRead> output = GetChipReadsWorker(reader, theEvent, connection);
            return output;
        }

        internal static List<ChipRead> GetAnnouncerUsedChipReads(int eventId, Event theEvent, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM chipreads c LEFT JOIN bib_chip_assoc b on (c.read_chipnumber=b.chip AND c.event_id=b.event_id) " +
                "WHERE c.event_id=@event AND " +
                "c.location_id=@announcer AND read_status=@used;";
            command.Parameters.AddRange(
            [
                new("@event", eventId),
                new("@announcer", Constants.Timing.LOCATION_ANNOUNCER),
                new("@used", Constants.Timing.CHIPREAD_STATUS_ANNOUNCER_USED)
            ]);
            SQLiteDataReader reader = command.ExecuteReader();
            List<ChipRead> output = GetChipReadsWorker(reader, theEvent, connection);
            return output;
        }

        internal static List<ChipRead> GetDNSChipReads(int eventId, Event theEvent, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM chipreads c LEFT JOIN bib_chip_assoc b on (c.read_chipnumber=b.chip AND c.event_id=b.event_id) " +
                "WHERE c.event_id=@event AND " +
                " read_status=@dns;";
            command.Parameters.AddRange(
            [
                new("@event", eventId),
                new("@dns", Constants.Timing.CHIPREAD_STATUS_DNS)
            ]);
            SQLiteDataReader reader = command.ExecuteReader();
            List<ChipRead> output = GetChipReadsWorker(reader, theEvent, connection);
            return output;
        }

        internal static List<ChipRead> GetChipReadsWorker(SQLiteDataReader reader, Event theEvent, SQLiteConnection connection)
        {
            DateTime start = DateTime.Now;
            if (theEvent != null && theEvent.Date != null)
            {
                start = DateTime.Parse(theEvent.Date).AddSeconds(theEvent.StartSeconds).AddMilliseconds(theEvent.StartMilliseconds);
            }
            List<TimingLocation> locations = [];
            if (theEvent != null) locations.AddRange(TimingLocations.GetTimingLocations(theEvent.Identifier, connection));
            if (theEvent != null && !theEvent.CommonStartFinish)
            {
                locations.Insert(0, new(Constants.Timing.LOCATION_ANNOUNCER, theEvent.Identifier, "Announcer", 0, 0));
                locations.Insert(0, new(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Finish", theEvent.FinishMaxOccurrences, theEvent.FinishIgnoreWithin));
                locations.Insert(0, new(Constants.Timing.LOCATION_START, theEvent.Identifier, "Start", 0, theEvent.StartWindow));
            }
            else if (theEvent != null)
            {
                locations.Insert(0, new(Constants.Timing.LOCATION_ANNOUNCER, theEvent.Identifier, "Announcer", 0, 0));
                locations.Insert(0, new(Constants.Timing.LOCATION_FINISH, theEvent == null ? -1 : theEvent.Identifier, "Start/Finish", theEvent == null ? 1 : theEvent.FinishMaxOccurrences, theEvent == null ? 0 : theEvent.FinishIgnoreWithin));
            }
            Dictionary<int, string> locDict = [];
            foreach (TimingLocation loc in locations)
            {
                locDict[loc.Identifier] = loc.Name;
            }
            List<ChipRead> output = [];
            Dictionary<string, Participant> partDictionary = [];
            if (theEvent != null)
            {
                foreach (Participant p in Participants.GetParticipants(theEvent!.Identifier, connection))
                {
                    if (p.Bib.Length > 0)
                    {
                        partDictionary[p.Bib] = p;
                    }
                }
            }
            while (reader.Read())
            {
                int locationId = Convert.ToInt32(reader["location_id"]);
                string locationName = locDict.TryGetValue(locationId, out string? locNa) ? locNa : "";
                string read_bib = reader["read_bib"] != DBNull.Value ? reader["read_bib"].ToString()! : Constants.Timing.CHIPREAD_DUMMYBIB;
                string bib = reader["bib"] != DBNull.Value ? reader["bib"].ToString()! : Constants.Timing.CHIPREAD_DUMMYBIB;
                Participant? part = null;
                if (bib.Length > 0 && bib != Constants.Timing.CHIPREAD_DUMMYBIB && partDictionary.TryGetValue(bib, out Participant? bibPart))
                {
                    part = bibPart;
                }
                else if (read_bib.Length > 0 && read_bib != Constants.Timing.CHIPREAD_DUMMYBIB && partDictionary.TryGetValue(read_bib, out Participant? readBibPart))
                {
                    part = readBibPart;
                }
                int read_id = Convert.ToInt32(reader["read_id"]);
                int event_id = Convert.ToInt32(reader["event_id"]);
                int read_status = Convert.ToInt32(reader["read_status"]);
                long read_seconds = Convert.ToInt64(reader["read_seconds"]);
                int read_milliseconds = Convert.ToInt32(reader["read_milliseconds"]);
                int read_antenna = Convert.ToInt32(reader["read_antenna"]);
                int read_isrewind = Convert.ToInt32(reader["read_isrewind"]);
                int read_starttime = Convert.ToInt32(reader["read_starttime"]);
                int read_logindex = Convert.ToInt32(reader["read_logindex"]);
                long read_time_seconds = Convert.ToInt64(reader["read_time_seconds"]);
                int read_time_milliseconds = Convert.ToInt32(reader["read_time_milliseconds"]);
                int read_type = Convert.ToInt32(reader["read_type"]);
                output.Add(new(
                    read_id,
                    event_id,
                    read_status,
                    locationId,
                    reader["read_chipnumber"] != DBNull.Value ? reader["read_chipnumber"].ToString()! : Constants.Timing.CHIPREAD_DUMMYCHIP,
                    read_seconds,
                    read_milliseconds,
                    read_antenna,
                    reader["read_rssi"].ToString()!,
                    read_isrewind,
                    reader["read_reader"].ToString()!,
                    reader["read_box"].ToString()!,
                    reader["read_readertime"].ToString()!,
                    read_starttime,
                    read_logindex,
                    read_time_seconds,
                    read_time_milliseconds,
                    read_bib,
                    read_type,
                    bib,
                    part == null ? "" : part.FirstName,
                    part == null ? "" : part.LastName,
                    start,
                    locationName
                    ));
            }
            reader.Close();
            return output;
        }

        internal static long UnprocessedReadsExist(int eventId, SQLiteConnection connection)
        {
            SQLiteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM chipreads WHERE event_id=@event AND read_status=@status;";
            command.Parameters.AddRange(
            [
                new("@event", eventId),
                new("@status", Constants.Timing.CHIPREAD_STATUS_NONE)
            ]);
            SQLiteDataReader reader = command.ExecuteReader();
            reader.Read();
            long output = reader.GetInt64(0);
            reader.Close();
            return output;
        }
    }
}
