using Avalonia.Controls;
using Chronokeep.UI.Import;

namespace Chronokeep.UI.Parts;

public partial class HeaderPart : UserControl
{
    public int Index { get; }

    public HeaderPart(string s, int ix)
    {
        InitializeComponent();
        Index = ix;
        HeaderLabel.Text = s;
        foreach (string field in ImportFileWindow.HUMAN_FIELDS)
        {
            HeaderBox.Items.Add(field);
        }
        HeaderBox.SelectedIndex = ImportFileWindow.GetHeaderBoxIndex(s.Trim());
    }
}