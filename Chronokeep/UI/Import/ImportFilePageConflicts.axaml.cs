using Avalonia.Controls;
using Chronokeep.Objects;
using Chronokeep.UI.Parts;
using System.Collections.Generic;
using System.Linq;

namespace Chronokeep.UI.Import;

public partial class ImportFilePageConflicts : UserControl
{
    public ImportFilePageConflicts(List<Participant> conflicts, Event theEvent)
    {
        InitializeComponent();
        foreach (Participant part in conflicts)
        {
            MultiplesListBox.Items.Add(new MultipleEntryPart(part, theEvent));
        }
    }

    public List<Participant> GetParticipantsToRemove()
    {
        List<Participant> output = [];
        output.AddRange(from item in MultiplesListBox.Items.Cast<MultipleEntryPart>() where item.Keep.IsChecked == false select item.Part);
        return output;
    }
}