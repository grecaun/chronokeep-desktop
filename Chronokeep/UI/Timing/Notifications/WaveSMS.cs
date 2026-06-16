namespace Chronokeep.UI.Timing.Notifications
{
    internal class WaveSms
    {
        public int Wave { get; init; }
        public string WaveName => $"Wave {Wave}";
        public bool SmsEnabled { get; set; }
    }
}
