using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Helpers;
using Chronokeep.Objects.RFID;
using Chronokeep.Timing.Interfaces;
using Chronokeep.UI.Util;
using System;
using System.Globalization;
using Avalonia.Controls.Primitives;

namespace Chronokeep.UI.Timing.ReaderSettings;

public partial class RfidSettings : ChronokeepWindow
{
    private readonly RfidUltraInterface reader;

    public RfidSettings(RfidUltraInterface reader)
    {
        InitializeComponent();
        MinWidth = 100;
        MinHeight = 100;
        this.reader = reader;
        reader.GetStatus();
        reader.QuerySettings();
    }

    public void UpdateView(RfidSettingsHolder settings)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "Updating View.");
        Application.Current!.Dispatcher.Invoke(delegate
        {
            if (settings.UltraId is > 0 and < 256)
            {
                IdSlider.Value = settings.UltraId;
                IdDisplay.Text = settings.UltraId.ToString();
            }
            switch (settings.ChipType)
            {
                case RfidSettingsHolder.ChipTypeEnum.DEC:
                    ChipBox.SelectedIndex = 0;
                    break;
                case RfidSettingsHolder.ChipTypeEnum.HEX:
                    ChipBox.SelectedIndex = 1;
                    break;
            }
            switch (settings.GatingMode)
            {
                case RfidSettingsHolder.GatingModeEnum.PER_READER:
                    GatingModeBox.SelectedIndex = 0;
                    break;
                case RfidSettingsHolder.GatingModeEnum.PER_BOX:
                    GatingModeBox.SelectedIndex = 1;
                    break;
                case RfidSettingsHolder.GatingModeEnum.FIRST_TIME_SEEN:
                    GatingModeBox.SelectedIndex = 2;
                    break;
            }
            if (settings.GatingInterval is >= 0 and < 21)
            {
                GatingSlider.Value = settings.GatingInterval;
                GatingDisplay.Text = settings.GatingInterval.ToString();
            }
            switch (settings.Beep)
            {
                case RfidSettingsHolder.BeepEnum.ALWAYS:
                    WhenBeepBox.SelectedIndex = 0;
                    break;
                case RfidSettingsHolder.BeepEnum.ONLY_FIRST_SEEN:
                    WhenBeepBox.SelectedIndex = 1;
                    break;
            }
            switch (settings.BeepVolume)
            {
                case RfidSettingsHolder.BeepVolumeEnum.OFF:
                    VolumeBox.SelectedIndex = 0;
                    break;
                case RfidSettingsHolder.BeepVolumeEnum.SOFT:
                    VolumeBox.SelectedIndex = 1;
                    break;
                case RfidSettingsHolder.BeepVolumeEnum.LOUD:
                    VolumeBox.SelectedIndex = 2;
                    break;
            }
            SetGpsSwitch.IsChecked = settings.SetFromGps switch
            {
                RfidSettingsHolder.GpsEnum.SET => true,
                RfidSettingsHolder.GpsEnum.DONT_SET => false,
                _ => SetGpsSwitch.IsChecked
            };
            if (settings.TimeZone is > -24 and < 24)
            {
                TimeZoneSlider.Value = settings.TimeZone;
                TimeZoneDisplay.Text = settings.TimeZone.ToString();
            }
            switch (settings.Status)
            {
                case RfidSettingsHolder.StatusEnum.STARTED:
                    ReadingSwitch.IsChecked = true;
                    break;
                case RfidSettingsHolder.StatusEnum.STOPPED:
                    ReadingSwitch.IsChecked = false;
                    break;
            }
            SettingsPanel.IsVisible = true;
        });
    }

    public void CloseWindow()
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "CloseWindow.");
        Application.Current!.Dispatcher.Invoke(Close);
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        reader.SettingsWindowFinalize();
    }

    private void SaveID_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "Save ID button clicked.");
        DialogBox.Show(
            "Saving ID will reboot the reader and forcibly close the connection. Proceed?",
            "Yes",
            "No",
            () =>
            {
                reader.SetUltraId(Convert.ToInt32(Math.Floor(IdSlider.Value)));
            });
    }

    private void IdSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "ID changed.");
        if (IdDisplay != null && IdSlider != null)
        {
            IdDisplay.Text = IdSlider.Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private void SaveChip_Click(object? sender, RoutedEventArgs e)
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
        reader.SetChipOutputType(byteVal);
    }

    private void GatingSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "Gating changed.");
        if (GatingDisplay != null && GatingSlider != null)
        {
            GatingDisplay.Text = GatingSlider.Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private void SaveGatingMode_Click(object? sender, RoutedEventArgs e)
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
        reader.SetGatingMode(byteVal);
    }

    private void SaveGatingInterval_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "Save Gating Interval button clicked.");
        reader.SetGatingInterval(Convert.ToInt32(Math.Floor(GatingSlider.Value)));
    }

    private void SaveWhenBeep_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "Save When to Beep button clicked.");
        char byteVal = WhenBeepBox.SelectedIndex switch
        {
            0 => // always
                (char)0x00,
            1 => // when first seen
                (char)0x01,
            _ => (char)0x00
        };
        reader.SetWhenToBeep(byteVal);
    }

    private void SaveVolume_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "Save Volume button clicked.");
        char byteVal = VolumeBox.SelectedIndex switch
        {
            0 => // off
                (char)0x00,
            1 => // soft
                (char)0x01,
            2 => // loud
                (char)0x02,
            _ => '0'
        };
        reader.SetBeeperVolume(byteVal);
    }

    private void TimeZoneSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "Time zone changed.");
        if (TimeZoneDisplay != null && TimeZoneSlider != null)
        {
            TimeZoneDisplay.Text = TimeZoneSlider.Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private void SetGPSSwitch_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "Set Time Via GPS button clicked.");
        char byteVal = (char)0x00;
        if (SetGpsSwitch.IsChecked == true)
        {
            byteVal = (char)0x01;
        }
        reader.SetAutoGpsTime(byteVal);
    }

    private void SaveTimezone_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "Save Timezone button clicked.");
        reader.SetTimeZone(Convert.ToInt32(Math.Floor(TimeZoneSlider.Value)));
    }

    private void ReadingSwitch_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Reading clicked.");
        if (ReadingSwitch.IsChecked == true)
        {
            // switch just switched on
            reader.StartReading();
        }
        else
        {
            // switch just switch off
            reader.StopReading();
        }
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.RFIDSettings", "Close button clicked.");
        Close();
    }

    protected override void Maximize()
    {
        WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
    }
}