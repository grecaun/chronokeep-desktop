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

namespace Chronokeep.Objects.RFID
{
    public class RfidSettingsHolder
    {
        public int UltraId { get; set; } = -1;
        public ChipTypeEnum ChipType { get; set; } = ChipTypeEnum.UNKNOWN;
        public GatingModeEnum GatingMode { get; set; } = GatingModeEnum.UNKNOWN;
        public int GatingInterval { get; set; } = -1;
        public BeepEnum Beep { get; set; } = BeepEnum.UNKNOWN;
        public BeepVolumeEnum BeepVolume { get; set; } = BeepVolumeEnum.UNKNOWN;
        public GpsEnum SetFromGps { get; set; } = GpsEnum.UNKNOWN;
        public int TimeZone { get; set; } = -25;
        public StatusEnum Status { get; set; } = StatusEnum.UNKNOWN;

        public enum ChipTypeEnum
        {
            UNKNOWN,
            DEC,
            HEX
        }

        public enum GatingModeEnum
        {
            UNKNOWN,
            PER_READER,
            PER_BOX,
            FIRST_TIME_SEEN
        }

        public enum BeepEnum
        {
            UNKNOWN,
            ALWAYS,
            ONLY_FIRST_SEEN
        }

        public enum BeepVolumeEnum
        {
            UNKNOWN,
            OFF,
            SOFT,
            LOUD
        }

        public enum GpsEnum
        {
            UNKNOWN,
            SET,
            DONT_SET
        }

        public enum StatusEnum
        {
            UNKNOWN,
            STARTED,
            STOPPED
        }
    }
}

