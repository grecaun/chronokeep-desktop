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

using System;

namespace Chronokeep.Objects
{
    internal class Range : IComparable<Range>, IEquatable<Range>
    {
        public int StartBib { get; init; }
        public int EndBib { get; init; }
        public int StartChip { get; init; }
        public int EndChip { get; init; }

        public int CompareTo(Range? other)
        {
            if (other == null) return -1;
            return StartBib.CompareTo(other.StartBib);
        }

        public bool Equals(Range? other)
        {
            if (other == null) return false;
            return StartBib.Equals(other.StartBib) && EndBib.Equals(other.EndBib) && StartChip.Equals(other.StartChip) && EndChip.Equals(other.EndChip);
        }

        public bool IsValid()
        {
            return StartBib <= EndBib;
        }

        public bool Violates(Range other)
        {
            return (other.StartBib >= StartBib && other.StartBib <= EndBib) || (other.EndBib >= StartBib && other.EndBib <= EndBib)
                || (StartBib >= other.StartBib && StartBib <= other.EndBib) || (EndBib >= other.StartBib && EndBib <= other.EndBib)
                || (other.StartChip >= StartChip && other.StartChip <= EndChip) || (other.EndChip >= StartChip && other.EndChip <= EndChip)
                || (StartChip >= other.StartChip && StartChip <= other.EndChip) || (EndChip >= other.StartChip && EndChip <= other.EndChip);
        }
    }
}

