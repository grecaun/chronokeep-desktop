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

using System.Text.Json.Serialization;

namespace Chronokeep.Objects.ChronokeepPortal
{
    public class PortalSetting
    {
        public const string SETTING_PORTAL_NAME = "SETTING_PORTAL_NAME";
        public const string SETTING_READ_WINDOW = "SETTING_READ_WINDOW";
        public const string SETTING_CHIP_TYPE = "SETTING_CHIP_TYPE";
        public const string SETTING_PLAY_SOUND = "SETTING_PLAY_SOUND";
        public const string SETTING_VOLUME = "SETTING_VOLUME";
        public const string SETTING_VOICE = "SETTING_VOICE";
        public const string SETTING_UPLOAD_INTERVAL = "SETTING_UPLOAD_INTERVAL";
        public const string SETTING_NTFY_URL = "SETTING_NTFY_URL";
        public const string SETTING_NTFY_USER = "SETTING_NTFY_USER";
        public const string SETTING_NTFY_PASS = "SETTING_NTFY_PASS";
        public const string SETTING_NTFY_TOPIC = "SETTING_NTFY_TOPIC";
        public const string SETTING_ENABLE_NTFY = "SETTING_ENABLE_NTFY";
        public const string SETTING_SCREEN_TYPE = "SETTING_SCREEN_TYPE";
        public const string SETTING_BEEP_INTERVAL = "SETTING_BEEP_IGNORE";

        public const string TYPE_CHIP_DEC = "DEC";
        public const string TYPE_CHIP_HEX = "HEX";

        public const string VOICE_EMILY = "emily";
        public const string VOICE_MICHAEL = "michael";
        public const string VOICE_CUSTOM = "custom";

        [JsonPropertyName("name")]
        public string Name { get; init; } = "";
        [JsonPropertyName("value")]
        public string Value { get; init; } = "";
    }
}

