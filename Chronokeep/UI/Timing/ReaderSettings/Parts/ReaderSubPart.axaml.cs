using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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

    public ReaderSubPart(PortalReader reader, ChronokeepInterface readerInterface)
    {
        InitializeComponent();
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
        for (int ix = 0; ix < reader.Antennas.Length; ix++)
        {
            // TODO -- update border background with correct coloring
            if (reader.Antennas[ix] != Constants.Readers.CHRONOKEEP_ANTENNA_STATUS_NONE)
            {
                AntennaPanel.Children.Add(new Border()
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
                });
            }
        }
    }

    public string GetReaderName()
    {
        return reader.Name;
    }

    public void UpdateAntennas(int[] antennas)
    {
        reader.Antennas = antennas;
        AntennaPanel.Children.Clear();
        for (int ix = 0; ix < reader.Antennas.Length; ix++)
        {
            if (reader.Antennas[ix] != Constants.Readers.CHRONOKEEP_ANTENNA_STATUS_NONE)
            {
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
                });
            }
        }
    }

    public void UpdateReader(PortalReader iReader)
    {
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
        AntennaPanel.Children.Clear();
        for (int ix = 0; ix < iReader.Antennas.Length; ix++)
        {
            if (reader.Antennas[ix] != Constants.Readers.CHRONOKEEP_ANTENNA_STATUS_NONE)
            {
                AntennaPanel.Children.Add(new Border()
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
                });
            }
        }
    }

    private void KindBox_ValueChanged(object? sender, SelectionChangedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", $"Changing port for reader {reader.Id}");
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
        e.Handled = AllowedChars().IsMatch(e.Text!);
    }

    private void NumberValidation(object? sender, TextInputEventArgs e)
    {
        e.Handled = AllowedNums().IsMatch(e.Text!);
    }

    private void DeleteReader(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", $"Deleting reader {reader.Id}");
        readerInterface.SendRemoveReader(reader);
    }

    private void SaveReader(object? sender, RoutedEventArgs e)
    {
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
        reader.IpAddress = !IpPattern().IsMatch(IpBox.Text!.Trim()) ? "" : IpBox.Text.Trim();
        _ = uint.TryParse(PortBox.Text!.Trim(), out uint portNo);
        if (portNo > 65535)
        {
            portNo = 0;
        }
        reader.Port = portNo;
        reader.AutoConnect = AutoConnectSwitch.IsChecked == true;

        readerInterface.SendSaveReader(reader);
    }
}