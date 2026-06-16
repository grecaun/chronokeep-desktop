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