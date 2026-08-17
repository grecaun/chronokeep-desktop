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

namespace Chronokeep.Constants
{
    internal static class Network
    {
        public const string DEFAULT_CHRONOKEEP_SERVER_NAME = "Chronokeep Registration";
        public const string CHRONOKEEP_ZCONF_MULTICAST_IP  = "224.0.44.88";
        public const string CHRONOKEEP_ZCONF_CONNECT_MSG   = "[DISCOVER_CHRONO_SERVER_REQUEST]";
        public const int    CHRONOKEEP_ZCONF_PORT          = 4488;

        public const string CHRONOKEEP_REGISTRATION_TYPE = "CHRONOKEEP_WINDOWS";
        public const int    CHRONOKEEP_REGISTRATION_VERS = 1;
    }
}

