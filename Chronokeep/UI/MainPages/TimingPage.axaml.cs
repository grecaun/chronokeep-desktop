using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Chronokeep.Constants;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.IO;
using Chronokeep.IO.HtmlTemplates;
using Chronokeep.Network.API;
using Chronokeep.Objects;
using Chronokeep.Objects.Notifications;
using Chronokeep.Timing.API;
using Chronokeep.Timing.Remote;
using Chronokeep.UI.API.Windows;
using Chronokeep.UI.Export;
using Chronokeep.UI.MainPages.Timing;
using Chronokeep.UI.Parts;
using Chronokeep.UI.Timing.Import;
using Chronokeep.UI.Timing.Notifications;
using Chronokeep.UI.Timing.Windows;
using Chronokeep.UI.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static Chronokeep.Helpers.Globals;

namespace Chronokeep.UI.MainPages;

public partial class TimingPage : UserControl, IMainPage, ITimingPage
{
    private readonly IMainWindow mWindow;
    private readonly IDBInterface database;
    private ISubPage? subPage;

    private CancellationTokenSource? cts;

    private readonly Event? theEvent;
    private readonly List<TimingLocation>? locations;

    private DateTime startTime;
    private readonly DispatcherTimer Timer = new();
    private bool TimerStarted = false;
    private SetTimeWindow? timeWindow = null;
    private RewindWindow? rewindWindow = null;

    private static bool alreadyRecalculating = false;
    private static readonly int uploadTimer = 1000;

    private readonly ObservableCollection<DistanceStat> stats = [];

    private int total = 0, known = 0;

    private const string ipformat = "{0:D}.{1:D}.{2:D}.{3:D}";
    private readonly int[] baseIP = [0, 0, 0, 0];

    private readonly bool remote_api = false;

    private readonly Dictionary<int, (long seconds, int milliseconds)> waveTimes = [];
    private readonly HashSet<int> waves = [];
    private int selectedWave = -1;
    private readonly List<TimeRelativeWave> relativeToWaveList = [];

    [GeneratedRegex(@"[^a-z0-9\-]")]
    private static partial Regex FileSaveRegex();

    private readonly bool loaded = false;

    public TimingPage(IMainWindow window, IDBInterface database)
    {
        InitializeComponent();
        this.database = database;
        this.mWindow = window;
        theEvent = database.GetCurrentEvent();
        ViewOnlyBox.SelectedIndex = 0;
        SortBy.SelectedIndex = 0;
        loaded = true;

        if (theEvent == null || theEvent.Identifier == -1)
        {
            return;
        }

        // Setup the running clock.
        Timer.Tick += new EventHandler(Timer_Tick);
        Timer.Interval = new TimeSpan(0, 0, 0, 0, 100);

        string webAddress = string.Format("{0}:{1}", "localhost", 6933);
        WebBlock.Text = webAddress;
        WebButton.NavigateUri = new(webAddress);
        // Check for default IP address to give to our reader boxes for connections
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet && adapter.OperationalStatus == OperationalStatus.Up)
            {
                if (adapter.GetIPProperties().GatewayAddresses.FirstOrDefault() != null)
                {
                    foreach (UnicastIPAddressInformation ipinfo in adapter.GetIPProperties().UnicastAddresses)
                    {
                        if (ipinfo.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            webAddress = string.Format("http://{0}:{1}", ipinfo.Address.ToString(), 6933);
                            WebBlock.Text = webAddress;
                            WebButton.NavigateUri = new(webAddress);
                            Log.D("UI.MainPages.TimingPage", "IP Address :" + ipinfo.Address);
                            Log.D("UI.MainPages.TimingPage", "IPv4 Mask  :" + ipinfo.IPv4Mask);
                            string[] ipParts = ipinfo.Address.ToString().Split('.');
                            string[] maskParts = ipinfo.IPv4Mask.ToString().Split('.');
                            if (ipParts.Length == 4 && maskParts.Length == 4)
                            {
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
                                    baseIP[i] = ip & mask;
                                }
                            }
                        }
                    }
                }
            }
        }

        // Check for multiple wave times, show an ellapsed relative to box if so
        waves.Clear();
        waveTimes.Clear();
        relativeToWaveList.Add(new()
        {
            Name = "Start Time",
            Wave = -1
        });
        foreach (Distance div in database.GetDistances(theEvent!.Identifier))
        {
            relativeToWaveList.Add(new()
            {
                Name = div.Name + " (Wave " + div.Wave + ")",
                Wave = div.Wave
            });
            waveTimes[div.Wave] = (div.StartOffsetSeconds, div.StartOffsetMilliseconds);
            waves.Add(div.Wave);
        }
        EllapsedRelativeToBox.ItemsSource = relativeToWaveList;
        EllapsedRelativeToBox.SelectedIndex = 0;

        // Check if we've already started the event.  Show a clock if we have.
        if (theEvent != null && theEvent.StartSeconds >= 0)
        {
            StartTime.Text = Constants.Timing.ToTimeOfDay(theEvent.StartSeconds, theEvent.StartMilliseconds);
            UpdateStartTime();
        }

        // Populate the list of readers with connected readers (or at least 4 readers)
        ReadersBox.Items.Clear();
        locations = database.GetTimingLocations(theEvent!.Identifier);
        int locCount = locations.Count;
        if (!theEvent.CommonStartFinish)
        {
            locations.Insert(0, new(Constants.Timing.LOCATION_ANNOUNCER, theEvent.Identifier, "Announcer", 0, 0));
            locations.Insert(0, new(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Finish", theEvent.FinishMaxOccurrences, theEvent.FinishIgnoreWithin));
            locations.Insert(0, new(Constants.Timing.LOCATION_START, theEvent.Identifier, "Start", 0, theEvent.StartWindow));
        }
        else
        {
            locations.Insert(0, new(Constants.Timing.LOCATION_ANNOUNCER, theEvent.Identifier, "Announcer", 0, 0));
            locations.Insert(0, new(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Start/Finish", theEvent.FinishMaxOccurrences, theEvent.FinishIgnoreWithin));
        }

        LocationBox.Items.Clear();
        if (locCount > 0)
        {
            LocationBox.Items.Add(new ComboBoxItem()
            {
                Content = "All Locations"
            });
            foreach (TimingLocation loc in locations)
            {
                if (!loc.Name.Equals("Announcer", StringComparison.OrdinalIgnoreCase))
                {
                    LocationBox.Items.Add(new ComboBoxItem()
                    {
                        Content = loc.Name,
                    });
                }
            }
            LocationBox.SelectedIndex = 0;
            LocationBox.IsVisible = true;
        }
        else
        {
            LocationBox.IsVisible = false;
        }

        List<TimingSystem> systems = mWindow.GetConnectedSystems();
        int numSystems = systems.Count;
        string system = Readers.DEFAULT_TIMING_SYSTEM;
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
                systems.Add(new(string.Format(ipformat, baseIP[0], baseIP[1], baseIP[2], baseIP[3]), system));
            }
        }
        systems.Sort((x, y) =>
        {
            return x.Status == y.Status ? x.IPAddress.CompareTo(y.IPAddress) : x.Status.CompareTo(y.Status);
        });
        systems.Add(new(string.Format(ipformat, baseIP[0], baseIP[1], baseIP[2], baseIP[3]), system));
        known = 0;
        foreach (TimingSystem sys in systems)
        {
            ReadersBox.Items.Add(new ReaderPart(this, sys, locations));
            if (sys.IPAddress != string.Format(ipformat, baseIP[0], baseIP[1], baseIP[2], baseIP[3]))
            {
                known++;
            }
        }
        total = ReadersBox.Items.Count;
        subPage = new TimingResultsPage(this, database);
        TimingFrame.Content = subPage;
        List<DistanceStat> inStats = database.GetDistanceStats(theEvent.Identifier, true);
        StatsListView.ItemsSource = stats;
        stats.Clear();
        foreach (DistanceStat s in inStats)
        {
            stats.Add(s);
        }

        if (Constants.Timing.EVENT_TYPE_TIME == theEvent.EventType)
        {
            DNFButton.Content = "Add Finished";
        }

        // Check if our web server is active and update the button
        if (mWindow.HttpServerActive())
        {
            HttpServerButton.Content = "Stop Web";
            WebButton.IsVisible = true;
        }
        else
        {
            HttpServerButton.Content = "Start Web";
            WebButton.IsVisible = false;
        }
        if (theEvent.API_ID > 0 && theEvent.API_Event_ID.Length > 1)
        {
            ApiPanel.IsVisible = true;
        }
        else
        {
            ApiPanel.IsVisible = false;
        }
        if (mWindow.IsAPIControllerRunning())
        {
            AutoAPIButton.Content = "Stop Uploads";
            ManualAPIButton.IsEnabled = false;
        }
        else
        {
            AutoAPIButton.Content = "Auto Upload";
            ManualAPIButton.IsEnabled = true;
        }

        RemoteReadsController.RemoteStatus rStatus = mWindow.IsRemoteRunning();
        if (rStatus == RemoteReadsController.RemoteStatus.RUNNING)
        {
            RemoteControllerSwitch.IsChecked = true;
            RemoteErrorsBlock.Text = mWindow.RemoteErrors() > 0 ? mWindow.RemoteErrors().ToString() : "";
            RemoteControllerSwitch.IsEnabled = true;
        }
        else if (rStatus == RemoteReadsController.RemoteStatus.STOPPED)
        {
            RemoteControllerSwitch.IsChecked = false;
            RemoteControllerSwitch.IsEnabled = true;
            RemoteErrorsBlock.Text = "";
        }

        UpdateDNSButton();

        // check if we have a remote api set up
        foreach (APIObject api in database.GetAllAPI())
        {
            if (api.Type == APIConstants.CHRONOKEEP_REMOTE_SELF || api.Type == APIConstants.CHRONOKEEP_REMOTE)
            {
                RemoteControllerSwitch?.IsVisible = true;
                RemoteReadersButton?.IsVisible = ReaderExpander.IsExpanded;
                remote_api = true;
                break;
            }
        }

        List<ReaderMessage> readerMsgs = GetReaderMessages();
        if (readerMsgs.Count > 0)
        {
            ReaderMessageButton.IsVisible = true;
            int count = readerMsgs.FindAll(x => !x.Notified).Count;
            ReaderMessageButton.Content = count.ToString();
            if (count > 0)
            {
                ReaderMessageButton.Background = (SolidColorBrush?)Resources["AlertColor"] ?? Brush.Parse("Orange");
            }
            else
            {
                ReaderMessageButton.Background = (SolidColorBrush?)Resources["LightPrimaryColor"] ?? Brush.Parse("LightBlue");
            }
        }
        else
        {
            ReaderMessageButton.IsVisible = false;
            ReaderMessageButton.Content = 0.ToString();
        }

        if (alreadyRecalculating)
        {
            RecalculateButton.Content = "Working...";
        }
        else
        {
            RecalculateButton.Content = "Recalculate";
        }
    }

    public void Keyboard_Ctrl_A() { }

    public void Keyboard_Ctrl_S() { }

    public void Keyboard_Ctrl_Z() { }

    public static void UpdateDatabase() { }

    public void Closing()
    {
        List<TimingSystem> removedSystems = database.GetTimingSystems();
        List<TimingSystem> ourSystems = [];
        foreach (ReaderPart? box in ReadersBox.Items.Cast<ReaderPart?>())
        {
            box!.UpdateReader();
            if (box.reader.IPAddress != "0.0.0.0" && box.reader.IPAddress.Length > 7 &&
                box.reader.IPAddress != string.Format(ipformat, baseIP[0], baseIP[1], baseIP[2], baseIP[3]))
            {
                ourSystems.Add(box.reader);
            }
        }
        removedSystems.RemoveAll(x => ourSystems.Contains(x));
        foreach (TimingSystem sys in removedSystems)
        {
            database.RemoveTimingSystem(sys);
        }
        foreach (TimingSystem sys in ourSystems)
        {
            database.AddTimingSystem(sys);
        }
        Timer.Stop();
    }

    public void UpdateAlarms()
    {
        if (subPage is AlarmsPage page)
        {
            page.UpdateAlarms();
        }
    }

    public void UpdateView()
    {
        Log.D("UI.MainPages.TimingPage", "Updating timing information.");
        if (theEvent == null || theEvent.Identifier == -1)
        {
            // Something went wrong and this shouldn't be visible.
            return;
        }
        if (TimerStarted)
        {
            UpdateStartTime();
        }

        // Update locations in the list of readers (and reader status)
        total = ReadersBox.Items.Count; known = 0;
        foreach (ReaderPart? read in ReadersBox.Items.Cast<ReaderPart?>())
        {
            read!.UpdateStatus();
            if (read.reader.Status == SYSTEM_STATUS.DISCONNECTED)
            {
                if (timeWindow != null && timeWindow.IsTimingSystem(read.reader))
                {
                    timeWindow.Close();
                    timeWindow = null;
                }
                if (rewindWindow != null && rewindWindow.IsTimingSystem(read.reader))
                {
                    rewindWindow.Close();
                    rewindWindow = null;
                }
            }
            if (read.reader.IPAddress != string.Format(ipformat, baseIP[0], baseIP[1], baseIP[2], baseIP[3]))
            {
                known++;
            }
        }

        if (total < 4 || known >= total)
        {
            string system = Readers.DEFAULT_TIMING_SYSTEM;
            try
            {
                system = database.GetAppSetting(Settings.DEFAULT_TIMING_SYSTEM)!.Value;
            }
            catch
            {
                Log.D("UI.MainPages.TimingPage", "Error fetching default timing system information.");
                system = Readers.DEFAULT_TIMING_SYSTEM;
            }
            for (int i = total; i < 3; i++)
            {
                ReadersBox.Items.Add(new ReaderPart(
                    this,
                    new(string.Format(ipformat, baseIP[0], baseIP[1], baseIP[2], baseIP[3]),
                        system),
                        locations!));
            }
            ReadersBox.Items.Add(new ReaderPart(
                this,
                new(string.Format(ipformat, baseIP[0], baseIP[1], baseIP[2], baseIP[3]),
                    system),
                    locations!));
        }
        List<DistanceStat> inStats = database.GetDistanceStats(theEvent.Identifier, CondenseSwitch.IsChecked == false);
        stats.Clear();
        foreach (DistanceStat s in inStats)
        {
            stats.Add(s);
        }
        if (mWindow.HttpServerActive())
        {
            HttpServerButton.Content = "Stop Web";
            WebButton.IsVisible = true;
        }
        else
        {
            HttpServerButton.Content = "Start Web";
            WebButton.IsVisible = false;
        }
        if (theEvent.API_ID > 0 && theEvent.API_Event_ID.Length > 1)
        {
            ApiPanel.IsVisible = true;
        }
        else
        {
            ApiPanel.IsVisible = false;
        }
        if (mWindow.IsAPIControllerRunning())
        {
            AutoAPIButton.Content = mWindow.APIErrors() > 0 ? string.Format("Stop Uploads ({0})", mWindow.APIErrors()) : "Stop Uploads";
            ManualAPIButton.IsEnabled = false;
        }
        else
        {
            AutoAPIButton.Content = "Auto Upload";
            ManualAPIButton.IsEnabled = true;
        }

        RemoteReadsController.RemoteStatus rStatus = mWindow.IsRemoteRunning();
        if (rStatus == RemoteReadsController.RemoteStatus.RUNNING)
        {
            RemoteControllerSwitch.IsChecked = true;
            RemoteControllerSwitch.IsEnabled = true;
            RemoteErrorsBlock.Text = mWindow.RemoteErrors() > 0 ? mWindow.RemoteErrors().ToString() : "";
        }
        else if (rStatus == RemoteReadsController.RemoteStatus.STOPPED)
        {
            RemoteControllerSwitch.IsChecked = false;
            RemoteControllerSwitch.IsEnabled = true;
            RemoteErrorsBlock.Text = "";
        }

        UpdateDNSButton();

        List<ReaderMessage> readerMsgs = GetReaderMessages();
        if (readerMsgs.Count > 0)
        {
            ReaderMessageButton.IsVisible = true;
            int count = readerMsgs.FindAll(x => !x.Notified).Count;
            ReaderMessageButton.Content = count.ToString();
            if (count > 0)
            {
                ReaderMessageButton.Background = Brush.Parse("orange");
            }
            else
            {
                ReaderMessageButton.Background = Brush.Parse("#479ef5");
            }
        }
        else
        {
            ReaderMessageButton.IsVisible = false;
            ReaderMessageButton.Content = 0.ToString();
        }
        UpdateSubView();
        if (alreadyRecalculating)
        {
            RecalculateButton.Content = "Working...";
        }
        else
        {
            RecalculateButton.Content = "Recalculate";
        }
    }

    public void UpdateSubView()
    {
        Log.D("UI.MainPages.TimingPage", "Updating sub view.");
        cts?.Cancel();
        cts = new();
        try
        {
            subPage!.CancelableUpdateView(cts.Token);
            cts = null;
        }
        catch
        {
            Log.D("UI.MainPages.TimingPage", "Update cancelled.");
        }
    }

    public void DatasetChanged()
    {
        mWindow.NotifyTimingWorker();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        long unixElapsed = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - (startTime.Ticks / TimeSpan.TicksPerMillisecond);
        if (waveTimes.TryGetValue(selectedWave, out (long seconds, int milliseconds) value))
        {
            unixElapsed -= value.seconds * 1000;
            unixElapsed -= value.milliseconds;
        }
        EllapsedTime.Text = Constants.Timing.SecondsToTime(Math.Abs(unixElapsed / 1000));
    }

    public void NotifyTimingWorker()
    {
        mWindow.NotifyTimingWorker();
    }

    private void StartTimeChanged()
    {
        UpdateStartTime();
        long oldStartSeconds = theEvent!.StartSeconds;
        int oldStartMilliseconds = theEvent.StartMilliseconds;
        theEvent.StartSeconds = (startTime.Hour * 3600) + (startTime.Minute * 60) + startTime.Second;
        theEvent.StartMilliseconds = startTime.Millisecond;
        if (oldStartSeconds != theEvent.StartSeconds || oldStartMilliseconds != theEvent.StartMilliseconds)
        {
            database.UpdateEvent(theEvent);
            database.UpdateStart(); // This is a MemStore specific database call that updates the Start value for ChipReads.
            database.ResetTimingResultsEvent(theEvent.Identifier);
            UpdateView();
            mWindow.NetworkClearResults();
            mWindow.NotifyTimingWorker();
        }
    }

    private void UpdateStartTime()
    {
        if (!TimerStarted)
        {
            TimerStarted = true;
            Timer.Start();
        }
        string startTimeValue = StartTime.Text!.Replace('_', '0');
        StartRace.IsEnabled = false;
        if (waves.Count > 1)
        {
            EllapsedRelativeToBox.IsVisible = true;
        }
        else
        {
            EllapsedRelativeToBox.IsVisible = false;
        }
        StartTime.Text = startTimeValue;
        Log.D("UI.MainPages.TimingPage", "Start time is " + startTimeValue);
        startTime = DateTime.ParseExact(startTimeValue + DateTime.Parse(theEvent!.Date).ToString("ddMMyyyy"), "HH:mm:ss.fffddMMyyyy", null);
        Log.D("UI.MainPages.TimingPage", "Start time is " + startTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
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
        rewindWindow = new(system, this);
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
            try
            {
                if (sys.Status == SYSTEM_STATUS.CONNECTED)
                {
                    if (now)
                    {
                        sys.SystemInterface?.SetTime(DateTime.Now);
                    }
                    else
                    {
                        sys.SystemInterface?.SetTime(time);
                    }
                }
            }
            catch (Exception e)
            {
                Log.E("TimingPage", $"Error setting time on timing system via set all. {e.Message}");
            }
        }
    }

    public void RemoveSystem(TimingSystem sys)
    {
        database.RemoveTimingSystem(sys.SystemIdentifier);
        ReaderPart? removed = null;
        foreach (ReaderPart? box in ReadersBox.Items.Cast<ReaderPart?>())
        {
            if (box!.reader.SystemIdentifier == sys.SystemIdentifier && sys.Saved())
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
        return sys.Status != SYSTEM_STATUS.DISCONNECTED;
    }

    public bool DisconnectSystem(TimingSystem sys)
    {
        mWindow.DisconnectTimingSystem(sys);
        return sys.Status == SYSTEM_STATUS.DISCONNECTED;
    }

    internal void SetRawReadsFinished()
    {
        RawButton.Content = "Raw Data";
    }

    public void LoadMainDisplay()
    {
        Log.D("UI.MainPages.TimingPage", "Going back to main display.");
        SetRawReadsFinished();
        subPage = new TimingResultsPage(this, database);
        TimingFrame.Content = subPage;
    }

    public PeopleType GetPeopleType()
    {
        switch (((ComboBoxItem)ViewOnlyBox.SelectedItem!).Content)
        {
            case "Show All":
                return PeopleType.ALL;
            case "Show Only Starts":
                return PeopleType.STARTS;
            case "Show Only Finishes":
                return PeopleType.FINISHES;
            case "Show Only Unknown":
                return PeopleType.UNKNOWN;
            case "Show Only Unknown Finishes":
                return PeopleType.UNKNOWN_FINISHES;
            case "Show Only Unknown Starts":
                return PeopleType.UNKNOWN_STARTS;
            default:
                break;
        }
        return PeopleType.KNOWN;
    }

    public SortType GetSortType()
    {
        switch (((ComboBoxItem)SortBy.SelectedItem!).Content)
        {
            case "Clock Time":
                return SortType.GUNTIME;
            case "Bib":
                return SortType.BIB;
            case "Distance":
                return SortType.DISTANCE;
            case "Age Group":
                return SortType.AGEGROUP;
            case "Gender":
                return SortType.GENDER;
            case "Place":
                return SortType.PLACE;
            default:
                break;
        }
        return SortType.SYSTIME;
    }

    public string GetSearchValue()
    {
        return SearchBox.Text == null ? "" : SearchBox.Text.Trim();
    }

    public string GetLocation()
    {
        ComboBoxItem locItem = (ComboBoxItem)LocationBox.SelectedItem!;
        if (locItem == null)
        {
            return "";
        }
        return locItem.Content!.ToString()!;
    }

    private async void UploadResults()
    {
        // Get API to upload.
        if (theEvent!.API_ID < 0 && theEvent.API_Event_ID.Length > 1)
        {
            return;
        }
        APIObject api = database.GetAPI(theEvent.API_ID)!;
        string[] event_ids = theEvent.API_Event_ID.Split(',');
        if (event_ids.Length != 2)
        {
            return;
        }
        // Get results to upload.
        List<TimeResult> results = database.GetNonUploadedResults(theEvent.Identifier);
        // Remove all results to upload that don't have a place set, are not DNF/DNS results, and are also not start times.
        results.RemoveAll(x => x.Place < 1
            && x.Status != Constants.Timing.TIMERESULT_STATUS_DNF
            && x.Status != Constants.Timing.TIMERESULT_STATUS_DNS
            && x.SegmentId != Constants.Timing.SEGMENT_START);
        if (results.Count < 1)
        {
            Log.D("UI.MainPages.TimingPage", "Nothing to upload.");
            Application.Current!.Dispatcher.Invoke(new Action(delegate ()
            {
                ManualAPIButton.Content = "Manual Upload";
            }));
            return;
        }
        // Upload results
        Log.D("UI.MainPages.TimingPage", "Results count: " + results.Count.ToString());
        if (APIController.GetUploadable(3000))
        {
            await APIController.UploadResults(results, api, event_ids, database, null, null, theEvent);
        }
        Application.Current!.Dispatcher.Invoke(new Action(delegate ()
        {
            ManualAPIButton.Content = "Manual Upload";
        }));
    }

    private void UpdateDNSButton()
    {
        if (mWindow.InDidNotStartMode())
        {
            DnsMode.Content = "Stop DNS Mode";
        }
        else
        {
            DnsMode.Content = "Start DNS Mode";
        }
    }

    public void SetReaders(string[] readers, bool visible)
    {
        ReaderSelectionBox.Items.Clear();
        foreach (string reader in readers)
        {
            ReaderSelectionBox.Items.Add(reader);
        }
        ReaderSelectionBox.SelectedIndex = 0;
        ReaderSelectionBox.IsVisible = visible;
    }

    public string GetReader()
    {
        return ReaderSelectionBox.SelectedItem != null ? ReaderSelectionBox.SelectedItem.ToString()! : "";
    }

    private void EllapsedRelativeToBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Log.E("UI.MainPages.TimingPage", "EllapsedRelativeToBox selection changed.");
        selectedWave = -1;
        if (EllapsedRelativeToBox.SelectedIndex >= 0 && EllapsedRelativeToBox.SelectedItem is TimeRelativeWave wave)
        {
            selectedWave = wave.Wave;
        }
    }

    private void StartTimeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                Log.D("UI.MainPages.TimingPage", "User wants to reset start time value.");
                theEvent!.StartSeconds = 0;
                theEvent!.StartMilliseconds = 0;
                if (TimerStarted)
                {
                    TimerStarted = false;
                    Timer.Stop();
                }
                database.UpdateEvent(theEvent);
                StartTime.Text = "";
                EllapsedTime.Text = "00:00:00";
                StartRace.IsEnabled = true;
                EllapsedRelativeToBox.IsEnabled = false;
                EllapsedRelativeToBox.IsVisible = false;
                return;
            }
            Log.D("UI.MainPages.TimingPage", "Start Time Box return key found.");
            UpdateStartTime();
        }
    }

    private void StartRaceClick(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Starting race.");
        StartTime.Text = DateTime.Now.ToString("HH:mm:ss.fff");
        StartRace.IsEnabled = false;
        EllapsedRelativeToBox.IsEnabled = true;
        if (waves.Count > 1)
        {
            EllapsedRelativeToBox.IsVisible = true;
        }
        else
        {
            EllapsedRelativeToBox.IsVisible = false;
        }
        foreach (Chronoclock clock in database.GetClocks())
        {
            if (clock.Enabled == true)
            {
                try
                {
                    _ = clock.StartCountUp();
                }
                catch { } // Exception may get thrown due to not waiting on the async method
                          // The clocks need to start as fast as possible and it does not matter if the
                          // call fails (the clock is probably not connected to the same network)
            }
        }
        StartTimeChanged();
    }

    private void OpenClock_Click(object? sender, RoutedEventArgs e)
    {
        ClockControl clockWindow = ClockControl.CreateWindow(mWindow, database);
        clockWindow.Show();
    }

    private void ChangeWaves(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Set Wave Times clicked.");
        WaveWindow waves = new(mWindow, database);
        mWindow.AddWindow(waves);
        waves.ShowDialog((Window)mWindow);
    }

    private void AlarmButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Alarms selected.");
        SetRawReadsFinished();
        subPage = new AlarmsPage(this, database);
        TimingFrame.Content = subPage;
    }

    private void AddDNF_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Add DNF Entry clicked.");
        ManualEntryWindow manualEntryWindow = ManualEntryWindow.NewWindow(mWindow, database);
        if (manualEntryWindow != null)
        {
            mWindow.AddWindow(manualEntryWindow);
            manualEntryWindow.ShowDialog((Window)mWindow);
        }
    }

    private void ManualEntry(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Manual Entry selected.");
        ManualEntryWindow manualEntryWindow = ManualEntryWindow.NewWindow(mWindow, database, locations!);
        if (manualEntryWindow != null)
        {
            mWindow.AddWindow(manualEntryWindow);
            manualEntryWindow.ShowDialog((Window)mWindow);
        }
    }

    private async void LoadLog(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Loading from log.");
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            IStorageFolder? startingFolder;
            try
            {
                startingFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(database.GetAppSetting(Settings.DEFAULT_EXPORT_DIR)!.Value));
            }
            catch
            {
                startingFolder = null;
            }
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                FileTypeFilter = [Utils.LogType, FilePickerFileTypes.All],
                AllowMultiple = false,
                SuggestedStartLocation = startingFolder,
            });
            if (files.Count > 0)
            {
                try
                {
                    LogImporter importer = new(files[0].TryGetLocalPath()!);
                    await Task.Run(importer.FindType);
                    ImportLogWindow logWindow = ImportLogWindow.NewWindow(mWindow, importer, database);
                    if (logWindow != null)
                    {
                        mWindow.AddWindow(logWindow);
                        await logWindow.ShowDialog((Window)mWindow);
                    }
                }
                catch (Exception ex)
                {
                    Log.E("UI.MainPages.TimingPage", "Something went wrong when trying to read the CSV file.");
                    Log.E("UI.MainPages.TimingPage", ex.StackTrace!);
                }
            }
        }
    }

    private async void SaveLog(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Save Log clicked.");
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            IStorageFolder? startingFolder;
            try
            {
                startingFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(database.GetAppSetting(Constants.Settings.DEFAULT_EXPORT_DIR)!.Value));
            }
            catch
            {
                startingFolder = null;
            }
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                FileTypeChoices = [Utils.CSVType],
                SuggestedFileName = string.Format("{0} {1} Log.{2}", theEvent!.YearCode, theEvent.Name, "csv"),
                SuggestedStartLocation = startingFolder,
            });
            if (file is not null)
            {
                Dictionary<string, List<ChipRead>> locationReadDict = [];
                string[] headers =
                [
                        "status",
                        "chip_number",
                        "seconds",
                        "milliseconds",
                        "time_seconds",
                        "time_milliseconds",
                        "antenna",
                        "reader",
                        "box",
                        "log_index",
                        "rssi",
                        "is_rewind",
                        "reader_time",
                        "start_time",
                        "read_bib",
                        "type"
                    ];
                List<ChipRead> chipReads = database.GetChipReads(theEvent!.Identifier);
                foreach (ChipRead read in chipReads)
                {
                    if (!locationReadDict.TryGetValue(read.LocationName, out List<ChipRead>? locChipReads))
                    {
                        locChipReads = [];
                        locationReadDict[read.LocationName] = locChipReads;
                    }

                    locChipReads.Add(read);
                }
                StringBuilder format = new();
                for (int i = 0; i < headers.Length; i++)
                {
                    format.Append("\"{");
                    format.Append(i);
                    format.Append("}\",");
                }
                format.Remove(format.Length - 1, 1);
                Log.D("UI.MainPages.TimingPage", string.Format("The format is '{0}'", format.ToString()));
                if (locationReadDict.Keys.Count == 1)
                {
                    List<object[]> data = [];
                    foreach (ChipRead read in chipReads)
                    {
                        data.Add([
                            read.Status,
                                read.ChipNumber,
                                read.Seconds,
                                read.Milliseconds,
                                read.TimeSeconds,
                                read.TimeMilliseconds,
                                read.Antenna,
                                read.Reader,
                                read.Box,
                                read.LogId,
                                read.RSSI,
                                read.IsRewind,
                                read.ReaderTime,
                                read.StartTime,
                                read.ReadBib,
                                read.Type
                        ]);
                    }
                    CSVExporter exporter = new(format.ToString());
                    exporter.SetData(headers, data);
                    exporter.ExportData(file.TryGetLocalPath()!);
                }
                // Multiple locations, save each individually.
                else
                {
                    foreach (string key in locationReadDict.Keys)
                    {
                        List<object[]> data = [];
                        foreach (ChipRead read in locationReadDict[key])
                        {
                            data.Add([
                            read.Status,
                                read.ChipNumber,
                                read.Seconds,
                                read.Milliseconds,
                                read.TimeSeconds,
                                read.TimeMilliseconds,
                                read.Antenna,
                                read.Reader,
                                read.Box,
                                read.LogId,
                                read.RSSI,
                                read.IsRewind,
                                read.ReaderTime,
                                read.StartTime,
                                read.ReadBib,
                                read.Type
                        ]);
                        }
                        CSVExporter exporter = new(format.ToString());
                        exporter.SetData(headers, data);
                        string outFileName = string.Format("{0}\\{1}-{2}", Path.GetDirectoryName(file.TryGetLocalPath()!), FileSaveRegex().Replace(key.ToLower(), ""), file.Name);
                        Log.D("UI.MainPages.TimingPage", string.Format("Saving file to: {0}", outFileName));
                        exporter.ExportData(outFileName);
                    }
                }
                DialogBox.Show("File saved.");
            }
        }
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Searchbox text has changed");
        cts?.Cancel();
        cts = null;
        cts = new();
        try
        {
            subPage!.Search(cts.Token, SearchBox.Text!.Trim());
            cts = null;
        }
        catch
        {
            Log.D("UI.MainPages.TimingPage", "Update cancelled.");
        }
    }

    private void ViewOnlyBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!loaded) { return; }
        if (subPage == null)
        {
            return;
        }
        switch (((ComboBoxItem)ViewOnlyBox.SelectedItem!).Content)
        {
            case "Show Only Unknown":
                subPage.Show(PeopleType.UNKNOWN);
                break;
            case "Show All":
                subPage.Show(PeopleType.ALL);
                break;
            case "Show Only Starts":
                subPage.Show(PeopleType.STARTS);
                break;
            case "Show Only Finishes":
                subPage.Show(PeopleType.FINISHES);
                break;
            case "Show Only Unknown Finishes":
                subPage.Show(PeopleType.UNKNOWN_FINISHES);
                break;
            case "Show Only Unknown Starts":
                subPage.Show(PeopleType.UNKNOWN_STARTS);
                break;
            default:
                subPage.Show(PeopleType.KNOWN);
                break;
        }
    }

    private void ReaderSelectionBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!loaded) { return; }
        if (subPage == null) return;
        string readerItem = (string)ReaderSelectionBox.SelectedItem!;
        if (readerItem == null)
        {
            subPage.Reader("");
        }
        else
        {
            subPage.Reader(readerItem);
        }
    }

    private void LocationBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!loaded) { return; }
        if (subPage == null)
        {
            return;
        }
        ComboBoxItem locItem = (ComboBoxItem)LocationBox.SelectedItem!;
        if (locItem == null)
        {
            subPage.Location("");
        }
        else
        {
            subPage.Location(locItem.Content!.ToString()!);
        }
    }

    private void SortBy_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!loaded) { return; }
        if (subPage == null)
        {
            return;
        }
        subPage.SortBy(GetSortType());
    }

    private void StatsListView_MouseDoubleClick(object? sender, TappedEventArgs e)
    {
        DistanceStat selected = (DistanceStat)StatsListView.SelectedItem;
        if (selected == null)
        {
            return;
        }
        Log.D("UI.MainPages.TimingPage", "Stats double cliked. Distance is " + selected.DistanceName);
        SetRawReadsFinished();
        subPage = new DistanceStatsPage(this, mWindow, database, selected.DistanceID, selected.DistanceName, CondenseSwitch.IsChecked == false);
        TimingFrame.Content = subPage;
    }

    private async void Recalculate_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Recalculate results clicked.");
        if ((string)RecalculateButton.Content! == "Working..." || alreadyRecalculating)
        {
            return;
        }
        RecalculateButton.Content = "Working...";
        alreadyRecalculating = true;
        if (APIController.SetUploadableFalse(uploadTimer))
        {
            bool canRecalculate = await Task<bool>.Run(() =>
            {
                int counter = 0;
                while (true)
                {
                    if (counter > 5)
                    {
                        return false;
                    }
                    if (!APIController.IsUploading())
                    {
                        return true;
                    }
                    counter++;
                    //Log.D("UI.MainPages.TimingPage", "APIController is uploading. Sleeping for 1 second. Counter is " + counter.ToString());
                    Thread.Sleep(1000);
                }
                ;
            });
            if (!canRecalculate)
            {
                await Task.Run(() =>
                {
                    int counter = 0;
                    while (!APIController.SetUploadableTrue(uploadTimer))
                    {
                        counter++;
                        if (counter > 5)
                        {
                            break;
                        }
                    }
                });
                RecalculateButton.Content = "Recalculate";
                alreadyRecalculating = false;
                DialogBox.Show("Unable to recalculate results.");
                return;
            }
        }
        else
        {
            RecalculateButton.Content = "Recalculate";
            alreadyRecalculating = false;
            return;
        }
        APIObject? api = null;
        try
        {
            api = database.GetAPI(theEvent!.API_ID);
            Log.D("UI.MainPages.TimingPage", "API found.");
        }
        catch { }
        // Get the event id values. Exit if not valid.
        string[] event_ids = theEvent!.API_Event_ID.Split(',');
        Log.D("UI.MainPages.TimingPage", "Event Id's found: " + event_ids.Length + " API is null? " + (api == null).ToString());
        if (event_ids.Length == 2 && api != null)
        {
            try
            {
                Log.D("UI.MainPages.TimingPage", "Deleting results from API.");
                if (theEvent.UploadSpecific == true)
                {
                    foreach (Distance d in database.GetDistances(theEvent.Identifier))
                    {
                        if (d.Upload == true && d.LinkedDistance == Constants.Timing.DISTANCE_NO_LINKED_ID)
                        {
                            await APIController.DeleteResults(api, event_ids[0], event_ids[1], d.Name);
                        }
                    }
                }
                else
                {
                    await APIController.DeleteResults(api, event_ids[0], event_ids[1], null);
                }
            }
            catch (APIException ex)
            {

                DialogBox.Show(ex.Message);
            }
        }
        // We do this because we want to ensure we've reset all the results before we allow
        // the auto uploader to start uploading any more results so we don't upload
        // old results over our brand new results.
        database.ResetTimingResultsEvent(theEvent.Identifier);
        await Task.Run(() =>
        {
            int counter = 0;
            while (!APIController.SetUploadableTrue(uploadTimer))
            {
                counter++;
                if (counter > 5)
                {
                    break;
                }
            }
        });
        RecalculateButton.Content = "Recalculate";
        alreadyRecalculating = false;
        UpdateSubView();
        mWindow.NetworkClearResults();
        mWindow.NotifyTimingWorker();
    }

    private void AutoAPI_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Auto API clicked.");
        if ((string)AutoAPIButton.Content! == "Auto Upload")
        {
            AutoAPIButton.Content = "Starting...";
            mWindow.StartAPIController();
        }
        else
        {
            AutoAPIButton.Content = "Stopping...";
            mWindow.StopAPIController();
        }
    }

    private async void ManualAPI_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Manual API clicked.");
        if (ManualAPIButton.Content!.ToString() != "Uploading")
        {
            Log.D("UI.MainPages.TimingPage", "Uploading data.");
            ManualAPIButton.Content = "Uploading";
            await Task.Run(() =>
            {
                UploadResults();
            });
            return;
        }
        Log.D("UI.MainPages.TimingPage", "Already uploading.");
    }

    private async void SendEmailsButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Send Emails button clicked.");
        if ((string)SendEmailsButton.Content! != "Send Emails")
        {
            return;
        }
        SendEmailsButton.Content = "Sending...";
        await Task.Run(() =>
        {
            HashSet<int> sentIDs = [];
            List<int> idents = database.GetEmailAlerts(theEvent!.Identifier);
            if (idents == null)
            {
                return;
            }
            foreach (int es_id in idents)
            {
                sentIDs.Add(es_id);
            }
            List<TimeResult> finishTimes = database.GetFinishTimes(theEvent.Identifier);
            APIObject api = database.GetAPI(theEvent.API_ID)!;
            Dictionary<string, Participant> participantDictionary = [];
            int distances = 0;
            foreach (Participant p in database.GetParticipants(theEvent.Identifier))
            {
                participantDictionary[p.Identifier.ToString()] = p;
            }
            foreach (Distance d in database.GetDistances(theEvent.Identifier))
            {
                if (Constants.Timing.DISTANCE_NO_LINKED_ID == d.LinkedDistance)
                {
                    distances++;
                }
            }
            GlobalVars.UpdateBannedEmails();
            HttpClient client = new();
            MailgunCredentials credentials = MailgunCredentials.GetCredentials(database);
            if (!credentials.Valid())
            {
                return;
            }
            string base64String = Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", credentials.Username, credentials.APIKey)));
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(@"Basic", base64String);
            foreach (TimeResult result in finishTimes)
            {
                Participant? part = participantDictionary.TryGetValue(result.ParticipantId, out Participant? oPart) ? oPart : null;
                if (part != null && result.EventSpecificId != Constants.Timing.EVENTSPECIFIC_UNKNOWN)
                {
                    if (part.Email.Length > 0 && !GlobalVars.BannedEmails.Contains(part.Email) && !sentIDs.Contains(result.EventSpecificId))
                    {
                        MultipartFormDataContent postData = new()
                            {
                                { new StringContent(credentials.From()), "from" },
                                { new StringContent(part.Email), "to" },
                                { new StringContent(string.Format("{0} {1}", theEvent.Year, theEvent.Name)), "subject" },
                                { new StringContent(new HtmlCertificateEmailTemplate(
                                    theEvent,
                                    result,
                                    part.Email,
                                    distances == 1,
                                    api
                                    ).TransformText()), "html" }
                            };
                        try
                        {
                            client.PostAsync(string.Format("https://api.mailgun.net/v3/{0}/messages", credentials.Domain), postData);
                        }
                        catch
                        {
                            Log.E("UI.MainPages.TimingPage", "Error sending email.");
                        }
                        database.AddEmailAlert(theEvent.Identifier, result.EventSpecificId);
                    }
                }
            }
            Log.D("UI.MainPages.TimingPage", "Async operation to send emails finished.");
        });
        Log.D("UI.MainPages.TimingPage", "Changing button back and sending dialog box.");
        DialogBox.Show("Emails sent.");
        SendEmailsButton.Content = "Send Emails";
    }

    private void ModifySMSButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Modify SMS button clicked.");
        SMSWaveEnabledWindow smsWindow = new(mWindow, database);
        smsWindow.Show();
    }

    private void DnsMode_Click(object? sender, RoutedEventArgs e)
    {
        bool worked;
        if (DnsMode.Content!.Equals("Start DNS Mode"))
        {
            Log.D("UI.MainPages.TimingPage", "Starting DNS Mode.");
            worked = mWindow.StartDidNotStartMode();
        }
        else
        {
            Log.D("UI.MainPages.TimingPage", "Stopping DNS Mode.");
            worked = mWindow.StopDidNotStartMode();
        }
        if (!worked)
        {
            DialogBox.Show("An error occurred entering DNS mode.");
        }
        UpdateDNSButton();
    }

    private void RawReads_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Raw Reads selected.");
        if (RawButton.Content!.ToString()!.Equals("Raw Data", StringComparison.OrdinalIgnoreCase))
        {
            RawButton.Content = "Refresh Data";
            subPage = new TimingRawReadsPage(this, database, mWindow);
            TimingFrame.Content = subPage;
        }
        else if (subPage is TimingRawReadsPage rawReadsPage)
        {
            // Refresh data
            rawReadsPage.PrivateUpdateView();
        }
        else
        {
            SetRawReadsFinished();
        }
    }

    private void HTMLServerButton_Click(object? sender, RoutedEventArgs e)
    {
        if (HttpServerButton.Content!.ToString()!.Equals("Start Web", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                mWindow.StartHttpServer();
                HttpServerButton.Content = "Stop Web";
                WebButton.IsVisible = true;
            }
            catch
            {
                mWindow.StopHttpServer();
                HttpServerButton.Content = "Start Web";
                DialogBox.Show("Unable to start the web server. Please type this command in an elevated command prompt:", "netsh http add urlacl url=http://*:6933/ user=everyone");
                WebButton.IsVisible = false;
            }
        }
        else
        {
            mWindow.StopHttpServer();
            HttpServerButton.Content = "Start Web";
            WebButton.IsVisible = false;
        }
    }

    private void Print_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Print clicked.");
        SetRawReadsFinished();
        subPage = new PrintPage(this, database);
        TimingFrame.Content = subPage;
    }

    private void Award_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Awards clicked.");
        SetRawReadsFinished();
        subPage = new AwardPage(this, database);
        TimingFrame.Content = subPage;
    }

    private async void CreateHTML_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Create HTML clicked.");
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            IStorageFolder? startingFolder;
            try
            {
                startingFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(database.GetAppSetting(Constants.Settings.DEFAULT_EXPORT_DIR)!.Value));
            }
            catch
            {
                startingFolder = null;
            }
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                FileTypeChoices = [Utils.HTMLType],
                SuggestedFileName = string.Format("{0} {1} Web.{2}", theEvent!.YearCode, theEvent.Name, "html"),
                SuggestedStartLocation = startingFolder,
            });
            if (file is not null)
            {
                List<TimeResult> finishResults = database.GetFinishTimes(theEvent!.Identifier);
                Dictionary<int, Participant> partDict = database.GetParticipants(theEvent.Identifier).ToDictionary(v => v.EventSpecific.Identifier, v => v);
                HtmlResultsTemplate template = new(theEvent, finishResults);
                File.WriteAllText(file.TryGetLocalPath()!, template.TransformText());
                DialogBox.Show("File saved.");
            }
        }
    }

    private void Export_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Export clicked.");
        ExportResults exportResults = new(mWindow, database);
        if (!exportResults.SetupError())
        {
            mWindow.AddWindow(exportResults);
            exportResults.ShowDialog((Window)mWindow);
        }
    }

    private void Export_BAA_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Export BAA Clicked.");
        if (theEvent!.EventType == Constants.Timing.EVENT_TYPE_TIME)
        {
            DialogBox.Show("Exporting time based events not supported.");
            return;
        }
        ExportDistanceResults exportBAA = new(mWindow, database, OutputType.Boston);
        if (!exportBAA.SetupError())
        {
            mWindow.AddWindow(exportBAA);
            exportBAA.ShowDialog((Window)mWindow);
        }
    }

    private void Export_Abbott_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Export Abbott Clicked.");
        ExportDistanceResults exportAbbott = new(mWindow, database, OutputType.Abbott);
        if (!exportAbbott.SetupError())
        {
            mWindow.AddWindow(exportAbbott);
            exportAbbott.ShowDialog((Window)mWindow);
        }
    }

    private void Export_US_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Export Ultrasignup Clicked.");
        if (theEvent!.EventType == Constants.Timing.EVENT_TYPE_TIME)
        {
            DialogBox.Show("Exporting time based events not supported.");
            return;
        }
        ExportDistanceResults exportUS = new(mWindow, database, OutputType.UltraSignup);
        if (!exportUS.SetupError())
        {
            mWindow.AddWindow(exportUS);
            exportUS.ShowDialog((Window)mWindow);
        }
    }

    private void Export_Runsignup_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Export Runsignup Clicked.");
        if (theEvent!.EventType == Constants.Timing.EVENT_TYPE_TIME)
        {
            DialogBox.Show("Exporting time based events not supported.");
            return;
        }
        ExportDistanceResults exportRunsignup = new(mWindow, database, OutputType.Runsignup);
        if (!exportRunsignup.SetupError())
        {
            mWindow.AddWindow(exportRunsignup);
            exportRunsignup.ShowDialog((Window)mWindow);
        }
    }

    private void Expander_Expanded(object? sender, RoutedEventArgs e)
    {
        if (RemoteReadersButton == null) { return; }
        if (ReaderExpander.IsExpanded == true && remote_api)
        {
            RemoteReadersButton.IsVisible = true;
        }
        else
        {
            RemoteReadersButton.IsVisible = false;
        }
    }

    private void RemoteReadersButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Remote readers button clicked.");
        RemoteReadersWindow win = RemoteReadersWindow.CreateWindow(mWindow, database);
        win.Show();
    }

    private async void RemoteControllerSwitch_Checked(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Remote toggle switch checked.");
        if (RemoteControllerSwitch.IsChecked == false)
        {
            RemoteControllerSwitch.IsEnabled = false;
            mWindow.StopRemote();
        }
        else
        {
            RemoteControllerSwitch.IsEnabled = false;
            mWindow.StartRemote();
        }
    }

    private void ReaderMessageButton_Click(object? sender, RoutedEventArgs e)
    {
        ReaderNotificationWindow notificationWindow = ReaderNotificationWindow.NewWindow(mWindow);
        notificationWindow.Show();
    }

    private void StartTime_LostFocus(object? sender, FocusChangedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", $"Start Time Box has lost focus. {StartTime.Text}");
        if (StartTime.Text!.Any(char.IsDigit))
        {
            StartTimeChanged();
        }
    }

    private void CondenseSwitch_Checked(object? sender, RoutedEventArgs e)
    {
        List<DistanceStat> inStats = database.GetDistanceStats(theEvent!.Identifier, CondenseSwitch.IsChecked == false);
        stats.Clear();
        foreach (DistanceStat s in inStats)
        {
            stats.Add(s);
        }
    }

    private void StatsExpander_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (StatsExpander == null || CondenseSwitch == null) { return; }
        CondenseSwitch.IsVisible = StatsExpander.IsExpanded == true;
    }
}