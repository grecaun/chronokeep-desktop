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
using Avalonia.Interactivity;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.UI.Util;
using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Threading;

namespace Chronokeep.UI.UhfRfidReader;

public partial class ChipReaderWindow : ChronokeepWindow
{
    private static Thread? readingThread;
    private static NewReader? reader;
    private static int readNo = 1;
    private RfidSerial? serial;
    private static ChipPersonWindow? personWindow;
    private readonly IdbInterface database;
    private readonly IWindowCallback? window;
    private readonly int eventId;

    private readonly ObservableCollection<RfidInfo> chipInfo = [];

    private ChipReaderWindow(IWindowCallback window, IdbInterface database)
    {
        InitializeComponent();
        ChronokeepInitialize();
        InstantiateSerialPortList();
        reader = new NewReader(this);
        this.window = window;
        this.database = database;
        Event theEvent = database.GetCurrentEvent() ?? throw new Exception("no event set");
        eventId = theEvent.Identifier;
        EventNameHolder.IsVisible = true;
        EventName.Text = theEvent.Name;
        ChipNumbers.ItemsSource = chipInfo;
    }

    public static ChipReaderWindow NewWindow(IWindowCallback window, IdbInterface database)
    {
        return new ChipReaderWindow(window, database);
    }

    internal void PersonWindowClosing()
    {
        personWindow = null;
        BeautyBtn.Content = "Show Info Window";
    }

    private void InstantiateSerialPortList()
    {
        SerialPortCb.Items.Clear();
        string[]? ports = SerialPort.GetPortNames();
        foreach (string port in ports)
        {
            SerialPortCb.Items.Add(port);
        }
        if (SerialPortCb.Items.Count > 0)
        {
            SerialPortCb.SelectedIndex = 0;
        }
    }

    private void KillReader()
    {
        serial?.Disconnect();
        chipInfo.Add(new RfidInfo { DecNumber = -1 });
        reader?.Kill();
        readingThread?.Join(TimeSpan.FromSeconds(1));
        readingThread = null;
    }

    internal void AddRfidItem(RfidInfo read)
    {
        Application.Current!.Dispatcher.Invoke(delegate
        {
            read.ReadNumber = readNo++;
            chipInfo.Add(read);
            if (personWindow == null) return;
            string chip = database.GetAppSetting(Constants.Settings.DEFAULT_CHIP_TYPE)!.Value.Equals(Constants.Settings.CHIP_TYPE_DEC) ? read.DecNumber.ToString() : read.HexNumber;
            Participant person = database.GetParticipantChip(eventId, chip)!;
            personWindow.UpdateInfo(person, chip);
        });
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        try
        {
            personWindow?.Close();
        }
        catch
        {
            Log.E("ChipReaderWindow", "Window not open.");
        }
        try
        {
            KillReader();
        }
        catch
        {
            Log.E("ChipReaderWindow", "Things are already closed.");
        }
        window?.WindowFinalize();
    }

    private void RefreshBtn_Click(object? sender, RoutedEventArgs e)
    {
        InstantiateSerialPortList();
    }

    private void ConnectBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (ConnectBtn.Content!.Equals("Connect"))
        {
            if (SerialPortCb.SelectedIndex >= 0)
            {
                serial = new RfidSerial(SerialPortCb.Text!, 9600);
                reader!.SetSerial(serial);
            }
            else
            {
                DialogBox.AsyncShow("No serial port selected.");
                return;
            }
            if (serial.Connect() != RfidError.NO_ERROR)
            {
                DialogBox.AsyncShow("Unable to connect to device.");
                return;
            }
            ConnectBtn.Content = "Disconnect";
            BeautyBtn.IsVisible = true;
            BeautyBtn.Content = "Show Info Window";
            chipInfo.Add(new RfidInfo { DecNumber = 0 });
            readingThread = new Thread(reader.Run);
            readingThread.Start();
        }
        else
        {
            ConnectBtn.Content = "Connect";
            BeautyBtn.IsVisible = false;
            BeautyBtn.Content = "Show Info Window";
            try
            {
                KillReader();
            }
            catch
            {
                DialogBox.AsyncShow("Something went wrong during disconnect.");
            }
            personWindow?.Close();
        }
    }

    private void BeautyBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (personWindow == null)
        {
            Event thisEvent = database.GetEvent(eventId)!;
            personWindow = new ChipPersonWindow(this, thisEvent.Date);
            personWindow.Show();
            BeautyBtn.Content = "Close Info Window";
        }
        else
        {
            personWindow.Close();
            personWindow = null;
        }
    }

    protected override void SetMaximizeIcon()
    {
        MaximizeIcon?.IsVisible = WindowState == WindowState.Normal;
        UnMaximizeIcon?.IsVisible = WindowState == WindowState.Maximized;        
    }

    protected override void Maximize()
    {
        WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
    }

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}
