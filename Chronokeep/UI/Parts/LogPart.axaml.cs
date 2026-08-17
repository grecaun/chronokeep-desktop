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
