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
