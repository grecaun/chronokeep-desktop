using Avalonia.Controls;
using System;

namespace Chronokeep.UI.Parts;

public partial class BibChipHeaderPart : UserControl
{
    public int Index { get; }
    public static readonly string[] HUMAN_FIELDS =
    [
        "",
        "Bib",
        "Chip"
    ];

    public BibChipHeaderPart(string s, int ix)
    {
        InitializeComponent();
        Index = ix;
        HeaderLabel.Text = s;
        foreach (string field in HUMAN_FIELDS)
        {
            HeaderBox.Items.Add(field);
        }
        HeaderBox.SelectedIndex = GetHeaderBoxIndex(s.Trim());
    }

    private static int GetHeaderBoxIndex(string s)
    {
        if (string.Equals(s, "bib", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        } 
        return string.Equals(s, "chip", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
    }
}