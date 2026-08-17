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

using Avalonia.Controls;
using Chronokeep.Objects;

namespace Chronokeep.UI.Parts;

public partial class MultipleEntryPart : UserControl
{
    public Participant Part { get; set; }

    public MultipleEntryPart(Participant person, Event theEvent)
    {
        InitializeComponent();
        Part = person;
        Existing.Text = (person.Identifier == Constants.Timing.PARTICIPANT_DUMMYIDENTIFIER ? "" : "X");
        Bib.Text = person.Bib;
        Distance.Text = person.Distance;
        PartName.Text = $"{person.FirstName} {person.LastName}";
        Sex.Text = person.Gender;
        Age.Text = person.Age(theEvent.Date);
    }
}
