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
