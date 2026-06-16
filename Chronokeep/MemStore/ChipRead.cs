using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Objects;
using System;
using System.Collections.Generic;

namespace Chronokeep.MemStore
{
    internal partial class MemStore : IDBInterface
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
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        DateTime start = DateTime.Now;
                        if (theEvent != null)
                        {
                            start = DateTime.Parse(theEvent.Date).AddSeconds(theEvent.StartSeconds).AddMilliseconds(theEvent.StartMilliseconds);
                        }
                        read.Start = start;
                        if (chipToBibAssociations.TryGetValue(read.ChipNumber, out BibChipAssociation? ba))
                        {
                            read.ChipBib = ba.Bib;
                        }
                        else
                        {
                            read.ChipBib = Constants.Timing.CHIPREAD_DUMMYBIB;
                        }
                        read.ReadBib ??= Constants.Timing.CHIPREAD_DUMMYBIB;
                        if (locations.TryGetValue(read.LocationId, out TimingLocation? loc))
                        {
                            read.LocationName = loc.Name;
                        }
                        else
                        {
                            read.LocationName = "";
                        }
                        Dictionary<string, Participant> partDictionary = [];
                        foreach (Participant part in participants.Values)
                        {
                            if (part.Bib.Length > 0)
                            {
                                partDictionary[part.Bib] = part;
                            }
                        }
                        if (partDictionary.TryGetValue(read.Bib, out Participant? p))
                        {
                            read.Name = string.Format("{0} {1}", p.FirstName, p.LastName).Trim();
                        }
                        else
                        {
                            read.Name = "";
                        }
                        // Do not overwrite our current chipread.
                        if (read.ReadId > 0)
                        {
                            chipReads.TryAdd(read.ReadId, read);
                        }
                    }
                    finally
                    {
                        memStoreLock.Exit();
                    }
                }
                return read.ReadId;
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronoLockException($"memStoreLock {e.Message}");
            }
        }

        public List<ChipRead> AddChipReads(List<ChipRead> reads)
        {
            Log.D("MemStore", "AddChipReads");
            List<ChipRead> newReads = database.AddChipReads(reads);
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        Dictionary<string, Participant> partDictionary = [];
                        foreach (Participant part in participants.Values)
                        {
                            if (part.Bib.Length > 0)
                            {
                                partDictionary[part.Bib] = part;
                            }
                        }
                        DateTime start = DateTime.Now;
                        if (theEvent != null)
                        {
                            start = DateTime.Parse(theEvent.Date).AddSeconds(theEvent.StartSeconds).AddMilliseconds(theEvent.StartMilliseconds);
                        }
                        foreach (ChipRead read in newReads)
                        {
                            read.Start = start;
                            if (chipToBibAssociations.TryGetValue(read.ChipNumber, out BibChipAssociation? ba))
                            {
                                read.ChipBib = ba.Bib;
                            }
                            else
                            {
                                read.ChipBib = Constants.Timing.CHIPREAD_DUMMYBIB;
                            }
                            read.ReadBib ??= Constants.Timing.CHIPREAD_DUMMYBIB;
                            if (locations.TryGetValue(read.LocationId, out TimingLocation? loc))
                            {
                                read.LocationName = loc.Name;
                            }
                            else
                            {
                                read.LocationName = "";
                            }
                            if (partDictionary.TryGetValue(read.Bib, out Participant? p))
                            {
                                read.Name = string.Format("{0} {1}", p.FirstName, p.LastName).Trim();
                            }
                            else
                            {
                                read.Name = "";
                            }
                            // Do not overwrite our current chipread.
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
                }
                return newReads;
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronoLockException($"memStoreLock {e.Message}");
            }
        }

        public void DeleteChipReads(List<ChipRead> reads)
        {
            Log.D("MemStore", "DeleteChipReads");
            database.DeleteChipReads(reads);
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
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
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronoLockException($"memStoreLock {e.Message}");
            }
        }

        public List<ChipRead> GetChipReads()
        {
            Log.D("MemStore", "GetChipReads");
            List<ChipRead> output = [];
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        output.AddRange(chipReads.Values);
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
            return output;
        }

        public List<ChipRead> GetAnnouncerChipReads(int eventId)
        {
            Log.D("MemStore", "GetAnnouncerChipReads");
            List<ChipRead> output = [];
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        foreach (ChipRead read in chipReads.Values)
                        {
                            if (Constants.Timing.LOCATION_ANNOUNCER == read.LocationId
                                && Constants.Timing.CHIPREAD_STATUS_NONE == read.Status)
                            {
                                output.Add(read);
                            }
                        }
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
            return output;
        }

        public List<ChipRead> GetAnnouncerUsedChipReads(int eventId)
        {
            Log.D("MemStore", "GetAnnouncerUsedChipReads");
            List<ChipRead> output = [];
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        foreach (ChipRead read in chipReads.Values)
                        {
                            if (Constants.Timing.LOCATION_ANNOUNCER == read.LocationId
                                && Constants.Timing.CHIPREAD_STATUS_ANNOUNCER_USED == read.Status)
                            {
                                output.Add(read);
                            }
                        }
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
            return output;
        }

        public List<ChipRead> GetChipReads(int eventId)
        {
            Log.D("MemStore", "GetChipReads");
            List<ChipRead> output = [];
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        output.AddRange(chipReads.Values);
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
            return output;
        }

        public List<ChipRead> GetChipReadsSafemode(int eventId)
        {
            Log.D("MemStore", "GetChipReadsSafemode");
            List<ChipRead> output = [];
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        output.AddRange(chipReads.Values);
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
            return output;
        }

        public List<ChipRead> GetDNSChipReads(int eventId)
        {
            Log.D("MemStore", "GetDNSChipReads");
            List<ChipRead> output = [];
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        foreach (ChipRead read in chipReads.Values)
                        {
                            if (Constants.Timing.CHIPREAD_STATUS_DNS == read.Status)
                            {
                                output.Add(read);
                            }
                        }
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
            return output;
        }

        public List<ChipRead> GetUsefulChipReads(int eventId)
        {
            Log.D("MemStore", "GetUsefulChipReads");
            List<ChipRead> output = [];
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        foreach (ChipRead read in chipReads.Values)
                        {
                            if (read.IsUseful() && Constants.Timing.LOCATION_ANNOUNCER != read.LocationId)
                            {
                                output.Add(read);
                            }
                        }
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
            return output;
        }

        public void SetChipReadStatus(ChipRead read)
        {
            Log.D("MemStore", "SetChipReadStatus");
            database.SetChipReadStatus(read);
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
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
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronoLockException($"memStoreLock {e.Message}");
            }
        }

        public void SetChipReadStatuses(List<ChipRead> reads)
        {
            Log.D("MemStore", "SetChipReadStatuses");
            database.SetChipReadStatuses(reads);
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
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
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronoLockException($"memStoreLock {e.Message}");
            }
        }

        public void UpdateChipRead(ChipRead read)
        {
            Log.D("MemStore", "UpdateChipRead");
            database.UpdateChipRead(read);
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        if (chipReads.TryGetValue(read.ReadId, out ChipRead? known))
                        {
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
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronoLockException($"memStoreLock {e.Message}");
            }
        }

        public void UpdateChipReads(List<ChipRead> reads)
        {
            Log.D("MemStore", "UpdateChipReads");
            database.UpdateChipReads(reads);
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        foreach (ChipRead read in reads)
                        {
                            if (chipReads.TryGetValue(read.ReadId, out ChipRead? known))
                            {
                                known.Status = read.Status;
                                known.TimeSeconds = read.TimeSeconds;
                                known.TimeMilliseconds = read.TimeMilliseconds;
                            }
                        }
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

        public bool UnprocessedReadsExist(int eventId)
        {
            Log.D("MemStore", "UnprocessedReadsExist");
            bool output = false;
            try
            {
                if (memStoreLock.TryEnter(lockTimeout))
                {
                    try
                    {
                        foreach (ChipRead read in chipReads.Values)
                        {
                            if (Constants.Timing.CHIPREAD_STATUS_NONE == read.Status)
                            {
                                output = true;
                                break;
                            }
                        }
                    }
                    finally
                    {
                        memStoreLock.Exit();
                    }
                }
                return output;
            }
            catch (Exception e)
            {
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronoLockException($"memStoreLock {e.Message}");
            }
        }
    }
}
