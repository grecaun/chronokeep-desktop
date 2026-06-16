using Avalonia.Controls;

namespace Chronokeep.UI.Parts;

public partial class LogPart : UserControl
{
    public int Index { get; }

    public LogPart(string s, int ix, string[] humanFields, int selectedIx)
    {
        InitializeComponent();
        Index = ix;
        HeaderLabel.Text = s;
        foreach (string field in humanFields)
        {
            HeaderBox.Items.Add(field);
        }
        HeaderBox.SelectedIndex = selectedIx;
    }
}