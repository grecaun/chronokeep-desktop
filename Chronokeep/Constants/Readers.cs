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
