using Chronokeep.Database;
using Chronokeep.Helpers;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Chronokeep.Objects
{
    public class Alarm(int identifier, string bib, string chip, bool enabled, int sound) : IEquatable<Alarm>, IComparable<Alarm>
    {
        private static readonly List<Alarm> Alarms = [];
        private static readonly Dictionary<string, Alarm> BibAlarms = [];
        private static readonly Dictionary<string, Alarm> ChipAlarms = [];

        public int Identifier { get; set; } = identifier;
        public string Bib { get; set; } = bib;
        public string Chip { get; set; } = chip;
        public bool Enabled { get; set; } = enabled;
        // Any number not assigned to a sound (1-5 currently) is assumed to be the default.
        public int AlarmSound { get; set; } = sound;
        private static Lock ListMtx { get; } = new();

        public static void SaveAlarms(int eventId, IdbInterface database)
        {
            Log.D("Objects.Alarm", "Saving multiple alarms.");
            if (ListMtx.TryEnter(3000))
            {
                try
                {
                    database.SaveAlarms(eventId, Alarms);
                }
                finally
                {
                    ListMtx.Exit();
                }
            }
            ClearAlarms();
            AddAlarms(database.GetAlarms(eventId));
        }

        public static void SaveAlarm(int eventId, IdbInterface database, Alarm alarm)
        {
            Log.D("Objects.Alarm", "Saving single alarm.");
            if (ListMtx.TryEnter(3000))
            {
                try
                {
                    database.SaveAlarm(eventId, alarm);
                }
                finally
                {
                    ListMtx.Exit();
                }
            }
            ClearAlarms();
            AddAlarms(database.GetAlarms(eventId));
        }

        public static List<Alarm> GetAlarms()
        {
            Log.D("Objects.Alarm", "Getting alarms.");
            List<Alarm> output = [];
            if (!ListMtx.TryEnter(3000)) return output;
            try
            {
                output.AddRange(Alarms);
            }
            finally
            {
                ListMtx.Exit();
            }
            return output;
        }

        public static (Dictionary<string, Alarm>, Dictionary<string, Alarm>) GetAlarmDictionaries()
        {
            Dictionary<string, Alarm> outBib = [];
            Dictionary<string, Alarm> outChip = [];
            if (!ListMtx.TryEnter(3000)) return (outBib, outChip);
            try
            {
                outBib = new Dictionary<string, Alarm>(BibAlarms);
                outChip = new Dictionary<string, Alarm>(ChipAlarms);
            }
            finally
            {
                ListMtx.Exit();
            }
            return (outBib, outChip);
        }

        public static bool RemoveAlarm(Alarm alarm)
        {
            bool output = false;
            if (!ListMtx.TryEnter(3000)) return output;
            try
            {
                output = output && BibAlarms.Remove(alarm.Bib);
                output = output && ChipAlarms.Remove(alarm.Chip);
                output = output && Alarms.Remove(alarm);
            }
            finally
            {
                ListMtx.Exit();
            }
            return output;
        }

        public static bool ClearAlarms()
        {
            bool output = false;
            if (!ListMtx.TryEnter(3000)) return output;
            try
            {
                Alarms.Clear();
                BibAlarms.Clear();
                ChipAlarms.Clear();
                output = true;
            }
            finally
            {
                ListMtx.Exit();
            }
            return output;
        }

        public static bool AddAlarm(Alarm alarm)
        {
            Log.D("Objects.Alarm", "Adding alarm.");
            bool output = false;
            if (!ListMtx.TryEnter(3000)) return output;
            try
            {
                Alarms.Add(alarm);
                if (alarm.Bib.Length > 0)
                {
                    BibAlarms[alarm.Bib] = alarm;
                }
                if (alarm.Chip.Length > 0)
                {
                    ChipAlarms[alarm.Chip] = alarm;
                }
                output = true;
            }
            finally
            {
                ListMtx.Exit();
            }
            return output;
        }

        public static bool AddAlarms(List<Alarm> newAlarms)
        {
            Log.D("Objects.Alarm", "Adding alarms.");
            bool output = false;
            if (!ListMtx.TryEnter(3000)) return output;
            try
            {
                Log.D("Objects.Alarm", $"Number of alarms: {newAlarms.Count}");
                foreach (Alarm alarm in newAlarms)
                {
                    if (alarm.Bib.Length > 0)
                    {
                        BibAlarms[alarm.Bib] = alarm;
                    }
                    if (alarm.Chip.Length > 0)
                    {
                        ChipAlarms[alarm.Chip] = alarm;
                    }
                    output = true;
                }
                Alarms.Clear();
                Alarms.AddRange(BibAlarms.Values);
                Alarms.AddRange(ChipAlarms.Values);
            }
            finally
            {
                ListMtx.Exit();
            }
            return output;
        }

        public static Alarm? GetAlarmByBib(string bib)
        {
            Alarm? output = null;
            if (!ListMtx.TryEnter(3000)) return output;
            try
            {
                if (BibAlarms.TryGetValue(bib, out Alarm? alarm))
                {
                    output = alarm;
                }
            }
            finally
            {
                ListMtx.Exit();
            }
            return output;
        }

        public static Alarm? GetAlarmByChip(string chip)
        {
            Alarm? output = null;
            if (!ListMtx.TryEnter(3000)) return output;
            try
            {
                if (ChipAlarms.TryGetValue(chip, out Alarm? alarm))
                {
                    output = alarm;
                }
            }
            finally
            {
                ListMtx.Exit();
            }
            return output;
        }

        public int CompareTo(Alarm? other)
        {
            if (other == null) return 1;
            return Bib == other.Bib ? string.Compare(Chip, other.Chip, StringComparison.Ordinal) : string.Compare(Bib, other.Bib, StringComparison.Ordinal);
        }

        public bool Equals(Alarm? other)
        {
            if (other == null) return false;
            return Identifier == other.Identifier;
        }
    }
}
