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
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
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
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
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
                Log.D("MemStore", "Exception acquiring memStoreLock. " + e.Message);
                throw new ChronokeepLockException($"memStoreLock {e.Message}");
            }
        }
    }
}