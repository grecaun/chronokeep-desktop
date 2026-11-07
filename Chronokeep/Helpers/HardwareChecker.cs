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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Chronokeep.Helpers
{
    internal class HardwareChecker(IdbInterface database)
    {
        /// <summary>
        /// Builds the identifier used to tell whether the database has moved to a different computer.
        /// </summary>
        /// <param name="operatingSystem">The operating system's name.</param>
        /// <param name="cpus">Each processor's name, core count and logical processor count.</param>
        /// <param name="memoryCapacities">The capacity of each memory module in bytes.</param>
        /// <param name="videoControllers">Each video controller's name.</param>
        /// <returns>
        /// The identifier, in the form OS-CPU+CPU{cores}C-{logical processors}P-{modules}@{total memory}{unit}-VIDEO+VIDEO,
        /// with spaces replaced by underscores.
        /// </returns>
        internal static string BuildHardwareId(
            string operatingSystem,
            IEnumerable<(string Name, uint Cores, uint LogicalProcessors)> cpus,
            IEnumerable<ulong> memoryCapacities,
            IEnumerable<string> videoControllers
            )
        {
            StringBuilder hardwareIdBuilder = new();
            hardwareIdBuilder.Append($"{operatingSystem}-");
            uint coreCount = 0;
            uint processorCount = 0;
            foreach ((string name, uint cores, uint logicalProcessors) in cpus)
            {
                coreCount += cores;
                processorCount += logicalProcessors;
                hardwareIdBuilder.Append($"{name.Trim()}+");
            }
            hardwareIdBuilder.Remove(hardwareIdBuilder.Length - 1, 1);
            hardwareIdBuilder.Append($"{coreCount}C-{processorCount}P-");
            uint memoryCount = 0;
            ulong totalCapacity = 0;
            foreach (ulong capacity in memoryCapacities)
            {
                memoryCount++;
                totalCapacity += capacity;
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
            foreach (string video in videoControllers)
            {
                hardwareIdBuilder.Append($"{video}+");
            }
            hardwareIdBuilder.Remove(hardwareIdBuilder.Length - 1, 1);
            hardwareIdBuilder.Replace(' ', '_');
            return hardwareIdBuilder.ToString();
        }

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
                string hwId = BuildHardwareId(
                    hardwareInfo.OperatingSystem.Name,
                    hardwareInfo.CpuList.Select(cpu => (cpu.Name, cpu.NumberOfCores, cpu.NumberOfLogicalProcessors)),
                    hardwareInfo.MemoryList.Select(memory => memory.Capacity),
                    hardwareInfo.VideoControllerList.Select(video => video.Name)
                    );
                Log.D("Helpers.HardwareChecker", $"Unique Identifier: '{hwId}'");
                AppSetting? hardwareSetting = database.GetAppSetting(Constants.Settings.HARDWARE_IDENTIFIER);
                // The first time this runs there's no identifier saved yet, so there's nothing to compare against.
                if (null != hardwareSetting
                    && hardwareSetting.Value.Length > 0
                    && !hardwareSetting.Value.Equals(hwId, StringComparison.OrdinalIgnoreCase))
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

