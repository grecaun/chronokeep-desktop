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
using System;
using System.Collections.Generic;

namespace Chronokeep.MemStore
{
    internal partial class MemStore
    {
        /**
         * Banned Email/Phone Functions
         */

        public void AddBannedEmail(string email)
        {
            Log.D("MemStore", "AddBannedEmail");
            database.AddBannedEmail(email);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    bannedEmails.Add(email);
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

        public void AddBannedEmails(List<string> emails)
        {
            Log.D("MemStore", "AddBannedEmails");
            database.AddBannedEmails(emails);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    foreach (string email in emails)
                    {
                        bannedEmails.Add(email);
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

        public void AddBannedPhone(string phone)
        {
            Log.D("MemStore", "AddBannedPhone");
            database.AddBannedPhone(phone);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    bannedPhones.Add(phone);
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

        public void AddBannedPhones(List<string> phones)
        {
            Log.D("MemStore", "AddBannedPhones");
            database.AddBannedPhones(phones);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    foreach (string phone in phones)
                    {
                        bannedPhones.Add(phone);
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

        public List<string> GetBannedEmails()
        {
            Log.D("MemStore", "GetBannedEmails");
            List<string> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    output.AddRange(bannedEmails);
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

        public List<string> GetBannedPhones()
        {
            Log.D("MemStore", "GetBannedPhones");
            List<string> output = [];
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    output.AddRange(bannedPhones);
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

        public void RemoveBannedEmail(string email)
        {
            Log.D("MemStore", "RemoveBannedEmail");
            database.RemoveBannedEmail(email);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    bannedEmails.Remove(email);
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

        public void RemoveBannedEmails(List<string> emails)
        {
            Log.D("MemStore", "RemoveBannedEmails");
            database.RemoveBannedEmails(emails);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    foreach (string email in emails)
                    {
                        bannedEmails.Remove(email);
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

        public void RemoveBannedPhone(string phone)
        {
            Log.D("MemStore", "RemoveBannedPhone");
            database.RemoveBannedPhone(phone);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    bannedPhones.Remove(phone);
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

        public void RemoveBannedPhones(List<string> phones)
        {
            Log.D("MemStore", "RemoveBannedPhones");
            database.RemoveBannedPhones(phones);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    foreach (string phone in phones)
                    {
                        bannedPhones.Remove(phone);
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

        public void ClearBannedEmails()
        {
            Log.D("MemStore", "ClearBannedEmails");
            database.ClearBannedEmails();
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    bannedEmails.Clear();
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

        public void ClearBannedPhones()
        {
            Log.D("MemStore", "ClearBannedPhones");
            database.ClearBannedPhones();
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    bannedPhones.Clear();
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
    }
}
