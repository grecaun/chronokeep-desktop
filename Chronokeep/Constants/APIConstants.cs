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

        /// <summary>Runsignup v2 credentials. The key goes in the token and the secret in the URL column.</summary>
        public const string RUNSIGNUP                  = "RUNSIGNUP_V2";
        public const string RUNSIGNUP_BASE_URL         = "https://api.runsignup.com/";

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
            { CHRONOKEEP_REMOTE_SELF,   "Remote (SH)" },
            { RUNSIGNUP,                "Runsignup" }
        };

        public static readonly Dictionary<string, bool> API_SELF_HOSTED = new()
        {
            { CHRONOKEEP_RESULTS,       false },
            { CHRONOKEEP_RESULTS_SELF,  true },
            { CHRONOKEEP_REMOTE,        false },
            { CHRONOKEEP_REMOTE_SELF,   true },
            { RUNSIGNUP,                false }
        };

        public static readonly Dictionary<string, string> API_URL = new()
        {
            { CHRONOKEEP_RESULTS,       "https://api.chronokeep.com/" },
            { CHRONOKEEP_REMOTE,        "https://remote.chronokeep.com/" },
            { CHRONOKEEP_RESULTS_SELF,  "" },
            { CHRONOKEEP_REMOTE_SELF,   "" },
            { RUNSIGNUP,                "" }
        };

        public static readonly Dictionary<string, bool> API_RESULTS = new()
        {
            { CHRONOKEEP_RESULTS,       true },
            { CHRONOKEEP_RESULTS_SELF,  true },
            { CHRONOKEEP_REMOTE,        false },
            { CHRONOKEEP_REMOTE_SELF,   false },
            { RUNSIGNUP,                false }
        };

        public static readonly Dictionary<string, bool> API_REMOTE = new()
        {
            { CHRONOKEEP_RESULTS,       false },
            { CHRONOKEEP_RESULTS_SELF,  false },
            { CHRONOKEEP_REMOTE,        true },
            { CHRONOKEEP_REMOTE_SELF,   true },
            { RUNSIGNUP,                false }
        };

        /// <summary>APIs that participants are downloaded from (registration systems).</summary>
        public static readonly Dictionary<string, bool> API_REGISTRATION = new()
        {
            { CHRONOKEEP_RESULTS,       false },
            { CHRONOKEEP_RESULTS_SELF,  false },
            { CHRONOKEEP_REMOTE,        false },
            { CHRONOKEEP_REMOTE_SELF,   false },
            { RUNSIGNUP,                true }
        };
    }
}
