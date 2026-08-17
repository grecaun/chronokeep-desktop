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
using Chronokeep.Helpers;
using Chronokeep.IO;
using Chronokeep.Objects;
using System;
using System.Collections.Generic;

namespace Chronokeep.UI.Timing.Import;

public partial class ImportLogPage1 : UserControl
{
    private readonly ImportLogWindow parent;

    public ImportLogPage1(ImportLogWindow parent, LogImporter importer, List<TimingLocation> locations)
    {
        InitializeComponent();
        this.parent = parent;
        TypeHolder.Items.Clear();
        ComboBoxItem? selected = null, custom = null;
        foreach (LogImporter.Type type in Enum.GetValues<LogImporter.Type>())
        {
            ComboBoxItem current = new()
            {
                Content = type.ToString(),
                Tag = type.ToString()
            };
            TypeHolder.Items.Add(current);
            if (type == importer.Kind)
            {
                selected = current;
            }
            if (type == LogImporter.Type.CUSTOM)
            {
                custom = current;
            }
        }
        selected ??= custom;
        TypeHolder.SelectedItem = selected;
        UpdateLocations(locations);
    }

    public void UpdateLocations(List<TimingLocation> locations)
    {
        Log.D("UI.Timing.ImportLog", "Updating locations in import log page 1.");
        int locationId = -12;
        if (LocationHolder.SelectedItem != null)
        {
            locationId = Convert.ToInt32(((ComboBoxItem)LocationHolder.SelectedItem).Tag);
        }
        LocationHolder.Items.Clear();
        ComboBoxItem? selected = null;
        foreach (TimingLocation loc in locations)
        {
            ComboBoxItem current = new ComboBoxItem()
            {
                Content = loc.Name,
                Tag = loc.Identifier.ToString()
            };
            LocationHolder.Items.Add(current);
            if (locationId == loc.Identifier)
            {
                selected = current;
            }
        }
        if (selected != null)
        {
            LocationHolder.SelectedItem = selected;
        }
        else
        {
            LocationHolder.SelectedIndex = 0;
        }
    }

    private void TypeHolder_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Log.D("UI.Timing.ImportLog", "Type changed.");
        NextButton.Content = ((ComboBoxItem)TypeHolder.SelectedItem!).Tag!.ToString() == nameof(LogImporter.Type.CUSTOM) ? "Next" : "Import";
    }

    private void NextButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ImportLog", "Next Button Clicked.");
        int locationId = Convert.ToInt32(((ComboBoxItem)LocationHolder.SelectedItem!).Tag);
        Log.D("UI.Timing.ImportLog", $"Location ID is: {locationId} name of: {((ComboBoxItem)LocationHolder.SelectedItem).Content}");
        if (((ComboBoxItem)TypeHolder.SelectedItem!).Tag!.ToString() == nameof(LogImporter.Type.CUSTOM))
        {
            parent.Next(locationId);
            return;
        }
        foreach (LogImporter.Type type in Enum.GetValues<LogImporter.Type>())
        {
            if (((ComboBoxItem)TypeHolder.SelectedItem).Tag!.ToString() != type.ToString()) continue;
            parent.Import(type, locationId, 0, 0);
            return;
        }
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ImportLog", "Cancel Button Clicked.");
        parent.Cancel();
    }
}
