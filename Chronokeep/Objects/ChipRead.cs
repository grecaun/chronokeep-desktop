using System;

namespace Chronokeep.Objects
{
    public class ChipRead : IComparable<ChipRead>
    {
        public int ReadId { get; set; }
        public int EventId { get; set; }
        public int Status { get; set; }
        public int LocationId { get; set; }
        public string ChipNumber { get; set; }
        public long Seconds { get; }
        public int Milliseconds { get; }
        public long TimeSeconds { get; set; }
        public int TimeMilliseconds { get; set; }
        public int Antenna { get; }
        public string Reader { get; }
        public string Box { get; set; }
        public int LogId { get; }
        public string Rssi { get; }
        public int IsRewind { get; }
        public string ReaderTime { get; }
        public long StartTime { get; }
        public string ReadBib { get; set; }
        public string ChipBib { get; set; } = "";
        public string Name { get; set; } = "";
        public int Type { get; }

        // RawReads window functions
        internal DateTime Start { get; set; }
        public string LocationName { get; set; } = "";
        public string Bib => Constants.Timing.CHIPREAD_TYPE_CHIP == Type ? ChipBib : ReadBib;

        // This constructor is used when receiving a read from a RFID Ultra 8
        public ChipRead(
            int eventId,
            int locationId,
            string chipNumber,
            long seconds,
            int millisec,
            int antenna,
            string rssi,
            int isRewind,
            string reader,
            string box,
            string readertime,
            long starttime,
            int logid
            )
        {
            EventId = eventId;
            Status = Constants.Timing.CHIPREAD_STATUS_NONE;
            LocationId = locationId;
            ChipNumber = chipNumber;
            Seconds = seconds;
            Milliseconds = millisec;
            Antenna = antenna;
            Rssi = rssi;
            IsRewind = isRewind;
            Reader = reader;
            Box = box;
            ReaderTime = readertime;
            StartTime = starttime;
            LogId = logid;
            TimeSeconds = seconds;
            TimeMilliseconds = millisec;
            Type = Constants.Timing.CHIPREAD_TYPE_CHIP;
            ReadBib = Constants.Timing.CHIPREAD_DUMMYBIB;
        }

        // This constructor is used when receiving a read from an Ipico system
        public ChipRead(
            int eventId,
            int locationId,
            string chipNumber,
            DateTime time,
            int antenna,
            int isRewind
            )
        {
            ReadBib = Constants.Timing.CHIPREAD_DUMMYBIB;
            TimeSeconds = Constants.Timing.RfidDateToEpoch(time);
            TimeMilliseconds = time.Millisecond;
            Type = Constants.Timing.CHIPREAD_TYPE_CHIP;
            EventId = eventId;
            Status = Constants.Timing.CHIPREAD_STATUS_NONE;
            LocationId = locationId;
            ChipNumber = chipNumber.Trim();
            Seconds = Constants.Timing.RfidDateToEpoch(time);
            Milliseconds = time.Millisecond;
            Antenna = antenna;
            Rssi = "";
            IsRewind = isRewind;
            Reader = "I";
            Box = "Ipico";
            ReaderTime = "";
            StartTime = 0;
            LogId = 0;
        }

        // This constructor is used when receiving a read from a Chronokeep Portal system
        public ChipRead(
            int eventId,
            int locationId,
            bool chipIsChip,
            string chipNumber,
            long seconds,
            int millisec,
            int antenna,
            string rssi,
            string reader,
            int readType,
            string readertime,
            string box
            )
        {
            EventId = eventId;
            Status = Constants.Timing.CHIPREAD_STATUS_NONE;
            LocationId = locationId;
            Seconds = seconds;
            Milliseconds = millisec;
            Antenna = antenna;
            Rssi = rssi;
            IsRewind = 0;
            Reader = reader;
            Box = box;
            StartTime = 0;
            LogId = 0;
            TimeSeconds = seconds;
            TimeMilliseconds = millisec;
            Type = readType;
            ReaderTime = readertime;
            if (chipIsChip)
            {
                ChipNumber = chipNumber;
                ReadBib = Constants.Timing.CHIPREAD_DUMMYBIB;
            }
            else // chip is a bib
            {
                ChipNumber = Constants.Timing.CHIPREAD_DUMMYCHIP;
                ReadBib = chipNumber;
            }
        }

        // This is the OLD database' constructor.
        public ChipRead(
            int readId,
            int eventId,
            int status,
            int locationId,
            long chipNumber,
            long seconds,
            int millisec,
            int antenna,
            string rssi,
            int isRewind,
            string reader,
            string box,
            string readertime,
            long starttime,
            int logid,
            DateTime time,
            int readbib,
            int type
            )
        {
            ReadId = readId;
            EventId = eventId;
            Status = status;
            LocationId = locationId;
            ChipNumber = chipNumber.ToString();
            Seconds = seconds;
            Milliseconds = millisec;
            Antenna = antenna;
            Rssi = rssi;
            IsRewind = isRewind;
            Reader = reader;
            Box = box;
            ReaderTime = readertime;
            StartTime = starttime;
            LogId = logid;
            TimeSeconds = Constants.Timing.RfidDateToEpoch(time);
            TimeMilliseconds = time.Millisecond;
            ReadBib = readbib.ToString();
            Type = type;
        }

        // new database constructor
        public ChipRead(
            int readId,
            int eventId,
            int status,
            int locationId,
            string chipNumber,
            long seconds,
            int millisec,
            int antenna,
            string rssi,
            int isRewind,
            string reader,
            string box,
            string readertime,
            long starttime,
            int logid,
            long timeSeconds,
            int timeMillisec,
            string readbib,
            int type,
            string chipbib,
            string first,
            string last,
            DateTime start,
            string locationName
            )
        {
            ReadId = readId;
            EventId = eventId;
            Status = status;
            LocationId = locationId;
            ChipNumber = chipNumber;
            Seconds = seconds;
            Milliseconds = millisec;
            Antenna = antenna;
            Rssi = rssi;
            IsRewind = isRewind;
            Reader = reader;
            Box = box;
            ReaderTime = readertime;
            StartTime = starttime;
            LogId = logid;
            TimeSeconds = timeSeconds;
            TimeMilliseconds = timeMillisec;
            ReadBib = readbib;
            Type = type;
            ChipBib = chipbib;
            Name = $"{first} {last}".Trim();
            Start = start;
            LocationName = locationName;
        }

        // Constructor used in manual entry.
        public ChipRead(
            int eventId,
            int locationId,
            string bib,
            DateTime time,
            int status
            )
        {
            ReadBib = bib;
            TimeSeconds = Constants.Timing.RfidDateToEpoch(time);
            TimeMilliseconds = time.Millisecond;
            Type = Constants.Timing.CHIPREAD_TYPE_MANUAL;
            EventId = eventId;
            Status = status;
            LocationId = locationId;
            ChipNumber = Constants.Timing.CHIPREAD_DUMMYCHIP;
            Seconds = Constants.Timing.RfidDateToEpoch(time);
            Milliseconds = time.Millisecond;
            Antenna = 0;
            Rssi = "";
            IsRewind = 0;
            Reader = "M";
            Box = "Man";
            ReaderTime = "";
            StartTime = 0;
            LogId = 0;
        }

        // Constructor used when loading from a Log
        public ChipRead(
            int eventId,
            int locationId,
            string chip,
            DateTime time
            )
        {
            ReadBib = Constants.Timing.CHIPREAD_DUMMYBIB;
            TimeSeconds = Constants.Timing.RfidDateToEpoch(time);
            TimeMilliseconds = time.Millisecond;
            Type = Constants.Timing.CHIPREAD_TYPE_CHIP;
            EventId = eventId;
            Status = Constants.Timing.CHIPREAD_STATUS_NONE;
            LocationId = locationId;
            ChipNumber = chip.Trim();
            Seconds = Constants.Timing.RfidDateToEpoch(time);
            Milliseconds = time.Millisecond;
            Antenna = 0;
            Rssi = "";
            IsRewind = 0;
            Reader = "L";
            Box = "Log";
            ReaderTime = "";
            StartTime = 0;
            LogId = 0;
        }

        // Constructor used for loading data from a chronokeep log, fake variable used to differentiate between this and the old db constructor.
        public ChipRead(
            int eventId,
            int locationId,
            int status,
            string chipNumber,
            long seconds,
            int milliseconds,
            long timeSeconds,
            int timeMilliseconds,
            int antenna,
            string reader,
            string box,
            int logIndex,
            string rssi,
            int isRewind,
            string readerTime,
            long startTime,
            string readBib,
            int type)
        {
            EventId = eventId;
            LocationId = locationId;
            Status = status;
            ChipNumber = chipNumber;
            Seconds = seconds;
            Milliseconds = milliseconds;
            TimeSeconds = timeSeconds;
            TimeMilliseconds = timeMilliseconds;
            Antenna = antenna;
            Reader = reader;
            Box = box;
            LogId = logIndex;
            Rssi = rssi;
            IsRewind = isRewind;
            ReaderTime = readerTime;
            StartTime = startTime;
            ReadBib = readBib ?? throw new ArgumentNullException(nameof(readBib));
            Type = type;
        }

        // Constructor used for loading data from a remote reader.
        public ChipRead(
            int eventId,
            int locationId,
            string chip,
            string bib,
            long seconds,
            int milliseconds,
            int antenna,
            string reader,
            string rssi,
            int type
            )
        {
            EventId = eventId;
            LocationId = locationId;
            Status = Constants.Timing.CHIPREAD_STATUS_NONE;
            ChipNumber = chip;
            ReadBib = bib;
            TimeSeconds = Constants.Timing.UtcSecondsToRfidSeconds(seconds);
            TimeMilliseconds = milliseconds;
            Seconds = Constants.Timing.UtcSecondsToRfidSeconds(seconds);
            Milliseconds = milliseconds;
            Antenna = antenna;
            Reader = reader;
            Rssi = rssi;
            Type = type;
            IsRewind = 0;
            Box = "Remote";
            ReaderTime = "";
            StartTime = 0;
            LogId = 0;
        }

        public string TimeString => Time.ToString("yyyy-MM-dd HH:mm:ss.fff");

        public string NetTime
        {
            get
            {
                TimeSpan ellapsed = Time - Start;
                return $"{ellapsed.Days * 24 + ellapsed.Hours}:{Math.Abs(ellapsed.Minutes):D2}:{Math.Abs(ellapsed.Seconds):D2}.{Math.Abs(ellapsed.Milliseconds):D3}";
            }
        }

        public DateTime Time => Constants.Timing.RfidEpochToDate(TimeSeconds).AddMilliseconds(TimeMilliseconds);

        public string TypeName => Constants.Timing.CHIPREAD_TYPE_MANUAL == Type ? "Manual" : "Chip";

        public string StatusName
        {
            get
            {
                if (Constants.Timing.CHIPREAD_STATUS_NONE == Status)
                {
                    return "Unprocessed";
                }
                if (Constants.Timing.CHIPREAD_STATUS_PRESTART == Status)
                {
                    return "Before Start";
                }
                if (Constants.Timing.CHIPREAD_STATUS_UNUSEDSTART == Status)
                {
                    return "Unused Start";
                }
                if (Constants.Timing.CHIPREAD_STATUS_IGNORE == Status ||
                    Constants.Timing.CHIPREAD_STATUS_DNF_IGNORE == Status ||
                    Constants.Timing.CHIPREAD_STATUS_DNS_IGNORE == Status)
                {
                    return "Ignored";
                }
                if (Constants.Timing.CHIPREAD_STATUS_USED == Status)
                {
                    return "Used";
                }
                if (Constants.Timing.CHIPREAD_STATUS_WITHINIGN == Status)
                {
                    return "Too Soon";
                }
                if (Constants.Timing.CHIPREAD_STATUS_OVERMAX == Status)
                {
                    return "Extra";
                }
                if (Constants.Timing.CHIPREAD_STATUS_STARTTIME == Status)
                {
                    return "Start";
                }
                if (Constants.Timing.CHIPREAD_STATUS_DNF == Status || Constants.Timing.CHIPREAD_STATUS_AUTO_DNF == Status)
                {
                    return "DNF";
                }
                if (Constants.Timing.CHIPREAD_STATUS_ANNOUNCER_SEEN == Status)
                {
                    return "A-Seen";
                }
                if (Constants.Timing.CHIPREAD_STATUS_ANNOUNCER_USED == Status)
                {
                    return "A-Used";
                }
                if (Constants.Timing.CHIPREAD_STATUS_DNS == Status)
                {
                    return "DNS";
                }
                if (Constants.Timing.CHIPREAD_STATUS_AFTER_DNS == Status)
                {
                    return "After DNS";
                }
                return Constants.Timing.CHIPREAD_STATUS_AFTER_FINISH == Status ? "After Finish" : "Unknown";
            }
        }

        public int CompareTo(ChipRead? other)
        {
            return other == null ? -1 : Time.CompareTo(other.Time);
        }

        public static int CompareByBib(ChipRead one, ChipRead two)
        {
            // Check if they're the same bib
            // Make sure they're not the dummy bib numer, then compare them.
            if (Constants.Timing.CHIPREAD_DUMMYBIB != one.ReadBib && (one.ReadBib == two.ReadBib || one.ReadBib == two.ChipBib) ||
                 Constants.Timing.CHIPREAD_DUMMYBIB != one.ChipBib && (one.ChipBib == two.ChipBib || one.ChipBib == two.ReadBib))
            {
                return one.Time.CompareTo(two.Time);
            }
            string stringBibOne = one.ReadBib == Constants.Timing.CHIPREAD_DUMMYBIB ? one.ChipBib : one.ReadBib;
            string stringBibTwo = two.ReadBib == Constants.Timing.CHIPREAD_DUMMYBIB ? two.ChipBib : two.ReadBib;
            if (int.TryParse(stringBibOne, out int intBibOne) && int.TryParse(stringBibTwo, out int intBibTwo))
            {
                return intBibOne.CompareTo(intBibTwo);
            }
            return string.Compare(stringBibOne, stringBibTwo, StringComparison.Ordinal);
        }

        public bool IsNotMatch(string value)
        {
            return !Bib.Contains(value, StringComparison.OrdinalIgnoreCase)
                && !Name.Contains(value, StringComparison.OrdinalIgnoreCase)
                && !ChipNumber.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsIgnored()
        {
            return Constants.Timing.CHIPREAD_STATUS_DNF_IGNORE == Status ||
                    Constants.Timing.CHIPREAD_STATUS_DNS_IGNORE == Status ||
                    Constants.Timing.CHIPREAD_STATUS_IGNORE == Status;
        }

        public bool IsUseful()
        {
            return Constants.Timing.CHIPREAD_STATUS_NONE == Status
                || Constants.Timing.CHIPREAD_STATUS_USED == Status
                || Constants.Timing.CHIPREAD_STATUS_STARTTIME == Status
                || Constants.Timing.CHIPREAD_STATUS_DNF == Status
                || Constants.Timing.CHIPREAD_STATUS_DNS == Status
                || Constants.Timing.CHIPREAD_STATUS_AUTO_DNF == Status;
        }

        public bool CanBeReset()
        {
            return Constants.Timing.CHIPREAD_STATUS_IGNORE != Status
                && Constants.Timing.CHIPREAD_STATUS_DNS != Status
                && Constants.Timing.CHIPREAD_STATUS_DNS_IGNORE != Status
                && Constants.Timing.CHIPREAD_STATUS_DNF != Status
                && Constants.Timing.CHIPREAD_STATUS_DNF_IGNORE != Status;
        }

        public bool Equals(ChipRead other)
        {
            return ReadId == other.ReadId
                   || EventId == other.EventId
                   && LocationId == other.LocationId
                   && ChipNumber == other.ChipNumber
                   && Seconds == other.Seconds
                   && Milliseconds == other.Milliseconds
                   && Antenna == other.Antenna
                   && Rssi == other.Rssi
                   && IsRewind == other.IsRewind
                   && Reader == other.Reader
                   && Box == other.Box
                   && ReaderTime == other.ReaderTime
                   && StartTime == other.StartTime
                   && LogId == other.LogId
                   && TimeSeconds == other.TimeSeconds
                   && TimeMilliseconds == other.TimeMilliseconds
                   && ReadBib == other.ReadBib
                   && Type == other.Type
                   && ChipBib == other.ChipBib
                   && Name == other.Name
                   && Start == other.Start
                   && LocationName == other.LocationName;
        }
    }
}
