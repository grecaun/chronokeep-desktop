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

public partial class ChipReaderWindow : Window
{
    private static Thread? readingThread;
    private static NewReader? reader;
    private static int ReadNo = 1;
    RFIDSerial? serial;
    private static ChipPersonWindow? personWindow = null;
    private readonly IDBInterface database;
    private readonly IWindowCallback? window = null;
    private readonly int eventId = -1;

    private readonly ObservableCollection<RFIDInfo> chipInfo = [];

    public ChipReaderWindow(IWindowCallback window, IDBInterface database)
    {
        InitializeComponent();
        InstantiateSerialPortList();
        reader = new NewReader(this);
        this.window = window;
        this.database = database;
        Event theEvent = database.GetCurrentEvent() ?? throw new Exception("no event set");
        eventId = theEvent.Identifier;
        EventNameHolder.IsVisible = true;
        eventName.Text = theEvent.Name;
        chipNumbers.ItemsSource = chipInfo;
    }

    public static ChipReaderWindow NewWindow(IWindowCallback window, IDBInterface database)
    {
        return new ChipReaderWindow(window, database);
    }

    internal void PersonWindowClosing()
    {
        personWindow = null;
        beautyBtn.Content = "Show Info Window";
    }

    public void InstantiateSerialPortList()
    {
        serialPortCB.Items.Clear();
        var Ports = SerialPort.GetPortNames();
        foreach (string port in Ports)
        {
            serialPortCB.Items.Add(port);
        }
        if (serialPortCB.Items.Count > 0)
        {
            serialPortCB.SelectedIndex = 0;
        }
    }

    internal void KillReader()
    {
        serial?.Disconnect();
        chipInfo.Add(new RFIDInfo { DecNumber = -1 });
        reader?.Kill();
        readingThread?.Join(TimeSpan.FromSeconds(1));
        readingThread = null;
    }

    internal void AddRFIDItem(RFIDInfo read)
    {
        Application.Current!.Dispatcher.Invoke(new Action(delegate ()
        {
            read.ReadNumber = ReadNo++;
            chipInfo.Add(read);
            if (personWindow != null)
            {
                string chip = database.GetAppSetting(Constants.Settings.DEFAULT_CHIP_TYPE)!.Value.Equals(Constants.Settings.CHIP_TYPE_DEC) ? read.DecNumber.ToString() : read.HexNumber;
                Participant person = database.GetParticipantChip(eventId, chip)!;
                personWindow.UpdateInfo(person, chip);
            }
        }));
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
        window?.WindowFinalize(this);
    }

    private void RefreshBtn_Click(object? sender, RoutedEventArgs e)
    {
        InstantiateSerialPortList();
    }

    private void ConnectBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (connectBtn.Content!.Equals("Connect"))
        {
            if (serialPortCB.SelectedIndex >= 0)
            {
                serial = new RFIDSerial(serialPortCB.Text!, 9600);
                reader!.SetSerial(serial);
            }
            else
            {
                DialogBox.Show("No serial port selected.");
                return;
            }
            if (serial.Connect() != RFIDError.NOERR)
            {
                DialogBox.Show("Unable to connect to device.");
                return;
            }
            connectBtn.Content = "Disconnect";
            beautyBtn.IsVisible = true;
            beautyBtn.Content = "Show Info Window";
            chipInfo.Add(new RFIDInfo { DecNumber = 0 });
            readingThread = new Thread(new ThreadStart(reader.Run));
            readingThread.Start();
        }
        else
        {
            connectBtn.Content = "Connect";
            beautyBtn.IsVisible = false;
            beautyBtn.Content = "Show Info Window";
            try
            {
                KillReader();
            }
            catch
            {
                DialogBox.Show("Something went wrong during disconnect.");
            }
            personWindow?.Close();
        }
    }

    private void BeautyBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (personWindow == null)
        {
            Event thisEvent = database.GetEvent(eventId)!;
            personWindow = new ChipPersonWindow(this, thisEvent!.Date);
            personWindow.Show();
            beautyBtn.Content = "Close Info Window";
        }
        else
        {
            personWindow.Close();
            personWindow = null;
        }
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