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

using System.Collections.Generic;

namespace Chronokeep.Objects.ChronokeepPortal
{
    public class PortalSettingsHolder
    {
        public enum ChipTypeEnum
        {
            DEC,
            HEX
        }

        public enum VoiceType
        {
            EMILY,
            MICHAEL,
            CUSTOM
        }

        public enum ChangeType
        {
            SETTINGS,
            READERS,
            APIS,
            ANTENNAS
        }

        public class ReaderAntennas
        {
            public string ReaderName { get; init; } = "";
            public int[] Antennas { get; init; } = [];
        }

        public string Name { get; set; } = "";
        public int ReadWindow { get; set; }
        public ChipTypeEnum ChipType { get; set; } = ChipTypeEnum.DEC;
        public bool PlaySound { get; set; } = false;
        public double Volume { get; set; } = 0.0;
        public List<PortalReader> Readers { get; set; } = [];
        public List<PortalApi> ApIs { get; set; } = [];
        public PortalStatus AutoUpload { get; set; } = PortalStatus.NOTSET;
        public VoiceType Voice { get; set; } = VoiceType.EMILY;
        public ReaderAntennas Antennas { get; set; } = new();
        public HashSet<ChangeType> Changes { get; set; } = [];
        public string PortalVersion { get; set; } = "";
        public int UploadInterval { get; set; }
        public int BeepInterval { get; set; }
        public string NtfyUrl { get; set; } = "";
        public string NtfyTopic { get; set; } = "";
        public string NtfyUser { get; set; } = "";
        public string NtfyPass { get; set; } = "";
        public bool EnableNtfy { get; set; } = false;
        public string ScreenType { get; set; } = "";
    }
}

