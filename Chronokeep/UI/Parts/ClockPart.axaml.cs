using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Helpers;
using Chronokeep.Network.API;
using Chronokeep.Objects;
using Chronokeep.UI.Timing.Windows;
using Chronokeep.UI.Util;
using System;

namespace Chronokeep.UI.Parts;

public partial class ClockPart : UserControl
{
    private readonly ClockControl parent;
    private Chronoclock clock;
    private readonly Event theEvent;

    private bool IsLocked { get; set; }

    public ClockPart(Chronoclock clock, ClockControl parent, Event theEvent)
    {
        InitializeComponent();
        this.clock = clock;
        this.theEvent = theEvent;
        this.parent = parent;
        NameBlock.Text = clock.Name;
        UrlBlock.Text = clock.Url;
        EnabledSwitch.IsChecked = clock.Enabled;
        BrightnessBox.IsEnabled = false;
        CountDatePicker.IsEnabled = false;
        CountTimeBox.IsEnabled = false;
        Start.IsEnabled = false;
        Stop.IsEnabled = false;
        GetTime.IsEnabled = false;
        SetTime.IsEnabled = false;
        if (!string.IsNullOrEmpty(clock.Url))
        {
            GetConfig();
        }
    }

    private void UpdateLockStatus(bool locked)
    {
        IsLocked = locked;
        if (locked)
        {
            Start.IsEnabled = false;
            Stop.IsEnabled = false;
            LockedImage.IsVisible = true;
            UnlockedImage.IsVisible = false;
        }
        else
        {
            Start.IsEnabled = true;
            Stop.IsEnabled = true;
            LockedImage.IsVisible = false;
            UnlockedImage.IsVisible = true;
        }
    }

    private async void GetConfig()
    {
        try
        {
            GetConfigResponse resp = await clock.GetConfig();
            UpdateInformation(new CountUpDownTimestampResponse
            {
                CountUpDownTimestamp = resp.CountUpDownTimestamp,
                Brightness = resp.Brightness,
                FlipDisplay = resp.FlipDisplay,
                LockCountUpDown = resp.LockCountUpDown,
            });
        }
        catch (ApiException ex)
        {
            Log.D("UI.Timing.ClockControl.ClockListItem", "Unable to fetch clock config." + ex.Message);
        }
        catch (Exception)
        {
            Log.D("UI.Timing.ClockControl.ClockListItem", "Error getting config.");
        }
    }

    private void UpdateInformation(CountUpDownTimestampResponse info)
    {
        clock = GetUpdatedClock();
        if (info.Brightness > 0)
        {
            BrightnessBox.SelectedIndex = (int)(info.Brightness - 1);
        }
        UpdateLockStatus(info.LockCountUpDown);
        if (info.CountUpDownTimestamp > 0)
        {
            DateTime countupdown = Constants.Timing.UtcToLocalDate(info.CountUpDownTimestamp, 0);
            CountDatePicker.SelectedDate = countupdown;
            ChangeCountTimeBox(countupdown.ToString("HH:mm:ss"));
        }
        else if (theEvent.StartSeconds > 0 || theEvent.StartMilliseconds > 0)
        {
            CountDatePicker.SelectedDate = DateTime.Parse(theEvent.Date);
            ChangeCountTimeBox(Constants.Timing.SecondsToTime(theEvent.StartMilliseconds >= 500 ? theEvent.StartSeconds + 1 : theEvent.StartSeconds));
            Log.D("UI.Timing.ClockControl.ClockListItem",
                $"Time should be set to: {Constants.Timing.SecondsToTime(theEvent.StartSeconds)}");
        }
        EnableConfig();
    }

    private void ChangeCountTimeBox(string time)
    {
        CountTimeBox.IsEnabled = true;
        CountTimeBox.Text = time;
        CountTimeBox.IsEnabled = false;
    }

    private void EnableConfig()
    {
        BrightnessBox.IsEnabled = true;
        LockedSwitch.IsEnabled = true;
        CountDatePicker.IsEnabled = true;
        CountTimeBox.IsEnabled = true;
        GetTime.IsEnabled = true;
        SetTime.IsEnabled = true;
        Start.IsEnabled = !IsLocked;
        Stop.IsEnabled = !IsLocked;
    }

    private void DisableConfig()
    {
        BrightnessBox.IsEnabled = false;
        LockedSwitch.IsEnabled = false;
        CountDatePicker.IsEnabled = false;
        CountTimeBox.IsEnabled = false;
        GetTime.IsEnabled = false;
        SetTime.IsEnabled = false;
        Start.IsEnabled = false;
        Stop.IsEnabled = false;
    }

    public Chronoclock GetUpdatedClock()
    {
        Chronoclock output = new()
        {
            Identifier = clock.Identifier,
            Name = NameBlock.Text!,
            Enabled = EnabledSwitch.IsChecked == true,
            Url = UrlBlock.Text!,
        };
        return output;
    }

    private async void LockedChanged(object sender, RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.Timing.ClockControl.ClockListItem", "LockedChanged");
            clock = GetUpdatedClock();
            if (!LockedSwitch.IsEnabled) return;
            UpdateLockStatus(!IsLocked);
            DisableConfig();
            try
            {
                CountUpDownTimestampResponse resp = await clock.SetLockCountUpDown(IsLocked);
                UpdateInformation(resp);
            }
            catch (ApiException ex)
            {
                DialogBox.Show(ex.Message);
            }
        }
        catch (Exception)
        {
            Log.D("UI.Timing.ClockControl.ClockListItem", "Error setting lock.");
        }
    }

    private async void BrightnessChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            Log.D("UI.Timing.ClockControl.ClockListItem", "BrightnessChanged");
            clock = GetUpdatedClock();
            if (!BrightnessBox.IsEnabled) return;
            if (BrightnessBox.SelectedIndex < 0) return;
            DisableConfig();
            try
            {
                CountUpDownTimestampResponse resp = await clock.SetBrightness((uint)(BrightnessBox.SelectedIndex + 1));
                UpdateInformation(resp);
            }
            catch (ApiException ex)
            {
                DialogBox.Show(ex.Message);
            }
        }
        catch (Exception)
        {
            Log.D("UI.Timing.ClockControl.ClockListItem", "Error changing brightness.");
        }
    }

    private async void Start_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.Timing.ClockControl.ClockListItem", "Start clicked.");
            clock = GetUpdatedClock();
            DateTime countDate;
            if (CountDatePicker.SelectedDate == null || CountTimeBox.Text == null)
            {
                try
                {
                    countDate = DateTime.Now;
                    CountUpDownTimestampResponse resp = await clock.SetCountUpDownTime(countDate);
                    UpdateInformation(resp);
                }
                catch (ApiException ex)
                {
                    DialogBox.Show(ex.Message);
                }
            }
            else
            {
                if (!DateTime.TryParse($"{CountDatePicker.SelectedDate?.ToString("yyyy/M/d") ?? DateTime.Now.ToString("yyyy/M/d")} {CountTimeBox.Text!.Replace('_', '0')}", out countDate))
                {
                    countDate = DateTime.Now;
                }
                try
                {
                    CountUpDownTimestampResponse resp = await clock.SetCountUpDownTime(countDate);
                    UpdateInformation(resp);
                }
                catch (ApiException ex)
                {
                    DialogBox.Show(ex.Message);
                }
            }
        }
        catch (Exception)
        {
            Log.D("UI.Timing.ClockControl.ClockListItem", "Error starting clock.");
        }
    }

    private async void Stop_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.Timing.ClockControl.ClockListItem", "Stop clicked.");
            clock = GetUpdatedClock();
            try
            {
                CountUpDownTimestampResponse resp = await clock.StopCountUp();
                UpdateInformation(resp);
            }
            catch (ApiException ex)
            {
                DialogBox.Show(ex.Message);
            }
        }
        catch (Exception)
        {
            Log.D("UI.Timing.ClockControl.ClockListItem", "Error stopping clock.");
        }
    }

    private async void GetTime_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.Timing.ClockControl.ClockListItem", "Get Time clicked.");
            clock = GetUpdatedClock();
            try
            {
                GetTimeResponse resp = await clock.GetTime();
                parent.UpdateTime(resp.Time);
            }
            catch (ApiException ex)
            {
                DialogBox.Show(ex.Message);
            }
        }
        catch (Exception)
        {
            Log.D("UI.Timing.ClockControl.ClockListItem", "Error getting time.");
        }
    }

    private async void SetTime_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.Timing.ClockControl.ClockListItem", "Set Time clicked.");
            clock = GetUpdatedClock();
            try
            {
                GetTimeResponse resp = await clock.SetTime(DateTime.Now);
                parent.UpdateTime(resp.Time);
            }
            catch (ApiException ex)
            {
                DialogBox.Show(ex.Message);
            }
        }
        catch (Exception)
        {
            Log.D("UI.Timing.ClockControl.ClockListItem", "Error setting time.");
        }
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.Timing.ClockControl.ClockListItem", "Refresh clicked.");
            clock = GetUpdatedClock();
            try
            {
                GetConfigResponse resp = await clock.GetConfig();
                UpdateInformation(new CountUpDownTimestampResponse
                {
                    CountUpDownTimestamp = resp.CountUpDownTimestamp,
                    Brightness = resp.Brightness,
                    FlipDisplay = resp.FlipDisplay,
                    LockCountUpDown = resp.LockCountUpDown,
                });
            }
            catch (ApiException ex)
            {
                DialogBox.Show(ex.Message);
            }
        }
        catch (Exception)
        {
            Log.D("UI.Timing.ClockControl.ClockListItem", "Error refreshing data.");
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ClockControl.ClockListItem", "Delete clicked.");
        parent.RemoveClock(clock);
    }
}