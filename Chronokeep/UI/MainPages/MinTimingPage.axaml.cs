using Avalonia.Controls;
using Chronokeep.Constants;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.UI.MainPages.Timing;
using Chronokeep.UI.Parts;
using Chronokeep.UI.Timing.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;

namespace Chronokeep.UI.MainPages;

public partial class MinTimingPage : UserControl, IMainPage, ITimingPage
{
    private readonly IMainWindow mWindow;
    private readonly IDBInterface database;
    private TimingRawReadsPage? subPage;

    private Event? theEvent;
    private List<TimingLocation>? locations;

    private SetTimeWindow? timeWindow;
    private RewindWindow? rewindWindow;

    private int total = 4, connected;

    private const string Ipformat = "{0:D}.{1:D}.{2:D}.{3:D}";
    private readonly int[] baseIp = [0, 0, 0, 0];

    public MinTimingPage(IMainWindow window, IDBInterface database)
    {
        InitializeComponent();
        this.database = database;
        mWindow = window;
        theEvent = database.GetCurrentEvent();

        // Check for default IP address to give to our reader boxes for connections
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter is not
                {
                    NetworkInterfaceType: NetworkInterfaceType.Ethernet, OperationalStatus: OperationalStatus.Up
                }) continue;
            if (adapter.GetIPProperties().GatewayAddresses.FirstOrDefault() == null) continue;
            foreach (UnicastIPAddressInformation ipinfo in adapter.GetIPProperties().UnicastAddresses)
            {
                if (ipinfo.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                Log.D("UI.MainPages.TimingPage", "IP Address :" + ipinfo.Address);
                Log.D("UI.MainPages.TimingPage", "IPv4 Mask  :" + ipinfo.IPv4Mask);
                string[] ipParts = ipinfo.Address.ToString().Split('.');
                string[] maskParts = ipinfo.IPv4Mask.ToString().Split('.');
                if (ipParts.Length != 4 || maskParts.Length != 4) continue;
                for (int i = 0; i < 4; i++)
                {
                    int ip, mask;
                    try
                    {
                        ip = Convert.ToInt32(ipParts[i]);
                        mask = Convert.ToInt32(maskParts[i]);
                    }
                    catch
                    {
                        ip = 0;
                        mask = 0;
                    }
                    baseIp[i] = ip & mask;
                }
            }
        }

        if (theEvent == null || theEvent.Identifier == -1)
        {
            return;
        }

        // Populate the list of readers with connected readers (or at least 4 readers)
        ReadersBox.Items.Clear();
        locations = database.GetTimingLocations(theEvent.Identifier);
        if (!theEvent.CommonStartFinish)
        {
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_ANNOUNCER, theEvent.Identifier, "Announcer", 0, 0));
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Finish", theEvent.FinishMaxOccurrences, theEvent.FinishIgnoreWithin));
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_START, theEvent.Identifier, "Start", 0, theEvent.StartWindow));
        }
        else
        {
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_ANNOUNCER, theEvent.Identifier, "Announcer", 0, 0));
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Start/Finish", theEvent.FinishMaxOccurrences, theEvent.FinishIgnoreWithin));
        }
        List<TimingSystem> systems = mWindow.GetConnectedSystems();
        int numSystems = systems.Count;
        string system;
        try
        {
            system = database.GetAppSetting(Settings.DEFAULT_TIMING_SYSTEM)!.Value;
        }
        catch
        {
            Log.D("UI.MainPages.TimingPage", "Error fetching default timing system information.");
            system = Readers.DEFAULT_TIMING_SYSTEM;
        }
        if (numSystems < 3)
        {
            Log.D("UI.MainPages.TimingPage", systems.Count + " systems found.");
            for (int i = 0; i < 3 - numSystems; i++)
            {
                systems.Add(new(string.Format(Ipformat, baseIp[0], baseIp[1], baseIp[2], baseIp[3]), system));
            }
        }
        systems.Add(new(string.Format(Ipformat, baseIp[0], baseIp[1], baseIp[2], baseIp[3]), system));
        connected = 0;
        foreach (TimingSystem sys in systems)
        {
            ReadersBox.Items.Add(new ReaderPart(this, sys, locations));
            if (sys.Status == SYSTEM_STATUS.CONNECTED || sys.Status == SYSTEM_STATUS.WORKING)
            {
                connected++;
            }
        }
        total = ReadersBox.Items.Count;
        subPage = new TimingRawReadsPage(this, database, mWindow);
        TimingFrame.Content = subPage;
    }

    public void Keyboard_Ctrl_A() { }

    public void Keyboard_Ctrl_S() { }

    public void Keyboard_Ctrl_Z() { }

    public static void UpdateDatabase() { }

    public void Closing() { }

    public void UpdateView()
    {
        Log.D("UI.MainPages.TimingPage", "Updating timing information.");
        theEvent = database.GetCurrentEvent();
        if (theEvent == null || theEvent.Identifier == -1)
        {
            subPage = null;
            TimingFrame.Content = subPage;
            TimingFrame.IsVisible = false;
            // Something went wrong and this shouldn't be visible.
            return;
        }
        if (subPage == null)
        {
            subPage = new TimingRawReadsPage(this, database, mWindow);
            TimingFrame.Content = subPage;
            TimingFrame.IsVisible = true;
        }

        // Get updated list of locations
        locations = database.GetTimingLocations(theEvent.Identifier);
        if (!theEvent.CommonStartFinish)
        {
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_ANNOUNCER, theEvent.Identifier, "Announcer", 0, 0));
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Finish", theEvent.FinishMaxOccurrences, theEvent.FinishIgnoreWithin));
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_START, theEvent.Identifier, "Start", 0, theEvent.StartWindow));
        }
        else
        {
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_ANNOUNCER, theEvent.Identifier, "Announcer", 0, 0));
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Start/Finish", theEvent.FinishMaxOccurrences, theEvent.FinishIgnoreWithin));
        }

        // Update locations in the list of readers
        connected = 0; total = ReadersBox.Items.Count;
        foreach (object? read in ReadersBox.Items)
        {
            if (read is not ReaderPart part) continue;
            part.UpdateLocations(locations);
            part.UpdateStatus();
            connected += part.Reader.Status == SYSTEM_STATUS.DISCONNECTED ? 0 : 1;
            if (part.Reader.Status != SYSTEM_STATUS.DISCONNECTED) continue;
            if (timeWindow != null && timeWindow.IsTimingSystem(part.Reader))
            {
                timeWindow.Close();
                timeWindow = null;
            }
            if (rewindWindow == null || !rewindWindow.IsTimingSystem(part.Reader)) continue;
            rewindWindow.Close();
            rewindWindow = null;
        }
        if (total < 4)
        {
            string system;
            try
            {
                system = database.GetAppSetting(Settings.DEFAULT_TIMING_SYSTEM)!.Value;
            }
            catch
            {
                Log.D("UI.MainPages.TimingPage", "Error fetching default timing system information.");
                system = Readers.DEFAULT_TIMING_SYSTEM;
            }
            for (int i = total; i < 4; i++)
            {
                ReadersBox.Items.Add(new ReaderPart(
                    this,
                    new(
                        string.Format(Ipformat, baseIp[0], baseIp[1], baseIp[2], baseIp[3]),
                        system),
                        locations));
            }
        }

        subPage.SafemodeUpdateView();
    }

    public void DatasetChanged()
    {
        mWindow.NotifyTimingWorker();
    }

    public void NotifyTimingWorker()
    {
        mWindow.NotifyTimingWorker();
    }

    public void NewMessage()
    {
        timeWindow?.UpdateTime();
    }

    public void OpenTimeWindow(TimingSystem system)
    {
        Log.D("UI.MainPages.TimingPage", "Opening Set Time Window.");
        timeWindow = new(this, system);
        timeWindow.ShowDialog((Window)mWindow);
    }

    public void CloseTimeWindow()
    {
        timeWindow = null;
    }

    public void OpenRewindWindow(TimingSystem system)
    {
        Log.D("UI.MainPages.TimingPage", "Opening Rewind Window.");
        rewindWindow = new RewindWindow(system, this);
        rewindWindow.ShowDialog((Window)mWindow);
    }

    public void CloseRewindWindow()
    {
        rewindWindow = null;
    }

    public void SetAllTimingSystemsToTime(DateTime time, bool now)
    {
        List<TimingSystem> systems = mWindow.GetConnectedSystems();
        foreach (TimingSystem sys in systems)
        {
            sys.SystemInterface?.SetTime(now ? DateTime.Now : time);
        }
    }

    public void RemoveSystem(TimingSystem sys)
    {
        ReaderPart? removed = null;
        foreach (ReaderPart? box in ReadersBox.Items.Cast<ReaderPart?>())
        {
            if (box!.Reader.SystemIdentifier == sys.SystemIdentifier && sys.Saved())
            {
                removed = box;
                break;
            }
        }
        ReadersBox.Items.Remove(removed);
        UpdateView();
    }

    public bool ConnectSystem(TimingSystem sys)
    {
        mWindow.ConnectTimingSystem(sys);
        if (sys.Status == SYSTEM_STATUS.CONNECTED || sys.Status == SYSTEM_STATUS.WORKING)
        {
            connected++;
        }
        Log.D("UI.MainPages.TimingPage", connected + " systems connected or trying to connect.");
        if (connected >= total)
        {
            string system;
            try
            {
                system = database.GetAppSetting(Settings.DEFAULT_TIMING_SYSTEM)!.Value;
            }
            catch
            {
                Log.D("UI.MainPages.TimingPage", "Error fetching default timing system information.");
                system = Readers.DEFAULT_TIMING_SYSTEM;
            }
            ReadersBox.Items.Add(new ReaderPart(this, new(string.Format(Ipformat, baseIp[0], baseIp[1], baseIp[2], baseIp[3]), system), locations!));
            total = ReadersBox.Items.Count;
        }
        return sys.Status != SYSTEM_STATUS.DISCONNECTED;
    }

    public bool DisconnectSystem(TimingSystem sys)
    {
        mWindow.DisconnectTimingSystem(sys);
        if (sys.Status == SYSTEM_STATUS.DISCONNECTED)
        {
            connected--;
        }
        Log.D("UI.MainPages.TimingPage", connected + " systems connected or trying to connect/disconnect.");
        return sys.Status == SYSTEM_STATUS.DISCONNECTED;
    }

    public string GetSearchValue() { return ""; }

    public SortType GetSortType()
    {
        return SortType.SYSTIME;
    }

    public void LoadMainDisplay() { }

    public PeopleType GetPeopleType()
    {
        return PeopleType.DEFAULT;
    }

    public string GetLocation() { return ""; }

    public string GetReader() { return ""; }

    public void SetReaders(string[] readers, bool visible) { }
}