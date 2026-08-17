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
    public class BibChipAssociation : IEquatable<BibChipAssociation>, IComparable<BibChipAssociation>
    {
        public int EventId { get; init; } = -1;
        public string Bib { get; set; } = Constants.Timing.CHIPREAD_DUMMYBIB;
        public string Chip { get; set; } = Constants.Timing.CHIPREAD_DUMMYCHIP;

        public int CompareTo(BibChipAssociation? other)
        {
            if (other == null) return 1;
            else if (EventId == other.EventId)
            {
                if (int.TryParse(Bib, out int bibOne) && int.TryParse(other.Bib, out int bibTwo))
                {
                    return bibOne.CompareTo(bibTwo);
                }
                return string.Compare(Bib, other.Bib, StringComparison.Ordinal);
            }
            return EventId.CompareTo(other.EventId);
        }

        public bool Equals(BibChipAssociation? other)
        {
            if (other == null) return false;
            return EventId == other.EventId && Bib.Equals(other.Bib, StringComparison.OrdinalIgnoreCase);
        }

        public void TrimFields()
        {
            Bib = Bib.Trim();
            Chip = Chip.Trim();
        }
    }
}

