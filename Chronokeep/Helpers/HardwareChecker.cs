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

using Avalonia;
using Chronokeep.Database;
using Chronokeep.Objects;
using Chronokeep.UI.Util;
using Hardware.Info;
using System;
using System.Text;

namespace Chronokeep.Helpers
{
    internal class HardwareChecker(IdbInterface database)
    {
        public void Run()
        {
            try
            {
                Log.D("Helpers.HardwareChecker", "Fetching hardware information.");
                HardwareInfo hardwareInfo = new();
                hardwareInfo.RefreshOperatingSystem();
                hardwareInfo.RefreshCPUList(false, 10);
                hardwareInfo.RefreshMemoryList();
                hardwareInfo.RefreshVideoControllerList();
                StringBuilder hardwareIdBuilder = new();
                hardwareIdBuilder.Append($"{hardwareInfo.OperatingSystem.Name}-");
                uint coreCount = 0;
                uint processorCount = 0;
                foreach (CPU cpu in hardwareInfo.CpuList)
                {
                    coreCount += cpu.NumberOfCores;
                    processorCount += cpu.NumberOfLogicalProcessors;
                    hardwareIdBuilder.Append($"{cpu.Name.Trim()}+");
                }
                hardwareIdBuilder.Remove(hardwareIdBuilder.Length - 1, 1);
                hardwareIdBuilder.Append($"{coreCount}C-{processorCount}P-");
                uint memoryCount = 0;
                ulong totalCapacity = 0;
                foreach (Memory memory in hardwareInfo.MemoryList)
                {
                    memoryCount++;
                    totalCapacity += memory.Capacity;
                }
                int reductionNum = 0;
                while (totalCapacity > 1024)
                {
                    reductionNum++;
                    totalCapacity /= 1024;
                }
                string byteType = reductionNum switch
                {
                    0 => "B",
                    1 => "KB",
                    2 => "MB",
                    3 => "GB",
                    4 => "TB",
                    _ => "??",
                };
                hardwareIdBuilder.Append($"{memoryCount}@{totalCapacity}{byteType}-");
                foreach (VideoController video in hardwareInfo.VideoControllerList)
                {
                    hardwareIdBuilder.Append($"{video.Name}+");
                }
                hardwareIdBuilder.Remove(hardwareIdBuilder.Length - 1, 1);
                hardwareIdBuilder.Replace(' ', '_');
                string hwId = hardwareIdBuilder.ToString();
                Log.D("Helpers.HardwareChecker", $"Unique Identifier: '{hwId}'");
                AppSetting hardwareSetting = database.GetAppSetting(Constants.Settings.HARDWARE_IDENTIFIER)!;
                if (!hardwareSetting.Value.Equals(hwId, StringComparison.OrdinalIgnoreCase))
                {
                    Log.D("Helpers.HardwareChecker", "Hardware identifier appears to have changed.");
                    Application.Current!.Dispatcher.Invoke(delegate
                    {
                        DialogBox.AsyncShow(
                            "We've detected that our database file may have been transferred from a different computer. Would you like to change the program's unique identifier to ensure there are no conflicts between devices?",
                            "Yes",
                            "No",
                            () =>
                            {
                                string randomMod = Constants.Settings.AlphaNum().Replace(Guid.NewGuid().ToString("N"), "").ToUpper()[0..3];
                                database.SetAppSetting(Constants.Settings.PROGRAM_UNIQUE_MODIFIER, randomMod);
                            }
                        );
                    });
                }
                database.SetAppSetting(Constants.Settings.HARDWARE_IDENTIFIER, hwId);
            }
            catch (Exception ex)
            {
                Log.E("Helpers.HardwareChecker", $"Error getting hardware information. {ex.Message}");
            }
        }
    }
}

