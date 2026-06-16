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
