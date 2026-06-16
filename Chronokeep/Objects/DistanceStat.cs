namespace Chronokeep.Objects
{
    public class DistanceStat
    {
        public string DistanceName { get; set; } = "";
        public int DistanceId { get; init; }
        public int Total => Dnf + Dns + Finished + Active;
        public int Dnf { get; set; }
        public int Dns { get; set; }
        public int Finished { get; set; }
        public int Active { get; set; }
    }
}
