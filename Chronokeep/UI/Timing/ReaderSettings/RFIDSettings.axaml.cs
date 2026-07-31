using Avalonia;
using Avalonia.Interactivity;
using Chronokeep.Helpers;
using Chronokeep.Objects.RFID;
using Chronokeep.Timing.Interfaces;
using Chronokeep.UI.Util;
using System;
using System.Globalization;
using Avalonia.Controls.Primitives;
using Avalonia.Controls;

namespace Chronokeep.UI.Timing.ReaderSettings;

public partial class RfidSettings : ChronokeepWindow
{
    private readonly RfidUltraInterface reader;

    public RfidSettings(RfidUltraInterface reader)
    {
        InitializeComponent();
        ChronokeepInitialize();
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
            ChipBox.SelectedIndex = settings.ChipType switch
            {
                RfidSettingsHolder.ChipTypeEnum.DEC => 0,
                RfidSettingsHolder.ChipTypeEnum.HEX => 1,
                _ => ChipBox.SelectedIndex
            };
            GatingModeBox.SelectedIndex = settings.GatingMode switch
            {
                RfidSettingsHolder.GatingModeEnum.PER_READER => 0,
                RfidSettingsHolder.GatingModeEnum.PER_BOX => 1,
                RfidSettingsHolder.GatingModeEnum.FIRST_TIME_SEEN => 2,
                _ => GatingModeBox.SelectedIndex
            };
            if (settings.GatingInterval is >= 0 and < 21)
            {
                GatingSlider.Value = settings.GatingInterval;
                GatingDisplay.Text = settings.GatingInterval.ToString();
            }
            WhenBeepBox.SelectedIndex = settings.Beep switch
            {
                RfidSettingsHolder.BeepEnum.ALWAYS => 0,
                RfidSettingsHolder.BeepEnum.ONLY_FIRST_SEEN => 1,
                _ => WhenBeepBox.SelectedIndex
            };
            VolumeBox.SelectedIndex = settings.BeepVolume switch
            {
                RfidSettingsHolder.BeepVolumeEnum.OFF => 0,
                RfidSettingsHolder.BeepVolumeEnum.SOFT => 1,
                RfidSettingsHolder.BeepVolumeEnum.LOUD => 2,
                _ => VolumeBox.SelectedIndex
            };
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
            ReadingSwitch.IsChecked = settings.Status switch
            {
                RfidSettingsHolder.StatusEnum.STARTED => true,
                RfidSettingsHolder.StatusEnum.STOPPED => false,
                _ => ReadingSwitch.IsChecked
            };
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
        DialogBox.AsyncShow(
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
        char byteVal = ChipBox.SelectedIndex switch
        {
            0 => // Decimal
                (char)0x00,
            1 => // Hexadecimal
                (char)0x01,
            _ => (char)0x00
        };
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
        char byteVal = GatingModeBox.SelectedIndex switch
        {
            0 => // Per reader
                (char)0x00,
            1 => // Per box
                (char)0x01,
            2 => // First time seen
                (char)0x02,
            _ => (char)0x00
        };
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

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}