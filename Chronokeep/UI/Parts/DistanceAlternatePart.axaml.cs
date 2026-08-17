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
