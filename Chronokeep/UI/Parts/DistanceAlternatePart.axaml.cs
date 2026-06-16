using Avalonia.Controls;
using Chronokeep.Objects;
using System.Collections.Generic;

namespace Chronokeep.UI.Parts;

public partial class DistanceAlternatePart : UserControl
{
    public DistanceAlternatePart(string name, List<Distance> iDistances)
    {
        InitializeComponent();
        DistanceName.Text = name;
        Distances.Items.Add(new ComboBoxItem()
        {
            Content = "Auto",
            Tag = "-1"
        });
        foreach (Distance d in iDistances)
        {
            Distances.Items.Add(new ComboBoxItem()
            {
                Content = d.Name,
                Tag = d.Identifier.ToString()
            });
        }
        Distances.SelectedIndex = 0;
    }

    public string NameFromFile()
    {
        return DistanceName.Text!.Trim();
    }

    public int DistanceId()
    {
        if (int.TryParse((string)((ComboBoxItem)Distances.SelectedItem!).Tag!, out int output))
        {
            return output;
        }
        return -1;
    }
}