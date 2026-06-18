using Avalonia;
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

public partial class DistancePart : UserControl
{
    private bool plusWave = true;

    private const string TimeFormat = "{0:D2}:{1:D2}:{2:D2}.{3:D3}";
    private const string LimitFormat = "{0:D2}:{1:D2}:{2:D2}";
    private readonly DistancesPage page;
    private readonly Distance theDistance;
    private readonly Dictionary<int, Distance> distanceDictionary;

    [GeneratedRegex("[^0-9.]")]
    private static partial Regex AllowedWithDot();
    [GeneratedRegex("[^0-9]")]
    private static partial Regex AllowedChars();

    public DistancePart(DistancesPage page, Distance distance, int maxOccurrences,
                List<Distance> distances, Dictionary<int, Distance> distanceDictionary,
                Event theEvent, DistancePart? parent)
    {
        List<Distance> otherDistances = [.. distances];
        this.distanceDictionary = distanceDictionary;
        otherDistances.Remove(distance);
        this.page = page;
        theDistance = distance;
        InitializeComponent();
        DistanceName.Text = theDistance.Name;
        CopyFromBox.Items.Clear();
        CopyFromBox.Items.Add(new ComboBoxItem()
        {
            Content = "",
            Tag = "-1"
        });
        foreach (Distance div in otherDistances)
        {
            if (div.LinkedDistance == Constants.Timing.DISTANCE_NO_LINKED_ID)
            {
                CopyFromBox.Items.Add(new ComboBoxItem()
                {
                    Content = div.Name,
                    Tag = div.Identifier.ToString()
                });
            }
        }
        CopyFromBox.SelectedIndex = 0;
        DistanceBox.Text = theDistance.DistanceValue.ToString(CultureInfo.InvariantCulture);
        DistanceUnit.Items.Clear();
        DistanceUnit.Items.Add(new ComboBoxItem()
        {
            Content = "",
            Tag = Constants.Distances.UNKNOWN
        });
        DistanceUnit.Items.Add(new ComboBoxItem()
        {
            Content = "Miles",
            Tag = Constants.Distances.MILES
        });
        DistanceUnit.Items.Add(new ComboBoxItem()
        {
            Content = "Kilometers",
            Tag = Constants.Distances.KILOMETERS
        });
        DistanceUnit.Items.Add(new ComboBoxItem()
        {
            Content = "Meters",
            Tag = Constants.Distances.METERS
        });
        DistanceUnit.Items.Add(new ComboBoxItem()
        {
            Content = "Yards",
            Tag = Constants.Distances.YARDS
        });
        DistanceUnit.Items.Add(new ComboBoxItem()
        {
            Content = "Feet",
            Tag = Constants.Distances.FEET
        });
        if (theDistance.DistanceUnit == Constants.Distances.MILES)
        {
            DistanceUnit.SelectedIndex = 1;
        }
        else if (theDistance.DistanceUnit == Constants.Distances.KILOMETERS)
        {
            DistanceUnit.SelectedIndex = 2;
        }
        else if (theDistance.DistanceUnit == Constants.Distances.METERS)
        {
            DistanceUnit.SelectedIndex = 3;
        }
        else if (theDistance.DistanceUnit == Constants.Distances.YARDS)
        {
            DistanceUnit.SelectedIndex = 4;
        }
        else if (theDistance.DistanceUnit == Constants.Distances.FEET)
        {
            DistanceUnit.SelectedIndex = 5;
        }
        else
        {
            DistanceUnit.SelectedIndex = 0;
        }
        if (Constants.Timing.EVENT_TYPE_DISTANCE == theEvent.EventType)
        {
            ComboBoxItem? selected = null;
            for (int i = 1; i <= maxOccurrences; i++)
            {
                ComboBoxItem current = new()
                {
                    Content = i.ToString(),
                    Tag = i.ToString()
                };
                if (i == theDistance.FinishOccurrence)
                {
                    selected = current;
                }
                FinishOccurrence.Items.Add(current);
            }
            if (selected != null)
            {
                FinishOccurrence.SelectedItem = selected;
            }
            else
            {
                FinishOccurrence.SelectedIndex = 0;
            }
        }
        else
        {
            DockPanel limitPanel = new();
            limitPanel.Children.Add(new TextBlock()
            {
                Text = "Max Time",
                Width = 65,
                FontSize = 12,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            });
            string limit = string.Format(LimitFormat, theDistance.EndSeconds / 3600,
                theDistance.EndSeconds % 3600 / 60, theDistance.EndSeconds % 60);
            TimeLimit.Text = limit;
        }
        if (Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA != theEvent.EventType)
        {
            Wave.Text = theDistance.Wave.ToString();
            plusWave = true;
            if (theDistance.StartOffsetSeconds < 0)
            {
                Log.D("UI.MainPages.DistancesPage", "Setting type to negative and making seconds/milliseconds positive for offset textbox.");
                plusWave = false;
                theDistance.StartOffsetSeconds *= -1;
                theDistance.StartOffsetMilliseconds *= -1;
            }
        }
        PlusIcon.IsVisible = plusWave;
        MinusIcon.IsVisible = !plusWave;
        StartOffset.Text = string.Format(TimeFormat, theDistance.StartOffsetSeconds / 3600,
            theDistance.StartOffsetSeconds % 3600 / 60, theDistance.StartOffsetSeconds % 60,
            theDistance.StartOffsetMilliseconds);
        Certification.Text = theDistance.Certification;
        if (theEvent.UploadSpecific)
        {
            UploadPanel.IsVisible = true;
            Upload.IsChecked = theDistance.Upload;
        }
        TypeBox.Items.Clear();
        TypeBox.Items.Add(
            new ComboBoxItem
            {
                Content = "Normal",
                Tag = Constants.Timing.DISTANCE_TYPE_NORMAL
            });
        TypeBox.Items.Add(
            new ComboBoxItem
            {
                Content = "Early Start",
                Tag = Constants.Timing.DISTANCE_TYPE_EARLY
            });
        TypeBox.Items.Add(
            new ComboBoxItem
            {
                Content = "Late Start",
                Tag = Constants.Timing.DISTANCE_TYPE_LATE
            });
        TypeBox.Items.Add(
            new ComboBoxItem
            {
                Content = "Drop",
                Tag = Constants.Timing.DISTANCE_TYPE_DROP
            });
        TypeBox.Items.Add(
            new ComboBoxItem
            {
                Content = "Unranked",
                Tag = Constants.Timing.DISTANCE_TYPE_UNOFFICIAL
            });
        TypeBox.Items.Add(
            new ComboBoxItem
            {
                Content = "Virtual",
                Tag = Constants.Timing.DISTANCE_TYPE_VIRTUAL
            });
        Ranking.Text = theDistance.Ranking.ToString();
        Ranking.IsEnabled = true;
        switch (theDistance.Type)
        {
            case Constants.Timing.DISTANCE_TYPE_EARLY:
                TypeBox.SelectedIndex = 1;
                break;
            case Constants.Timing.DISTANCE_TYPE_LATE:
                Ranking.Text = "0";
                Ranking.IsEnabled = false;
                TypeBox.SelectedIndex = 2;
                break;
            case Constants.Timing.DISTANCE_TYPE_DROP:
                TypeBox.SelectedIndex = 3;
                break;
            case Constants.Timing.DISTANCE_TYPE_UNOFFICIAL:
                TypeBox.SelectedIndex = 4;
                break;
            case Constants.Timing.DISTANCE_TYPE_VIRTUAL:
                TypeBox.SelectedIndex = 5;
                break;
            case Constants.Timing.DISTANCE_TYPE_NORMAL:
                TypeBox.SelectedIndex = 0;
                break;
        }
        DistPanel.IsVisible = parent == null;
        CopyPanel.IsVisible = parent == null;
        AddRemovePanel.IsVisible = parent == null;
        LinkedPanel.IsVisible = parent != null;
        RankPanel.IsVisible = parent != null;
        AltRemoveBtn.IsVisible = parent != null;
        CertPanel.IsVisible = Constants.Timing.EVENT_TYPE_DISTANCE == theEvent.EventType && parent == null;
        IntervalBlock.IsVisible = Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA == theEvent.EventType;
        MaxTimePanel.IsVisible = Constants.Timing.EVENT_TYPE_DISTANCE != theEvent.EventType;
        OccurrencePanel.IsVisible = Constants.Timing.EVENT_TYPE_DISTANCE == theEvent.EventType;
        if (parent == null) return;
        MainPanel.Margin = new Thickness(50, 0);
        MainPanel.MaxWidth = 500;
        SepPanel.Margin = new Thickness(-50, 5, -50, 0);
    }

    public Distance GetDistance()
    {
        return theDistance;
    }

    public void UpdateDistance()
    {
        Log.D("UI.MainPages.DistancesPage", "Updating distance.");
        theDistance.Name = DistanceName.Text!;
        double dist;
        try
        {
            dist = Convert.ToDouble(DistanceBox.Text!);
        }
        catch
        {
            dist = 0.0;
        }
        if (dist >= 0.0)
        {
            theDistance.DistanceValue = dist;
        }
        theDistance.DistanceUnit = Convert.ToInt32(((ComboBoxItem)DistanceUnit.SelectedItem!).Tag);
        if (FinishOccurrence is { SelectedItem: not null })
        {
            theDistance.FinishOccurrence = Convert.ToInt32(((ComboBoxItem)FinishOccurrence.SelectedItem).Tag!);
        }
        theDistance.EndSeconds = 0;
        if (TimeLimit != null)
        {
            string[] limitParts = TimeLimit.Text!.Replace('_', '0').Split(':');
            theDistance.EndSeconds = (Convert.ToInt32(limitParts[0]) * 3600)
                + (Convert.ToInt32(limitParts[1]) * 60)
                + Convert.ToInt32(limitParts[2]);
        }
        int wave = -1;
        if (Wave != null)
        {
            if (!int.TryParse(Wave.Text, out wave))
            {
                theDistance.Wave = -1;
            }
        }
        if (wave >= 0)
        {
            theDistance.Wave = wave;
        }
        string[] firstParts = StartOffset.Text!.Replace('_', '0').Split(':');
        string[] secondParts = firstParts[2].Split('.');
        try
        {
            theDistance.StartOffsetSeconds = (Convert.ToInt32(firstParts[0]) * 3600)
                + (Convert.ToInt32(firstParts[1]) * 60)
                + Convert.ToInt32(secondParts[0]);
            theDistance.StartOffsetMilliseconds = Convert.ToInt32(secondParts[1]);
        }
        catch
        {
            DialogBox.Show("Error with values given.");
        }
        if (!plusWave)
        {
            Log.D("UI.MainPages.DistancesPage", "Recording negative values.");
            theDistance.StartOffsetSeconds *= -1;
            theDistance.StartOffsetMilliseconds *= -1;
        }
        if (Upload != null)
        {
            theDistance.Upload = Upload.IsChecked == true;
        }
        else
        {
            theDistance.Upload = true;
        }
        theDistance.Certification = Certification != null ? Certification.Text! : "";
        if (Ranking.Text != null && int.TryParse(Ranking.Text, out int rankVal))
        {
            theDistance.Ranking = rankVal;
        }
        theDistance.Type = TypeBox.SelectedItem != null ? (int)((ComboBoxItem)TypeBox.SelectedItem).Tag! : Constants.Timing.DISTANCE_TYPE_NORMAL;
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
        Log.D("UI.MainPages.DistancesPage", "Removing distance.");
        page.RemoveDistance(theDistance);
    }

    private void TypeBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (TypeBox.SelectedIndex == 2)
        {
            Ranking.Text = "0";
            Ranking.IsEnabled = false;
        }
        else
        {
            Ranking.Text = theDistance.Ranking.ToString();
            Ranking.IsEnabled = true;
        }
    }

    private void SwapWaveType_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.DistancesPage", $"Plus/Minus sign clicked. PlusWave is: {plusWave}'");
        plusWave = !plusWave;
        PlusIcon.IsVisible = plusWave;
        MinusIcon.IsVisible = !plusWave;
    }

    private void DotValidation(object? sender, TextInputEventArgs e)
    {
        e.Handled = AllowedWithDot().IsMatch(e.Text!);
    }

    private void AddSub_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.DistancesPage", "Adding sub distance.");
        page.AddSubDistance(theDistance);
    }

    private void CopyFromBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Log.D("UI.MainPages.DistancesPage", "Attempting to copy from a different distance! Here we go!");
        // Ensure we've got something selected, it has a parse-able UID,
        // and there's a distance related to it
        if (CopyFromBox.SelectedItem == null
            || !int.TryParse((string)((ComboBoxItem)CopyFromBox.SelectedItem).Tag!, out int newDivId)
            || !distanceDictionary.TryGetValue(newDivId, out Distance? newDiv)) return;
        theDistance.Name = DistanceName.Text!;
        theDistance.DistanceValue = newDiv.DistanceValue;
        theDistance.DistanceUnit = newDiv.DistanceUnit;
        theDistance.FinishOccurrence = newDiv.FinishOccurrence;
        theDistance.Wave = newDiv.Wave;
        theDistance.StartOffsetSeconds = newDiv.StartOffsetSeconds;
        theDistance.StartOffsetMilliseconds = newDiv.StartOffsetMilliseconds;
        theDistance.Upload = newDiv.Upload;
        page.UpdateDistance(theDistance);
    }
}