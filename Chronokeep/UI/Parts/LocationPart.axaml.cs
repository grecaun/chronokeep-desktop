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
using Chronokeep.UI.Util;
using System;
using System.Text.RegularExpressions;

namespace Chronokeep.UI.Parts;

public partial class LocationPart : UserControl
{
    private const string TimeFormat = "{0:D2}:{1:D2}:{2:D2}";
    private readonly LocationsPage page;
    public readonly TimingLocation MyLocation;

    [GeneratedRegex("[^0-9]+")]
    private static partial Regex AllowedChars();

    public LocationPart(LocationsPage page, TimingLocation location, Event theEvent)
    {
        InitializeComponent();
        // Filter typing before the box adds the text; a XAML TextInput handler runs too late to block it.
        MaxOccurrences.FilterTyping(NumberValidation);
        this.page = page;
        MyLocation = location;
        LocationName.Text = MyLocation.Name;
        MaxOccurrences.Text = MyLocation.MaxOccurrences.ToString();
        string labelLabel = MyLocation.Identifier == Constants.Timing.LOCATION_START ? "Start Window" : "Ignore Within";
        if (Constants.Timing.EVENT_TYPE_TIME == theEvent.EventType
            || (Constants.Timing.LOCATION_START == MyLocation.Identifier && theEvent.CommonStartFinish))
        {
            OccPanel.IsVisible = false;
            OccPanel.Height = 0;
            OccPanel.Width = 0;
        }
        IgnoreWithinLabel.Text = labelLabel;
        IgnoreWithinLabel.Width = 120;
        // New locations start at -1 (not set), which the time box can't show; it means the same as zero.
        int ignore = Math.Max(0, MyLocation.IgnoreWithin);
        string ignStr = string.Format(TimeFormat, ignore / 3600, (ignore % 3600) / 60, ignore % 60);
        IgnoreWithin.Text = ignStr;
        if (Constants.Timing.EVENT_TYPE_TIME != theEvent.EventType
            && !(Constants.Timing.LOCATION_START == MyLocation.Identifier && theEvent.CommonStartFinish))
        {
            Grid.SetColumnSpan(IgnorePanel, 1);
        }
        else
        {
            Grid.SetColumnSpan(IgnorePanel, 2);
        }

        if (MyLocation.Identifier != Constants.Timing.LOCATION_FINISH
            && MyLocation.Identifier != Constants.Timing.LOCATION_START) return;
        LocationName.IsEnabled = false;
        Remove.IsEnabled = false;
        Remove.IsVisible = false;
    }

    /// <summary>
    /// Copies the form into the location and says what changed. Changed covers any field; TimingChanged
    /// only the max occurrences and ignore window, which change how reads are counted. A rename alone
    /// doesn't need results recalculated. Invalid values change nothing.
    /// </summary>
    public (bool Changed, bool TimingChanged) UpdateLocation()
    {
        Log.D("UI.MainPages.LocationsPage", "Updating location.");
        try
        {
            string name = LocationName.Text ?? "";
            int maxOccurrences = Convert.ToInt32(MaxOccurrences.Text);
            string[] parts = IgnoreWithin.Text!.Replace('_', '0').Split(':');
            int hours = Convert.ToInt32(parts[0]), minutes = Convert.ToInt32(parts[1]), seconds = Convert.ToInt32(parts[2]);
            int ignoreWithin = (hours * 3600) + (minutes * 60) + seconds;
            bool timingChanged = MyLocation.MaxOccurrences != maxOccurrences || Math.Max(0, MyLocation.IgnoreWithin) != ignoreWithin;
            bool changed = timingChanged || MyLocation.Name != name;
            MyLocation.Name = name;
            MyLocation.MaxOccurrences = maxOccurrences;
            MyLocation.IgnoreWithin = ignoreWithin;
            return (changed, timingChanged);
        }
        catch
        {
            DialogBox.AsyncShow("Error with values given.");
            return (false, false);
        }
    }

    private void SelectAll(object? sender, FocusChangedEventArgs e)
    {
        TextBox src = (TextBox)e.Source!;
        src.SelectAll();
    }

    private void NumberValidation(object? sender, TextInputEventArgs e)
    {
        e.Handled = AllowedChars().IsMatch(e.Text!);
    }

    private void Remove_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.LocationsPage", "Removing an item.");
        this.page.RemoveLocation(MyLocation);
    }
}
