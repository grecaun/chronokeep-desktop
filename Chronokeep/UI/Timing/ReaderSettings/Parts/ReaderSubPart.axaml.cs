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
using Avalonia.Media;
using Chronokeep.Helpers;
using Chronokeep.Objects.ChronokeepPortal;
using Chronokeep.Timing.Interfaces;
using Chronokeep.UI.Util;
using System.Text.RegularExpressions;

namespace Chronokeep.UI.Timing.ReaderSettings.Parts;

public partial class ReaderSubPart : UserControl
{
    private PortalReader reader;
    private readonly ChronokeepInterface readerInterface;

    [GeneratedRegex("^([01]?[0-9]?[0-9]|2[0-4][0-9]|25[0-5])\\.([01]?[0-9]?[0-9]|2[0-4][0-9]|25[0-5])\\.([01]?[0-9]?[0-9]|2[0-4][0-9]|25[0-5])\\.([01]?[0-9]?[0-9]|2[0-4][0-9]|25[0-5])$")]
    private static partial Regex IpPattern();
    [GeneratedRegex("[^0-9.]")]
    private static partial Regex AllowedChars();
    [GeneratedRegex("[^0-9]")]
    private static partial Regex AllowedNums();

    private readonly bool loaded = false;

    public ReaderSubPart(PortalReader reader, ChronokeepInterface readerInterface)
    {
        InitializeComponent();
        // Filter typing before the box adds the text; a XAML TextInput handler runs too late to block it.
        IpBox.FilterTyping(IpValidation);
        PortBox.FilterTyping(NumberValidation);
        this.reader = reader;
        this.readerInterface = readerInterface;
        NameBox.Text = reader.Name;
        KindBox.SelectedIndex = reader.Kind.Equals(PortalReader.READER_KIND_ZEBRA) ? 0
                    //: reader.Kind.Equals(PortalReader.READER_KIND_IMPINJ) ? 1 
                    //: reader.Kind.Equals(PortalReader.READER_KIND_RFID) ? 2 
                    : -1;
        IpBox.Text = reader.IpAddress;
        PortBox.Text = reader.Port.ToString();
        AutoConnectSwitch.IsChecked = reader.AutoConnect;
        ConnectedSwitch.IsChecked = reader.Connected;
        ShowAntennas();
        loaded = true;
    }

    public string GetReaderName()
    {
        if (!loaded) return "";
        return reader.Name;
    }

    /// <summary>Shows a circle for each antenna in use: green when connected, red when not.</summary>
    private void ShowAntennas()
    {
        AntennaPanel.Children.Clear();
        for (int ix = 0; ix < reader.Antennas.Length; ix++)
        {
            if (reader.Antennas[ix] == Constants.Readers.CHRONOKEEP_ANTENNA_STATUS_NONE) continue;
            AntennaPanel.Children.Add(new Border
            {
                Child = new TextBlock()
                {
                    Text = (ix + 1).ToString(),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                },
                Width = 30,
                Height = 30,
                CornerRadius = Avalonia.CornerRadius.Parse("15"),
                Background = Constants.Readers.CHRONOKEEP_ANTENNA_STATUS_CONNECTED == reader.Antennas[ix] ? Brush.Parse("Green") : Brush.Parse("Red"),
            });
        }
    }

    public void UpdateAntennas(int[] antennas)
    {
        if (!loaded) return;
        reader.Antennas = antennas;
        ShowAntennas();
    }

    public void UpdateReader(PortalReader iReader)
    {
        if (!loaded) return;
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", $"Updating reader {iReader.Id}");
        reader = iReader;
        NameBox.Text = iReader.Name;
        KindBox.SelectedIndex = iReader.Kind switch
        {
            PortalReader.READER_KIND_ZEBRA => 0,
            PortalReader.READER_KIND_IMPINJ => 1,
            PortalReader.READER_KIND_RFID => 2,
            _ => -1,
        };
        IpBox.Text = iReader.IpAddress;
        PortBox.Text = iReader.Port.ToString();
        AutoConnectSwitch.IsChecked = iReader.AutoConnect;
        ConnectedSwitch.IsChecked = iReader.Connected;
        ConnectedSwitch.IsEnabled = true;
        ShowAntennas();
    }

    private void KindBox_ValueChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!loaded) return;
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", $"Changing port for reader {reader?.Id}");
        switch (KindBox.SelectedIndex)
        {
            case 0:
                PortBox.Text = PortalReader.READER_DEFAULT_PORT_ZEBRA;
                break;
            /*case 1:
                PortBox.Text = PortalReader.READER_DEFAULT_PORT_IMPINJ;
                break;
            case 2:
                PortBox.Text = PortalReader.READER_DEFAULT_PORT_RFID;
                break;//*/
            default:
                PortBox.Text = "";
                return;
        }
    }

    private void IpValidation(object? sender, TextInputEventArgs e)
    {
        if (!loaded) return;
        e.Handled = AllowedChars().IsMatch(e.Text!);
    }

    private void NumberValidation(object? sender, TextInputEventArgs e)
    {
        if (!loaded) return;
        e.Handled = AllowedNums().IsMatch(e.Text!);
    }

    private void DeleteReader(object? sender, RoutedEventArgs e)
    {
        if (!loaded) return;
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", $"Deleting reader {reader.Id}");
        readerInterface.SendRemoveReader(reader);
    }

    private void SaveReader(object? sender, RoutedEventArgs e)
    {
        if (!loaded) return;
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", $"Saving reader {reader.Id}");
        reader.Name = NameBox.Text!.Trim();
        switch (KindBox.SelectedIndex)
        {
            case 0:
                reader.Kind = PortalReader.READER_KIND_ZEBRA;
                break;
            /*case 1:
                reader.Kind = PortalReader.READER_KIND_IMPINJ;
                break;
            case 2:
                reader.Kind = PortalReader.READER_KIND_RFID;
                break;//*/
            default:
                DialogBox.AsyncShow("Unknown kind specified. Unable to save.");
                return;
        }
        // Refuse an address or port the portal can't connect to rather than saving it blank.
        string ip = IpBox.Text?.Trim() ?? "";
        if (!IpPattern().IsMatch(ip))
        {
            DialogBox.AsyncShow("IP address given not valid.");
            return;
        }
        if (!uint.TryParse(PortBox.Text?.Trim(), out uint portNo) || portNo is 0 or > 65535)
        {
            DialogBox.AsyncShow("Port given not valid.");
            return;
        }
        reader.IpAddress = ip;
        reader.Port = portNo;
        reader.AutoConnect = AutoConnectSwitch.IsChecked == true;

        readerInterface.SendSaveReader(reader);
    }

    private void ConnectReader(object?sender, RoutedEventArgs e)
    {
        // Follow the switch: off stops the reader, on starts it.
        if (ConnectedSwitch.IsChecked != true)
        {
            readerInterface.SendStopReader(reader);
        }
        else
        {
            readerInterface.SendStartReader(reader);
        }
    }
}
