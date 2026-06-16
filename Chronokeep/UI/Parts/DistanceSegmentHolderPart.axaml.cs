using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Helpers;
using Chronokeep.Objects;
using Chronokeep.UI.MainPages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Chronokeep.UI.Parts;

public partial class DistanceSegmentHolderPart : UserControl
{
    private readonly Distance? distance;
    private readonly SegmentsPage page;
    public readonly List<UserControl> SegmentItems = [];

    public DistanceSegmentHolderPart(Event theEvent, SegmentsPage page, Distance? distance,
                List<Distance> distances, List<Segment> segments, List<TimingLocation> locations)
    {
        InitializeComponent();
        this.distance = distance;
        this.page = page;
        List<Distance> otherDistances1 = [.. distances];
        otherDistances1.RemoveAll(x => x.Identifier == (distance?.Identifier ?? -1));
        DistanceName.Text = distance == null ? "All Distances" : distance.Name;
        CopyFromDistance.Items.Add(new ComboBoxItem()
        {
            Content = "",
            Tag = "-1"
        });
        foreach (Distance d in otherDistances1)
        {
            CopyFromDistance.Items.Add(new ComboBoxItem()
            {
                Content = d.Name,
                Tag = d.Identifier.ToString()
            });
        }
        CopyFromDistance.SelectedIndex = 0;
        int finishOccurrences = 0;
        SegmentItems.Add(new SegmentHeaderPart(theEvent));
        //segmentHolder.Items.Add(new ASegmentHeader(theEvent));
        segments.Sort((x1, x2) => x1.CompareTo(x2));
        foreach (Segment s in segments)
        {
            SegmentPart newSeg = new(theEvent, page, s, locations);
            SegmentItems.Add(newSeg);
            //segmentHolder.Items.Add(newSeg);
            if (s.LocationId == Constants.Timing.LOCATION_FINISH || s.LocationId == Constants.Timing.LOCATION_START)
            {
                finishOccurrences = s.Occurrence > finishOccurrences ? s.Occurrence : finishOccurrences;
            }
            finishOccurrences++;
        }
    }

    private void AddClick(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.SegmentsPage", "Add segment clicked.");
        int selectedDistance = Constants.Timing.COMMON_SEGMENTS_DISTANCEID;
        if (distance != null)
        {
            selectedDistance = distance.Identifier;
        }
        _ = int.TryParse(NumAdd.Text, out int count);
        for (int i = 0; i < count; i++)
        {
            page.AddSegment(selectedDistance);
        }
    }

    private void CopyFromDistanceSelected(object? sender, SelectionChangedEventArgs e)
    {
        Log.D("UI.MainPages.SegmentsPage", "Copy from distance changed.");
        if (distance == null || CopyFromDistance.SelectedIndex < 1)
        {
            return;
        }
        page.CopyFromDistance(distance.Identifier, Convert.ToInt32(((ComboBoxItem)CopyFromDistance.SelectedItem!).Tag!));
    }

    private void NumberValidation(object? sender, Avalonia.Input.TextInputEventArgs e)
    {
        e.Handled = !e.Text!.All(char.IsDigit);
    }
}