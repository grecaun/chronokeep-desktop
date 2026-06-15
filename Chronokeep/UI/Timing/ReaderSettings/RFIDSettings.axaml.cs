using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Helpers;
using Chronokeep.Objects.RFID;
using Chronokeep.Timing.Interfaces;
using Chronokeep.UI.Util;
using System;

namespace Chronokeep.UI.Timing.ReaderSettings;

public partial class RFIDSettings : Window
{
    private readonly RFIDUltraInterface? reader = null;

    public RFIDSettings(RFIDUltraInterface reader)
    {
        InitializeComponent();
        this.MinWidth = 100;
        this.MinHeight = 100;
        this.reader = reader;
        reader?.GetStatus();
        reader?.QuerySettings();
    }

    public void UpdateView(RFIDSettingsHolder settings)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "Updating View.");
        Application.Current!.Dispatcher.Invoke(new Action(delegate ()
        {
            if (settings.UltraID > 0 && settings.UltraID < 256)
            {
                IdSlider.Value = settings.UltraID;
                IdDisplay.Text = settings.UltraID.ToString();
            }
            switch (settings.ChipType)
            {
                case RFIDSettingsHolder.ChipTypeEnum.DEC:
                    ChipBox.SelectedIndex = 0;
                    break;
                case RFIDSettingsHolder.ChipTypeEnum.HEX:
                    ChipBox.SelectedIndex = 1;
                    break;
            }
            switch (settings.GatingMode)
            {
                case RFIDSettingsHolder.GatingModeEnum.PER_READER:
                    GatingModeBox.SelectedIndex = 0;
                    break;
                case RFIDSettingsHolder.GatingModeEnum.PER_BOX:
                    GatingModeBox.SelectedIndex = 1;
                    break;
                case RFIDSettingsHolder.GatingModeEnum.FIRST_TIME_SEEN:
                    GatingModeBox.SelectedIndex = 2;
                    break;
            }
            if (settings.GatingInterval >= 0 && settings.GatingInterval < 21)
            {
                GatingSlider.Value = settings.GatingInterval;
                GatingDisplay.Text = settings.GatingInterval.ToString();
            }
            switch (settings.Beep)
            {
                case RFIDSettingsHolder.BeepEnum.ALWAYS:
                    WhenBeepBox.SelectedIndex = 0;
                    break;
                case RFIDSettingsHolder.BeepEnum.ONLY_FIRST_SEEN:
                    WhenBeepBox.SelectedIndex = 1;
                    break;
            }
            switch (settings.BeepVolume)
            {
                case RFIDSettingsHolder.BeepVolumeEnum.OFF:
                    VolumeBox.SelectedIndex = 0;
                    break;
                case RFIDSettingsHolder.BeepVolumeEnum.SOFT:
                    VolumeBox.SelectedIndex = 1;
                    break;
                case RFIDSettingsHolder.BeepVolumeEnum.LOUD:
                    VolumeBox.SelectedIndex = 2;
                    break;
            }
            switch (settings.SetFromGPS)
            {
                case RFIDSettingsHolder.GPSEnum.SET:
                    SetGPSSwitch.IsChecked = true;
                    break;
                case RFIDSettingsHolder.GPSEnum.DONT_SET:
                    SetGPSSwitch.IsChecked = false;
                    break;
            }
            if (settings.TimeZone > -24 && settings.TimeZone < 24)
            {
                TimeZoneSlider.Value = settings.TimeZone;
                TimeZoneDisplay.Text = settings.TimeZone.ToString();
            }
            switch (settings.Status)
            {
                case RFIDSettingsHolder.StatusEnum.STARTED:
                    ReadingSwitch.IsChecked = true;
                    break;
                case RFIDSettingsHolder.StatusEnum.STOPPED:
                    ReadingSwitch.IsChecked = false;
                    break;
            }
            SettingsPanel.IsVisible = true;
        }));
    }

    public void CloseWindow()
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "CloseWindow.");
        Application.Current!.Dispatcher.Invoke(new Action(Close));
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        reader?.SettingsWindowFinalize();
    }

    private void SaveID_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "Save ID button clicked.");
        DialogBox.Show(
            "Saving ID will reboot the reader and forcibly close the connection. Proceed?",
            "Yes",
            "No",
            () =>
            {
                reader?.SetUltraId(Convert.ToInt32(Math.Floor(IdSlider.Value)));
            });
    }

    private void IdSlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "ID changed.");
        if (IdDisplay != null && IdSlider != null)
        {
            IdDisplay.Text = IdSlider.Value.ToString();
        }
    }

    private void SaveChip_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "Save Chip button clicked.");
        char byteVal = (char)0x00;
        switch (ChipBox.SelectedIndex)
        {
            case 0:     // Decimal
                byteVal = (char)0x00;
                break;
            case 1:     // Hexadecimal
                byteVal = (char)0x01;
                break;
        }
        reader?.SetChipOutputType(byteVal);
    }

    private void GatingSlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "Gating changed.");
        if (GatingDisplay != null && GatingSlider != null)
        {
            GatingDisplay.Text = GatingSlider.Value.ToString();
        }
    }

    private void SaveGatingMode_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "Save Gating Mode button clicked.");
        char byteVal = (char)0x00;
        switch (GatingModeBox.SelectedIndex)
        {
            case 0:     // Per reader
                byteVal = (char)0x00;
                break;
            case 1:     // Per box
                byteVal = (char)0x01;
                break;
            case 2:     // First time seen
                byteVal = (char)0x02;
                break;
        }
        reader?.SetGatingMode(byteVal);
    }

    private void SaveGatingInterval_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "Save Gating Interval button clicked.");
        reader?.SetGatingInterval(Convert.ToInt32(Math.Floor(GatingSlider.Value)));
    }

    private void SaveWhenBeep_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "Save When to Beep button clicked.");
        char byteVal = (char)0x00;
        switch (WhenBeepBox.SelectedIndex)
        {
            case 0:     // always
                byteVal = (char)0x00;
                break;
            case 1:     // when first seen
                byteVal = (char)0x01;
                break;
        }
        reader?.SetWhenToBeep(byteVal);
    }

    private void SaveVolume_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "Save Volume button clicked.");
        char byteVal = '0';
        switch (VolumeBox.SelectedIndex)
        {
            case 0:     // off
                byteVal = (char)0x00;
                break;
            case 1:     // soft
                byteVal = (char)0x01;
                break;
            case 2:     // loud
                byteVal = (char)0x02;
                break;
        }
        reader?.SetBeeperVolume(byteVal);
    }

    private void TimeZoneSlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "Time zone changed.");
        if (TimeZoneDisplay != null && TimeZoneSlider != null)
        {
            TimeZoneDisplay.Text = TimeZoneSlider.Value.ToString();
        }
    }

    private void SetGPSSwitch_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "Set Time Via GPS button clicked.");
        char byteVal = (char)0x00;
        if (SetGPSSwitch.IsChecked == true)
        {
            byteVal = (char)0x01;
        }
        reader?.SetAutoGPSTime(byteVal);
    }

    private void SaveTimezone_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "Save Timezone button clicked.");
        reader?.SetTimeZone(Convert.ToInt32(Math.Floor(TimeZoneSlider.Value)));
    }

    private void ReadingSwitch_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Reading clicked.");
        if (ReadingSwitch.IsChecked == true)
        {
            // switch just switched on
            reader?.StartReading();
        }
        else
        {
            // switch just switch off
            reader?.StopReading();
        }
    }

    private void CloseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "Close button clicked.");
        Close();
    }

    private void OnMinimize(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximize(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        MaximizeIcon?.IsVisible = WindowState == WindowState.Normal;
        UnMaximizeIcon?.IsVisible = WindowState == WindowState.Maximized;
    }
}