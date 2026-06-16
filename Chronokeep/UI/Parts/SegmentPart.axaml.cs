using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Chronokeep.Helpers;
using Chronokeep.Objects;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Chronokeep.UI.Parts;

public partial class SegmentPart : UserControl
{
    private readonly SegmentsPage page;
    public readonly Segment MySegment;
    private readonly Dictionary<string, int> locationDictionary;
    public readonly Event TheEvent;

    [GeneratedRegex("[^0-9.]+")]
    private static partial Regex AllowedChars();

    public SegmentPart(Event theEvent, SegmentsPage page, Segment segment, List<TimingLocation> locations)
    {
        InitializeComponent();
        this.page = page;
        TheEvent = theEvent;
        MySegment = segment;
        locationDictionary = [];

        ComboBoxItem? selected = null, current;
        foreach (TimingLocation loc in locations)
        {
            current = new ComboBoxItem()
            {
                Content = loc.Name,
                Tag = loc.Identifier.ToString()
            };
            Location.Items.Add(current);
            if (MySegment.LocationId == loc.Identifier)
            {
                selected = current;
            }
            locationDictionary[loc.Identifier.ToString()] = loc.MaxOccurrences;
        }
        if (selected != null)
        {
            Location.SelectedItem = selected;
        }
        SegName.Text = MySegment.Name;
        // Occurrence
        Occurrence.Items.Clear();
        if (Constants.Timing.EVENT_TYPE_DISTANCE == theEvent.EventType)
        {
            if (Location.SelectedItem == null || !locationDictionary.TryGetValue((string)((ComboBoxItem)Location.SelectedItem).Tag!, out int maxOccurrences))
            {
                maxOccurrences = 1;
            }
            selected = null;
            int start = 1;
            if ((theEvent.CommonStartFinish && MySegment.LocationId == Constants.Timing.LOCATION_FINISH)
                || MySegment.LocationId == Constants.Timing.LOCATION_START)
            {
                start = 0;
            }
            for (int i = start; i <= maxOccurrences; i++)
            {
                current = new ComboBoxItem
                {
                    Content = i.ToString(),
                    Tag = i.ToString()
                };
                if (i == MySegment.Occurrence)
                {
                    selected = current;
                }
                Occurrence.Items.Add(current);
            }
            if (selected != null)
            {
                Occurrence.SelectedItem = selected;
            }
            else
            {
                Occurrence.SelectedIndex = 0;
            }
        }
        CumDistance.Text = MySegment.CumulativeDistance.ToString(CultureInfo.InvariantCulture);
        DistanceUnit.SelectedIndex = MySegment.DistanceUnit switch
        {
            Constants.Distances.KILOMETERS => 1,
            Constants.Distances.METERS => 2,
            Constants.Distances.YARDS => 3,
            Constants.Distances.FEET => 4,
            _ => 0
        };
        Gps.Text = MySegment.Gps;
        MapLink.Text = MySegment.MapLink;
    }

    public void UpdateSegment()
    {
        Log.D("UI.MainPages.SegmentsPage", "Segments - Updating segment.");
        try
        {
            MySegment.Name = SegName.Text!;
            try
            {
                MySegment.LocationId = Convert.ToInt32(((ComboBoxItem)Location.SelectedItem!).Tag!);
            }
            catch
            {
                MySegment.LocationId = Constants.Timing.LOCATION_DUMMY;
            }
            MySegment.CumulativeDistance = Convert.ToDouble(CumDistance.Text);
            MySegment.DistanceUnit = DistanceUnit.SelectedIndex switch
            {
                1 => Constants.Distances.KILOMETERS,
                2 => Constants.Distances.METERS,
                3 => Constants.Distances.YARDS,
                4 => Constants.Distances.FEET,
                _ => Constants.Distances.MILES,
            };
            if (Occurrence is { SelectedItem: not null }) MySegment.Occurrence = Convert.ToInt32(((ComboBoxItem)Occurrence.SelectedItem).Tag!);
            else MySegment.Occurrence = -1;
            MySegment.Gps = Gps.Text!;
            MySegment.MapLink = MapLink.Text!;
        }
        catch
        {
            DialogBox.Show("Error with values given.");
        }
    }

    private void SelectAll(object? sender, FocusChangedEventArgs e)
    {
        TextBox src = (TextBox)e.Source!;
        src.SelectAll();
    }

    private void Location_Changed(object? sender, SelectionChangedEventArgs e)
    {
        Occurrence.Items.Clear();
        if (Location.SelectedItem == null || !locationDictionary.TryGetValue((string)((ComboBoxItem)Location.SelectedItem).Tag!, out int maxOccurrences))
        {
            maxOccurrences = 1;
        }
        int start = 1;
        if ((TheEvent.CommonStartFinish && MySegment.LocationId == Constants.Timing.LOCATION_FINISH)
            || MySegment.LocationId == Constants.Timing.LOCATION_START)
        {
            start = 0;
        }
        for (int i = start; i <= maxOccurrences; i++)
        {
            Occurrence.Items.Add(new ComboBoxItem()
            {
                Content = i.ToString(),
                Tag = i.ToString()
            });
        }
        Occurrence.SelectedIndex = 0;
    }

    private void DoubleValidation(object? sender, TextInputEventArgs e)
    {
        e.Handled = AllowedChars().IsMatch(e.Text!);
    }

    private void Remove_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.SegmentsPage", "Removing an item.");
        page.RemoveSegment(MySegment);
    }
}