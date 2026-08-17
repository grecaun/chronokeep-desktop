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
using Avalonia.Input;
using Avalonia.Interactivity;
using Chronokeep.Helpers;
using Chronokeep.Objects;
using Chronokeep.UI.MainPages;
using System.Text.RegularExpressions;

namespace Chronokeep.UI.Parts;

public partial class AgeGroupPart : UserControl
{
    private readonly AgeGroupsPage page;
    private AgeGroup MyGroup { get; }

    [GeneratedRegex("[^0-9]+")]
    private static partial Regex AllowedChars();

    public AgeGroupPart(AgeGroupsPage page, AgeGroup group)
    {
        InitializeComponent();
        this.page = page;
        MyGroup = group;
        StartAge.Text = group.StartAge.ToString();
        EndAge.Text = group.EndAge.ToString();
    }

    public AgeGroup GetAgeGroup()
    {
        if (int.TryParse(StartAge.Text, out int start))
        {
            MyGroup.StartAge = start;
        }
        if (int.TryParse(EndAge.Text, out int end))
        {
            MyGroup.EndAge = end;
        }
        return MyGroup;
    }

    private void SelectAll(object? sender, FocusChangedEventArgs e)
    {
        TextBox? src = (TextBox?)e.Source;
        src?.SelectAll();
    }

    private void NumberValidation(object? sender, TextInputEventArgs e)
    {
        if (e.Text != null)
        {
            e.Handled = AllowedChars().IsMatch(e.Text);
        }
    }

    private void Remove_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.AgeGroupsPage", "Removing.");
        page.RemoveAgeGroup(this);
    }
}
