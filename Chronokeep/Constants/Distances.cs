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

