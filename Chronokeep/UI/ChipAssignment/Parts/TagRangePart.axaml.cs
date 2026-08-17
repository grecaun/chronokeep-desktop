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

namespace Chronokeep.UI.ChipAssignment.Parts;

public partial class TagRangePart : UserControl
{
    private string EndBibVal => EndBib.Text!;
    private string EndChipVal => EndChip.Text!;

    private readonly ListBox parent;

    public TagRangePart(ListBox correlationBox)
    {
        InitializeComponent();
        int lastEndBib = 0, lastEndChip = 0;
        parent = correlationBox;
        if (correlationBox.Items.Count > 0)
        {
            TagRangePart? lastItem = correlationBox.Items[^1] as TagRangePart;
            try
            {
                _ = int.TryParse(lastItem!.EndBibVal, out lastEndBib);
                _ = int.TryParse(lastItem.EndChipVal, out lastEndChip);
            }
            catch
            {
                Log.D("UI.ChipAssignment.ChipTool", "Error parsing values.");
            }
        }
        StartBib.Text = $"{lastEndBib + 1}";
        EndBib.Text = $"{lastEndBib + 1}";
        StartChip.Text = $"{lastEndChip + 1}";
        EndChip.Text = $"{lastEndChip + 1}";
    }

    private void UpdateEndChip()
    {
        _ = int.TryParse(StartBib.Text, out int startBib);
        _ = int.TryParse(EndBib.Text, out int endBib);
        _ = int.TryParse(StartChip.Text, out int startChip);
        int endChip = endBib - startBib + startChip;
        EndChip.Text = endChip.ToString();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        Log.D("UI.ChipAssignment.ChipTool", "Removing an item.");
        try
        {
            parent.Items.Remove(this);
        }
        catch
        {
            Log.D("UI.ChipAssignment.ChipTool", "Error removing an item.");
        }
    }

    private void StartBib_TextChanged(object sender, TextChangedEventArgs e)
    {
        string replaceStr = StartBib.Text!.Replace(" ", "");
        if (StartBib.Text.Length != replaceStr.Length)
        {
            StartBib.Text = replaceStr;
        }
        UpdateEndChip();
    }

    private void EndBib_TextChanged(object sender, TextChangedEventArgs e)
    {
        string replaceStr = EndBib.Text!.Replace(" ", "");
        if (EndBib.Text.Length != replaceStr.Length)
        {
            EndBib.Text = replaceStr;
        }
        UpdateEndChip();
    }

    private void StartChip_TextChanged(object sender, TextChangedEventArgs e)
    {
        string replaceStr = StartChip.Text!.Replace(" ", "");
        if (StartChip.Text.Length != replaceStr.Length)
        {
            StartChip.Text = replaceStr;
        }
        _ = (int.TryParse(StartChip.Text, out int startChip));
        if (string.CompareOrdinal(StartChip.Text, startChip.ToString()) != 0)
        {
            StartChip.Text = startChip.ToString();
        }
        UpdateEndChip();
    }

    private void SelectAll(object sender, FocusChangedEventArgs e)
    {
        TextBox src = (TextBox)e.Source!;
        src.SelectAll();
    }

    private void KeyPressHandler(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case >= Key.D0 and <= Key.D9:
            case >= Key.NumPad0 and <= Key.NumPad9:
            case Key.Tab:
                break;
            default:
                e.Handled = true;
                break;
        }
    }
}
