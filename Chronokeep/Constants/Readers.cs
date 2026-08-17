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

namespace Chronokeep.Constants
{
    internal static class Readers
    {
        public const string SYSTEM_RFID = "RFID";
        public const string SYSTEM_IPICO = "IPICO";
        public const string SYSTEM_IPICO_LITE = "IPICO_LITE";
        public const string SYSTEM_CHRONOKEEP_PORTAL = "CHRONOKEEP_PORTAL";

        public const string DEFAULT_TIMING_SYSTEM = SYSTEM_CHRONOKEEP_PORTAL;

        public const int RFID_DEFAULT_PORT = 23;
        public const int IPICO_DEFAULT_PORT = 10000;
        public const int IPICO_CONTROL_PORT = 9999;

        public const byte CHRONOKEEP_ANTENNA_STATUS_NONE = 0;
        public const byte CHRONOKEEP_ANTENNA_STATUS_DISCONNECTED = 1;
        public const byte CHRONOKEEP_ANTENNA_STATUS_CONNECTED = 2;

        public const string CHRONOKEEP_SCREEN_ADAFRUIT = "ADAFRUIT";
        public const string CHRONOKEEP_SCREEN_PCF8574T = "PCF8574T";

        public const int TIMEOUT = 3000;

        public static readonly Dictionary<string, string> SYSTEM_NAMES = new()
        {
            { SYSTEM_RFID, "RFID Timing Systems" },
            { SYSTEM_IPICO, "Ipico Elite Reader" },
            { SYSTEM_IPICO_LITE, "Ipico Lite Reader" },
            { SYSTEM_CHRONOKEEP_PORTAL, "Chronokeep Portal"},
        };

    }
}

