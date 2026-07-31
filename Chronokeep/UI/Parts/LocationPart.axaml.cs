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
        string ignStr = string.Format(TimeFormat, MyLocation.IgnoreWithin / 3600, (MyLocation.IgnoreWithin % 3600) / 60, MyLocation.IgnoreWithin % 60);
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

    public bool IsUpdated()
    {
        try
        {
            string[] parts = IgnoreWithin.Text!.Replace('_', '0').Split(':');
            int hours = Convert.ToInt32(parts[0]), minutes = Convert.ToInt32(parts[1]), seconds = Convert.ToInt32(parts[2]);
            return MyLocation.MaxOccurrences != Convert.ToInt32(MaxOccurrences) && MyLocation.IgnoreWithin != (hours * 3600) + (minutes * 60) + seconds;
        }
        catch
        {
            return true;
        }
    }

    public void UpdateLocation()
    {
        Log.D("UI.MainPages.LocationsPage", "Updating location.");
        try
        {
            MyLocation.Name = LocationName.Text!;
            MyLocation.MaxOccurrences = Convert.ToInt32(MaxOccurrences.Text);
            string[] parts = IgnoreWithin.Text!.Replace('_', '0').Split(':');
            int hours = Convert.ToInt32(parts[0]), minutes = Convert.ToInt32(parts[1]), seconds = Convert.ToInt32(parts[2]);
            MyLocation.IgnoreWithin = (hours * 3600) + (minutes * 60) + seconds;
        }
        catch
        {
            DialogBox.AsyncShow("Error with values given.");
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