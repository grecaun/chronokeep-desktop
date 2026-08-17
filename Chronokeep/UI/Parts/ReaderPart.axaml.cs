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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Chronokeep.Constants;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.UI.Timing.Windows;
using Chronokeep.UI.Util;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Chronokeep.UI.Parts;

public partial class ReaderPart : UserControl
{
    private readonly ITimingPage parent;
    private List<TimingLocation> locations;
    public readonly TimingSystem Reader;

    public RewindWindow? Rewind = null;

    [GeneratedRegex("^([01]?[0-9]?[0-9]|2[0-4][0-9]|25[0-5])\\.([01]?[0-9]?[0-9]|2[0-4][0-9]|25[0-5])\\.([01]?[0-9]?[0-9]|2[0-4][0-9]|25[0-5])\\.([01]?[0-9]?[0-9]|2[0-4][0-9]|25[0-5])$")]
    private static partial Regex IpPattern();
    [GeneratedRegex("[^0-9.]")]
    private static partial Regex AllowedChars();
    [GeneratedRegex("[^0-9]")]
    private static partial Regex AllowedNums();

    public ReaderPart(ITimingPage parent, TimingSystem sys, List<TimingLocation> locations)
    {
        this.parent = parent;
        this.locations = locations;
        InitializeComponent();
        Reader = sys;
        ComboBoxItem? current, selected = null;
        foreach (string systemIdVal in Readers.SYSTEM_NAMES.Keys)
        {
            current = new ComboBoxItem()
            {
                Content = Readers.SYSTEM_NAMES[systemIdVal],
                Tag = systemIdVal
            };
            if (systemIdVal == Reader.Type)
            {
                selected = current;
            }
            ReaderType.Items.Add(current);
        }
        if (selected != null)
        {
            ReaderType.SelectedItem = selected;
        }
        else
        {
            ReaderType.SelectedIndex = 0;
        }
        ReaderIp.Text = Reader.IpAddress;
        ReaderPort.Text = Reader.Port.ToString();
        selected = null;
        foreach (TimingLocation loc in this.locations)
        {
            current = new ComboBoxItem()
            {
                Content = loc.Name,
                Tag = loc.Identifier.ToString()
            };
            if (Reader.LocationId == loc.Identifier)
            {
                selected = current;
            }
            ReaderLocation.Items.Add(current);
        }
        if (selected != null)
        {
            ReaderLocation.SelectedItem = selected;
        }
        else
        {
            ReaderLocation.SelectedIndex = 0;
        }
        RemoveButton.IsVisible = Reader.Saved();
        UpdateStatus();
    }

    public void UpdateLocations(List<TimingLocation> iLocations)
    {
        this.locations = iLocations;
        int selectedLocation = Convert.ToInt32(((ComboBoxItem)ReaderLocation.SelectedItem!).Tag!);
        ReaderLocation.Items.Clear();
        ComboBoxItem? selected = null;
        foreach (TimingLocation loc in this.locations)
        {
            ComboBoxItem current = new()
            {
                Content = loc.Name,
                Tag = loc.Identifier.ToString()
            };
            if (selectedLocation == loc.Identifier)
            {
                selected = current;
            }
            ReaderLocation.Items.Add(current);
        }
        if (selected != null)
        {
            ReaderLocation.SelectedItem = selected;
        }
        else
        {
            ReaderLocation.SelectedIndex = 0;
        }
    }

    public void UpdateStatus()
    {
        switch (Reader.Status)
        {
            case SYSTEM_STATUS.CONNECTED:
                SetConnected();
                break;
            case SYSTEM_STATUS.DISCONNECTED:
                SetDisconnected();
                break;
            case SYSTEM_STATUS.WORKING:
            default:
                SetWorking();
                break;
        }

        ChangeReadingStatus(Reader.SystemStatus);
    }

    public void UpdateReader()
    {
        // Check if IP is a valid IP address
        Reader.IpAddress = !IpPattern().IsMatch(ReaderIp.Text!.Trim()) ? "" : ReaderIp.Text.Trim();
        // Check if Port is valid.
        _ = int.TryParse(ReaderPort.Text!.Trim(), out int portNo);
        if (portNo > 65535)
        {
            portNo = -1;
        }
        Reader.Port = portNo;
        Reader.LocationId = Convert.ToInt32(((ComboBoxItem)ReaderLocation.SelectedItem!).Tag!);
        Reader.LocationName = ((ComboBoxItem)ReaderLocation.SelectedItem).Content!.ToString()!;
    }

    private void SetConnected()
    {
        ReaderType.IsEnabled = false;
        ReaderIp.IsEnabled = false;
        ReaderPort.IsEnabled = false;
        ReaderLocation.IsEnabled = false;
        RemoveButton.IsEnabled = false;
        RemoveButton.Opacity = 0.2;
        if (Reader.Type.Equals(Readers.SYSTEM_IPICO_LITE, StringComparison.OrdinalIgnoreCase))
        {
            RewindButton.IsEnabled = false;
            ClockButton.IsEnabled = false;
            SettingsButton.IsEnabled = false;
            RewindButton.Opacity = 0.2;
            ClockButton.Opacity = 0.2;
            SettingsButton.Opacity = 0.2;
        }
        else
        {
            RewindButton.IsEnabled = true;
            ClockButton.IsEnabled = true;
            RewindButton.Opacity = 1.0;
            ClockButton.Opacity = 1.0;
            if (Reader.SystemInterface!.SettingsEditable())
            {
                SettingsButton.IsEnabled = true;
                ReaderButton.IsEnabled = true;
                SettingsButton.Opacity = 1.0;
                ReaderButton.Opacity = 1.0;
            }
            else
            {
                SettingsButton.IsEnabled = false;
                ReaderButton.IsEnabled = false;
                SettingsButton.Opacity = 0.2;
                ReaderButton.Opacity = 0.2;
            }
        }
        ConnectButton.IsEnabled = true;
        ConnectButton.Opacity = 1.0;
        Application.Current!.Resources.TryGetResource("stop_regular", null, out object? icon);
        ConnectButton.Content = new PathIcon()
        {
            Data = (StreamGeometry?)icon,
        };
        ConnectButton.Tag = "disconnect";
    }

    private void SetDisconnected()
    {
        ReaderType.IsEnabled = true;
        ReaderIp.IsEnabled = true;
        ReaderPort.IsEnabled = Readers.SYSTEM_CHRONOKEEP_PORTAL != Reader.Type;
        ReaderLocation.IsEnabled = true;
        // Set Remove and Connect buttons to enabled
        RemoveButton.IsEnabled = true;
        ConnectButton.IsEnabled = true;
        RemoveButton.Opacity = 1.0;
        ConnectButton.Opacity = 1.0;
        // Set Clock and Rewind Buttons to disabled
        ClockButton.IsEnabled = false;
        RewindButton.IsEnabled = false;
        SettingsButton.IsEnabled = false;
        ReaderButton.IsEnabled = false;
        ClockButton.Opacity = 0.2;
        RewindButton.Opacity = 0.2;
        SettingsButton.Opacity = 0.2;
        ReaderButton.Opacity = 0.2;
        Application.Current!.Resources.TryGetResource("play_regular", null, out object? icon);
        ConnectButton.Content = new PathIcon()
        {
            Data = (StreamGeometry?)icon,
        };
        ConnectButton.Tag = "connect";
    }

    private void SetWorking()
    {
        ReaderType.IsEnabled = false;
        ReaderIp.IsEnabled = false;
        ReaderPort.IsEnabled = false;
        ReaderLocation.IsEnabled = false;
        ClockButton.IsEnabled = false;
        RewindButton.IsEnabled = false;
        ConnectButton.IsEnabled = false;
        RemoveButton.IsEnabled = false;
        SettingsButton.IsEnabled = false;
        ReaderButton.IsEnabled = false;
        ClockButton.Opacity = 0.2;
        RewindButton.Opacity = 0.2;
        ConnectButton.Opacity = 0.2;
        RemoveButton.Opacity = 0.2;
        SettingsButton.Opacity = 0.2;
        ReaderButton.Opacity = 0.2;
        ConnectButton.Tag = "working";
    }

    private void ChangeReadingStatus(string status)
    {
        ReaderButton.Foreground = status switch
        {
            TimingSystem.READING_STATUS_STOPPED => new SolidColorBrush(Colors.Red),
            TimingSystem.READING_STATUS_READING => new SolidColorBrush(Colors.LimeGreen),
            TimingSystem.READING_STATUS_PARTIAL => new SolidColorBrush(Colors.Violet),
            _ => null
        };
    }

    internal void UpdateSystemType(string type)
    {
        Reader.UpdateSystemType(type);
        ReaderPort.Text = Reader.Port.ToString();
    }

    private void ReaderType_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Reader type has changed.");
        string type = (string)((ComboBoxItem)ReaderType.SelectedItem!).Tag!;
        Log.D("UI.MainPages.TimingPage", $"Updating to type: {Readers.SYSTEM_NAMES[type]}");
        Reader.UpdateSystemType(type);
        ReaderPort.Text = Reader.Port.ToString();
        ReaderPort.IsEnabled = Readers.SYSTEM_CHRONOKEEP_PORTAL != type;
    }

    private void SelectAll(object? sender, FocusChangedEventArgs e)
    {
        TextBox src = (TextBox)e.Source!;
        src.SelectAll();
    }

    private void IpValidation(object? sender, TextInputEventArgs e)
    {
        e.Handled = AllowedChars().IsMatch(e.Text!);
    }

    private void NumberValidation(object? sender, TextInputEventArgs e)
    {
        e.Handled = AllowedNums().IsMatch(e.Text!);
    }

    private void Rewind_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", $"Settings button pressed. IP is {ReaderIp.Text}");
        parent.OpenRewindWindow(Reader);
    }

    private void Clock_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", $"Clock button pressed. IP is {ReaderIp.Text}");
        parent.OpenTimeWindow(Reader);
    }

    private void Settings_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Reader.SystemInterface == null)
        {
            return;
        }
        if (Reader.SystemInterface.SettingsEditable())
        {
            Reader.SystemInterface.OpenSettings();
        }
        else
        {
            DialogBox.AsyncShow("Settings not yet implemented.");
        }
    }

    private void Readers_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Reader.SystemInterface == null)
        {
            return;
        }
        switch (Reader.SystemStatus)
        {
            case TimingSystem.READING_STATUS_READING:
            case TimingSystem.READING_STATUS_PARTIAL:
                Reader.SystemInterface.StopReading();
                return;
            case TimingSystem.READING_STATUS_STOPPED:
                Reader.SystemInterface.StartReading();
                return;
            default:
                return;
        }
    }

    private void Connect_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if ("connect" != (string)ConnectButton.Tag!)
        {
            Log.D("UI.MainPages.TimingPage", "Disconnect pressed.");
            Reader.Status = SYSTEM_STATUS.WORKING;
            parent.DisconnectSystem(Reader);
            UpdateStatus();
            Reader.SystemInterface!.CloseSettings();
            return;
        }
        Log.D("UI.MainPages.TimingPage", $"Connect button pressed. IP is {ReaderIp.Text}");
        // Check if IP is a valid IP address
        if (!IpPattern().IsMatch(ReaderIp.Text!.Trim()))
        {
            DialogBox.AsyncShow("IP address given not valid.");
            return;
        }
        Reader.IpAddress = ReaderIp.Text.Trim();
        // Check if Port is valid.
        _ = int.TryParse(ReaderPort.Text!.Trim(), out int portNo);
        if (portNo is < 0 or > 65535)
        {
            DialogBox.AsyncShow("Port given not valid.");
            return;
        }
        Reader.Port = portNo;
        Reader.LocationId = Convert.ToInt32(((ComboBoxItem)ReaderLocation.SelectedItem!).Tag!);
        Reader.LocationName = ((ComboBoxItem)ReaderLocation.SelectedItem).Content!.ToString()!;
        Reader.Status = SYSTEM_STATUS.WORKING;
        parent.ConnectSystem(Reader);
        UpdateStatus();
    }

    private void Remove_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Remove button for a timing system has been clicked.");
        if (Reader.Saved())
        {
            parent.RemoveSystem(Reader);
        }
    }
}
