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
    public static class ApiConstants
    {
        public const string CHRONOKEEP_RESULTS         = "CHRONOKEEP_V1";
        public const string CHRONOKEEP_RESULTS_SELF    = "CHRONOKEEP_V1_SELF";
        public const string CHRONOKEEP_RESULTS_WEB_URL = "https://www.chronokeep.com/";

        public const string CHRONOKEEP_REMOTE          = "CHRONOKEEP_REMOTE_V1";
        public const string CHRONOKEEP_REMOTE_SELF     = "CHRONOKEEP_REMOTE_V1_SELF";

        public const int    NULL_ID         = -1;
        public const string NULL_EVENT_ID   = "";

        public const string CHRONOKEEP_EVENT_TYPE_DISTANCE          = "distance";
        public const string CHRONOKEEP_EVENT_TYPE_TIME              = "time";
        public const string CHRONOKEEP_EVENT_TYPE_BACKYARD_ULTRA    = "backyardultra";
        public const string CHRONOKEEP_EVENT_TYPE_UNKNOWN           = "unknown";

        public static readonly Dictionary<string, string> API_TYPE_NAMES = new()
        {
            { CHRONOKEEP_RESULTS,       "Results" },
            { CHRONOKEEP_RESULTS_SELF,  "Results (SH)" },
            { CHRONOKEEP_REMOTE,        "Remote" },
            { CHRONOKEEP_REMOTE_SELF,   "Remote (SH)" }
        };

        public static readonly Dictionary<string, bool> API_SELF_HOSTED = new()
        {
            { CHRONOKEEP_RESULTS,       false },
            { CHRONOKEEP_RESULTS_SELF,  true },
            { CHRONOKEEP_REMOTE,        false },
            { CHRONOKEEP_REMOTE_SELF,   true }
        };

        public static readonly Dictionary<string, string> API_URL = new()
        {
            { CHRONOKEEP_RESULTS,       "https://api.chronokeep.com/" },
            { CHRONOKEEP_REMOTE,        "https://remote.chronokeep.com/" },
            { CHRONOKEEP_RESULTS_SELF,  "" },
            { CHRONOKEEP_REMOTE_SELF,   "" }
        };

        public static readonly Dictionary<string, bool> API_RESULTS = new()
        {
            { CHRONOKEEP_RESULTS,       true },
            { CHRONOKEEP_RESULTS_SELF,  true },
            { CHRONOKEEP_REMOTE,        false },
            { CHRONOKEEP_REMOTE_SELF,   false }
        };

        public static readonly Dictionary<string, bool> API_REMOTE = new()
        {
            { CHRONOKEEP_RESULTS,       false },
            { CHRONOKEEP_RESULTS_SELF,  false },
            { CHRONOKEEP_REMOTE,        true },
            { CHRONOKEEP_REMOTE_SELF,   true }
        };
    }
}
