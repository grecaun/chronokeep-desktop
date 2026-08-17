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
    public class Segment(
        int id,
        int e,
        int d,
        int l,
        int occ,
        double dseg,
        double dcum,
        int dunit,
        string n,
        string gps,
        string ml)
        : IEquatable<Segment>, IComparable<Segment>
    {
        public Segment(Segment seg) : this(
            -1,
            seg.EventId,
            seg.DistanceId,
            seg.LocationId,
            seg.Occurrence,
            seg.SegmentDistance,
            seg.CumulativeDistance,
            seg.DistanceUnit,
            seg.Name,
            seg.Gps,
            seg.MapLink) { }

        public Segment(
            int e,
            int d,
            int l,
            int occ,
            double dseg,
            double dcum,
            int dunit,
            string n,
            string gps,
            string ml
            ) : this(-1, e, d, l, occ, dseg, dcum, dunit, n, gps, ml) { }

        public string Name { get; set; } = n;
        public int DistanceUnit { get; set; } = dunit;
        public double SegmentDistance { get; private set; } = dseg;
        public double CumulativeDistance { get; set; } = dcum;
        public int EventId { get; set; } = e;
        public int DistanceId { get; set; } = d;
        public int LocationId { get; set; } = l;
        public int Occurrence { get; set; } = occ;
        public int Identifier { get; set; } = id;
        public string Gps { get; set; } = gps;
        public string MapLink { get; set; } = ml;

        public int CompareTo(Segment? other)
        {
            if (other == null) return 1;
            if (EventId != other.EventId)
            {
                return EventId.CompareTo(other.EventId);
            }
            if (other.DistanceId != DistanceId)
            {
                return DistanceId.CompareTo(other.DistanceId);
            }
            if (Math.Abs(CumulativeDistance - other.CumulativeDistance) > 0.001)
            {
                return CumulativeDistance.CompareTo(other.CumulativeDistance);
            }
            return LocationId != other.LocationId ? LocationId.CompareTo(other.LocationId) : Occurrence.CompareTo(other.Occurrence);
        }

        public bool Equals(Segment? other)
        {
            if (other == null) return false;
            return EventId == other.EventId &&
                DistanceId == other.DistanceId &&
                LocationId == other.LocationId &&
                Occurrence == other.Occurrence;
        }

        public void CopyFrom(Segment other)
        {
            EventId = other.EventId;
            DistanceId = other.DistanceId;
            LocationId = other.LocationId;
            Occurrence = other.Occurrence;
            Name = other.Name;
            SegmentDistance = other.SegmentDistance;
            CumulativeDistance = other.CumulativeDistance;
            DistanceUnit = other.DistanceUnit;
            Gps = other.Gps;
            MapLink = other.MapLink;
        }
    }
}

