namespace Chronokeep.Constants
{
    public static class Distances
    {
        public const int UNKNOWN = 0;
        public const int MILES = 1;
        public const int YARDS = 2;
        public const int FEET = 3;
        public const int KILOMETERS = 101;
        public const int METERS = 102;

        public static string DistanceString(int dist)
        {
            return dist switch
            {
                MILES => "Miles",
                YARDS => "Yards",
                FEET => "Feet",
                METERS => "Meters",
                KILOMETERS => "Kilometers",
                _ => "",
            };
        }
    }
}
