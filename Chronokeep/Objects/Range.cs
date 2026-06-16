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
