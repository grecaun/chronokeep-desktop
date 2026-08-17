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

using Chronokeep.Helpers;
using Chronokeep.Objects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Chronokeep.MemStore
{
    internal partial class MemStore
    {
        /**
         * ChipRead Functions
         */

        public int AddChipRead(ChipRead read)
        {
            Log.D("MemStore", "AddChipRead");
            read.ReadId = database.AddChipRead(read);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return read.ReadId;
                try
                {
                    DateTime start = DateTime.Now;
                    if (theEvent != null)
                    {
                        start = DateTime.Parse(theEvent.Date).AddSeconds(theEvent.StartSeconds).AddMilliseconds(theEvent.StartMilliseconds);
                    }
                    read.Start = start;
                    read.ChipBib = chipToBibAssociations.TryGetValue(read.ChipNumber, out BibChipAssociation? ba) ? ba.Bib : Constants.Timing.CHIPREAD_DUMMYBIB;
                    read.LocationName = locations.TryGetValue(read.LocationId, out TimingLocation? loc) ? loc.Name : "";
                    Dictionary<string, Participant> partDictionary = [];
                    foreach (Participant part in participants.Values.Where(part => part.Bib.Length > 0))
                    {
                        partDictionary[part.Bib] = part;
                    }
                    read.Name = partDictionary.TryGetValue(read.Bib, out Participant? p) ? $"{p.FirstName} {p.LastName}".Trim() : "";
                    // Do not overwrite our current chip read.
                    if (read.ReadId > 0)
                    {
                        chipReads.TryAdd(read.ReadId, read);
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
                return read.ReadId;
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }

        public List<ChipRead> AddChipReads(List<ChipRead> reads)
        {
            Log.D("MemStore", "AddChipReads");
            List<ChipRead> output = database.AddChipReads(reads);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    Dictionary<string, Participant> partDictionary = [];
                    foreach (Participant part in participants.Values.Where(part => part.Bib.Length > 0))
                    {
                        partDictionary[part.Bib] = part;
                    }
                    DateTime start = DateTime.Now;
                    if (theEvent != null)
                    {
                        start = DateTime.Parse(theEvent.Date).AddSeconds(theEvent.StartSeconds).AddMilliseconds(theEvent.StartMilliseconds);
                    }
                    foreach (ChipRead read in output)
                    {
                        read.Start = start;
                        read.ChipBib = chipToBibAssociations.TryGetValue(read.ChipNumber, out BibChipAssociation? ba) ? ba.Bib : Constants.Timing.CHIPREAD_DUMMYBIB;
                        read.LocationName = locations.TryGetValue(read.LocationId, out TimingLocation? loc) ? loc.Name : "";
                        read.Name = partDictionary.TryGetValue(read.Bib, out Participant? p) ? $"{p.FirstName} {p.LastName}".Trim() : "";
                        // Do not overwrite our current chip read.
                        if (read.ReadId > 0)
                        {
                            chipReads.TryAdd(read.ReadId, read);
                        }
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
                return output;
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }

        public void DeleteChipReads(List<ChipRead> reads)
        {
            Log.D("MemStore", "DeleteChipReads");
            database.DeleteChipReads(reads);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    foreach (ChipRead read in reads)
                    {
                        chipReads.Remove(read.ReadId);
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }

        public List<ChipRead> GetChipReads()
        {
            Log.D("MemStore", "GetChipReads");
            List<ChipRead> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    output.AddRange(chipReads.Values);
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
            return output;
        }

        public List<ChipRead> GetAnnouncerChipReads(int eventId)
        {
            Log.D("MemStore", "GetAnnouncerChipReads");
            List<ChipRead> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    output.AddRange(chipReads.Values.Where(read => Constants.Timing.LOCATION_ANNOUNCER == read.LocationId && Constants.Timing.CHIPREAD_STATUS_NONE == read.Status));
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
            return output;
        }

        public List<ChipRead> GetAnnouncerUsedChipReads(int eventId)
        {
            Log.D("MemStore", "GetAnnouncerUsedChipReads");
            List<ChipRead> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    output.AddRange(chipReads.Values.Where(read => Constants.Timing.LOCATION_ANNOUNCER == read.LocationId && Constants.Timing.CHIPREAD_STATUS_ANNOUNCER_USED == read.Status));
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
            return output;
        }

        public List<ChipRead> GetChipReads(int eventId)
        {
            Log.D("MemStore", "GetChipReads");
            List<ChipRead> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    output.AddRange(chipReads.Values);
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
            return output;
        }

        public List<ChipRead> GetChipReadsSafemode(int eventId)
        {
            Log.D("MemStore", "GetChipReadsSafeMode");
            List<ChipRead> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    output.AddRange(chipReads.Values);
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
            return output;
        }

        public List<ChipRead> GetDnsChipReads(int eventId)
        {
            Log.D("MemStore", "GetDNSChipReads");
            List<ChipRead> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    output.AddRange(chipReads.Values.Where(read => Constants.Timing.CHIPREAD_STATUS_DNS == read.Status));
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
            return output;
        }

        public List<ChipRead> GetUsefulChipReads(int eventId)
        {
            Log.D("MemStore", "GetUsefulChipReads");
            List<ChipRead> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    output.AddRange(chipReads.Values.Where(read => read.IsUseful() && Constants.Timing.LOCATION_ANNOUNCER != read.LocationId));
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
            return output;
        }

        public void SetChipReadStatus(ChipRead read)
        {
            Log.D("MemStore", "SetChipReadStatus");
            database.SetChipReadStatus(read);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (chipReads.TryGetValue(read.ReadId, out ChipRead? known))
                    {
                        known.Status = read.Status;
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }

        public void SetChipReadStatuses(List<ChipRead> reads)
        {
            Log.D("MemStore", "SetChipReadStatuses");
            database.SetChipReadStatuses(reads);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    foreach (ChipRead read in reads)
                    {
                        if (chipReads.TryGetValue(read.ReadId, out ChipRead? known))
                        {
                            known.Status = read.Status;
                        }
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }

        public void UpdateChipRead(ChipRead read)
        {
            Log.D("MemStore", "UpdateChipRead");
            database.UpdateChipRead(read);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    if (!chipReads.TryGetValue(read.ReadId, out ChipRead? known)) return;
                    known.Status = read.Status;
                    known.TimeSeconds = read.TimeSeconds;
                    known.TimeMilliseconds = read.TimeMilliseconds;
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }

        public void UpdateChipReads(List<ChipRead> reads)
        {
            Log.D("MemStore", "UpdateChipReads");
            database.UpdateChipReads(reads);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    foreach (ChipRead read in reads)
                    {
                        if (!chipReads.TryGetValue(read.ReadId, out ChipRead? known)) continue;
                        known.Status = read.Status;
                        known.TimeSeconds = read.TimeSeconds;
                        known.TimeMilliseconds = read.TimeMilliseconds;
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }

        public bool UnprocessedReadsExist(int eventId)
        {
            Log.D("MemStore", "UnprocessedReadsExist");
            bool output = false;
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    if (chipReads.Values.Any(read => Constants.Timing.CHIPREAD_STATUS_NONE == read.Status))
                    {
                        output = true;
                    }
                }
                finally
                {
                    memStoreLock.Exit();
                }
                return output;
            }
            catch (Exception e)
            {
                Log.D("MemStore", $"Exception acquiring memStoreLock. {e.Message}");
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }
    }
}
