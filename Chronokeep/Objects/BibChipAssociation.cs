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
