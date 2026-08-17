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

namespace Chronokeep.MemStore
{
    internal partial class MemStore
    {
        /**
         * AppSetting Functions
         */

        public AppSetting? GetAppSetting(string name)
        {
            Log.D("MemStore", "GetAppSetting");
            AppSetting? output = null;
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return output;
                try
                {
                    settings.TryGetValue(name, out output);
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

        public void SetAppSetting(string name, string value)
        {
            Log.D("MemStore", "SetAppSetting");
            database.SetAppSetting(name, value);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    settings[name] = new AppSetting { Name = name, Value = value };
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

        public void SetAppSetting(AppSetting setting)
        {
            Log.D("MemStore", "SetAppSetting");
            database.SetAppSetting(setting);
            try
            {
                if (!memStoreLock.TryEnter(LockTimeout)) return;
                try
                {
                    settings[setting.Name] = setting;
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
