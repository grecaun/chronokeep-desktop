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
    private readonly IdbInterface database;
    private ISubPage? subPage;

    private CancellationTokenSource? cts;

    private readonly Event? theEvent;
    private readonly List<TimingLocation>? locations;

    private DateTime startTime;
    private readonly DispatcherTimer timer = new();
    private bool timerStarted;
    private SetTimeWindow? timeWindow;
    private RewindWindow? rewindWindow;

    private static bool alreadyRecalculating;
    private const int UploadTimer = 1000;

    private readonly ObservableCollection<DistanceStat> stats = [];

    private int total, known;

    private const string IpFormat = "{0:D}.{1:D}.{2:D}.{3:D}";
    private readonly int[] baseIp = [0, 0, 0, 0];

    private readonly bool remoteApi;

    private readonly Dictionary<int, (long seconds, int milliseconds)> waveTimes = [];
    private readonly HashSet<int> waves = [];
    private int selectedWave = -1;
    private readonly List<TimeRelativeWave> relativeToWaveList = [];

    [GeneratedRegex(@"[^a-z0-9\-]")]
    private static partial Regex FileSaveRegex();

    private readonly bool loaded;

    public TimingPage(IMainWindow window, IdbInterface database)
    {
        InitializeComponent();
        this.database = database;
        mWindow = window;
        theEvent = database.GetCurrentEvent();
        ViewOnlyBox.SelectedIndex = 0;
        SortBy.SelectedIndex = 0;
        loaded = true;

        if (theEvent == null || theEvent.Identifier == -1)
        {
            return;
        }

        // Set up the running clock.
        timer.Tick += Timer_Tick;
        timer.Interval = new TimeSpan(0, 0, 0, 0, 100);

        string webAddress = $"localhost:{6933}";
        WebBlock.Text = webAddress;
        WebButton.NavigateUri = new Uri(webAddress);
        // Check for default IP address to give to our reader boxes for connections
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.NetworkInterfaceType != NetworkInterfaceType.Ethernet ||
                adapter.OperationalStatus != OperationalStatus.Up) continue;
            if (adapter.GetIPProperties().GatewayAddresses.FirstOrDefault() == null) continue;
            foreach (UnicastIPAddressInformation ipinfo in adapter.GetIPProperties().UnicastAddresses)
            {
                if (ipinfo.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                webAddress = $"http://{ipinfo.Address.ToString()}:{6933}";
                WebBlock.Text = webAddress;
                WebButton.NavigateUri = new Uri(webAddress);
                Log.D("UI.MainPages.TimingPage", $"IP Address :{ipinfo.Address}");
                Log.D("UI.MainPages.TimingPage", $"IPv4 Mask  :{ipinfo.IPv4Mask}");
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

        // Check for multiple wave times, show an elapsed relative to box if so
        waves.Clear();
        waveTimes.Clear();
        relativeToWaveList.Add(new TimeRelativeWave
        {
            Name = "Start Time",
            Wave = -1
        });
        foreach (Distance div in database.GetDistances(theEvent!.Identifier))
        {
            relativeToWaveList.Add(new TimeRelativeWave
            {
                Name = $"{div.Name} (Wave {div.Wave})",
                Wave = div.Wave
            });
            waveTimes[div.Wave] = (div.StartOffsetSeconds, div.StartOffsetMilliseconds);
            waves.Add(div.Wave);
        }
        ElapsedRelativeToBox.ItemsSource = relativeToWaveList;
        ElapsedRelativeToBox.SelectedIndex = 0;

        // Check if we've already started the event.  Show a clock if we have.
        if (theEvent is { StartSeconds: >= 0 })
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
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_ANNOUNCER, theEvent.Identifier, "Announcer", 0, 0));
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Finish", theEvent.FinishMaxOccurrences, theEvent.FinishIgnoreWithin));
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_START, theEvent.Identifier, "Start", 0, theEvent.StartWindow));
        }
        else
        {
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_ANNOUNCER, theEvent.Identifier, "Announcer", 0, 0));
            locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Start/Finish", theEvent.FinishMaxOccurrences, theEvent.FinishIgnoreWithin));
        }

        LocationBox.Items.Clear();
        if (locCount > 0)
        {
            LocationBox.Items.Add(new ComboBoxItem()
            {
                Content = "All Locations"
            });
            foreach (TimingLocation loc in locations.Where(loc => !loc.Name.Equals("Announcer", StringComparison.OrdinalIgnoreCase)))
            {
                LocationBox.Items.Add(new ComboBoxItem()
                {
                    Content = loc.Name,
                });
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
            Log.D("UI.MainPages.TimingPage", $"{systems.Count} systems found.");
            for (int i = 0; i < 3 - numSystems; i++)
            {
                systems.Add(new TimingSystem(string.Format(IpFormat, baseIp[0], baseIp[1], baseIp[2], baseIp[3]), system));
            }
        }
        systems.Sort((x, y) => x.Status == y.Status ? string.Compare(x.IpAddress, y.IpAddress, StringComparison.Ordinal) : x.Status.CompareTo(y.Status));
        systems.Add(new TimingSystem(string.Format(IpFormat, baseIp[0], baseIp[1], baseIp[2], baseIp[3]), system));
        known = 0;
        foreach (TimingSystem sys in systems)
        {
            ReadersBox.Items.Add(new ReaderPart(this, sys, locations));
            if (sys.IpAddress != string.Format(IpFormat, baseIp[0], baseIp[1], baseIp[2], baseIp[3]))
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
            DnfButton.Content = "Add Finished";
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
        if (theEvent.ApiId > 0 && theEvent.ApiEventId.Length > 1)
        {
            ApiPanel.IsVisible = true;
        }
        else
        {
            ApiPanel.IsVisible = false;
        }
        if (mWindow.IsApiControllerRunning())
        {
            AutoApiButton.Content = "Stop Uploads";
            ManualApiButton.IsEnabled = false;
        }
        else
        {
            AutoApiButton.Content = "Auto Upload";
            ManualApiButton.IsEnabled = true;
        }

        RemoteReadsController.RemoteStatus rStatus = mWindow.IsRemoteRunning();
        switch (rStatus)
        {
            case RemoteReadsController.RemoteStatus.RUNNING:
                RemoteControllerSwitch.IsChecked = true;
                RemoteErrorsBlock.Text = mWindow.RemoteErrors() > 0 ? mWindow.RemoteErrors().ToString() : "";
                RemoteControllerSwitch.IsEnabled = true;
                break;
            case RemoteReadsController.RemoteStatus.STOPPED:
                RemoteControllerSwitch.IsChecked = false;
                RemoteControllerSwitch.IsEnabled = true;
                RemoteErrorsBlock.Text = "";
                break;
            case RemoteReadsController.RemoteStatus.UNKNOWN:
            default:
                break;
        }

        UpdateDnsButton();

        // check if we have a remote api set up
        if (database.GetAllApi().Any(api => api.Type is ApiConstants.CHRONOKEEP_REMOTE_SELF or ApiConstants.CHRONOKEEP_REMOTE))
        {
            RemoteControllerSwitch?.IsVisible = true;
            RemoteReadersButton?.IsVisible = ReaderExpander.IsExpanded;
            remoteApi = true;
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
        RecalculateButton.Content = alreadyRecalculating ? "Working..." : "Recalculate";
    }

    public void KeyboardCtrlA() { }

    public void KeyboardCtrlS() { }

    public void KeyboardCtrlZ() { }

    public static void UpdateDatabase() { }

    public void Closing()
    {
        List<TimingSystem> removedSystems = database.GetTimingSystems();
        List<TimingSystem> ourSystems = [];
        foreach (ReaderPart? box in ReadersBox.Items)
        {
            box?.UpdateReader();
            if (box != null && box.Reader.IpAddress != "0.0.0.0" && box.Reader.IpAddress.Length > 7 &&
                box.Reader.IpAddress != string.Format(IpFormat, baseIp[0], baseIp[1], baseIp[2], baseIp[3]))
            {
                ourSystems.Add(box.Reader);
            }
        }
        removedSystems.RemoveAll(ourSystems.Contains);
        foreach (TimingSystem sys in removedSystems)
        {
            database.RemoveTimingSystem(sys);
        }
        foreach (TimingSystem sys in ourSystems)
        {
            database.AddTimingSystem(sys);
        }
        timer.Stop();
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
        if (timerStarted)
        {
            UpdateStartTime();
        }

        // Update locations in the list of readers (and reader status)
        total = ReadersBox.Items.Count; known = 0;
        foreach (ReaderPart? read in ReadersBox.Items.Cast<ReaderPart?>())
        {
            read!.UpdateStatus();
            if (read.Reader.Status == SYSTEM_STATUS.DISCONNECTED)
            {
                if (timeWindow != null && timeWindow.IsTimingSystem(read.Reader))
                {
                    timeWindow.Close();
                    timeWindow = null;
                }
                if (rewindWindow != null && rewindWindow.IsTimingSystem(read.Reader))
                {
                    rewindWindow.Close();
                    rewindWindow = null;
                }
            }
            if (read.Reader.IpAddress != string.Format(IpFormat, baseIp[0], baseIp[1], baseIp[2], baseIp[3]))
            {
                known++;
            }
        }

        if (total < 4 || known >= total)
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
            for (int i = total; i < 3; i++)
            {
                ReadersBox.Items.Add(new ReaderPart(
                    this,
                    new TimingSystem(string.Format(IpFormat, baseIp[0], baseIp[1], baseIp[2], baseIp[3]),
                        system),
                        locations!));
            }
            ReadersBox.Items.Add(new ReaderPart(
                this,
                new TimingSystem(string.Format(IpFormat, baseIp[0], baseIp[1], baseIp[2], baseIp[3]),
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
        if (theEvent.ApiId > 0 && theEvent.ApiEventId.Length > 1)
        {
            ApiPanel.IsVisible = true;
        }
        else
        {
            ApiPanel.IsVisible = false;
        }
        if (mWindow.IsApiControllerRunning())
        {
            AutoApiButton.Content = mWindow.ApiErrors() > 0 ? $"Stop Uploads ({mWindow.ApiErrors()})" : "Stop Uploads";
            ManualApiButton.IsEnabled = false;
        }
        else
        {
            AutoApiButton.Content = "Auto Upload";
            ManualApiButton.IsEnabled = true;
        }

        RemoteReadsController.RemoteStatus rStatus = mWindow.IsRemoteRunning();
        switch (rStatus)
        {
            case RemoteReadsController.RemoteStatus.RUNNING:
                RemoteControllerSwitch.IsChecked = true;
                RemoteControllerSwitch.IsEnabled = true;
                RemoteErrorsBlock.Text = mWindow.RemoteErrors() > 0 ? mWindow.RemoteErrors().ToString() : "";
                break;
            case RemoteReadsController.RemoteStatus.STOPPED:
                RemoteControllerSwitch.IsChecked = false;
                RemoteControllerSwitch.IsEnabled = true;
                RemoteErrorsBlock.Text = "";
                break;
        }

        UpdateDnsButton();

        List<ReaderMessage> readerMsgs = GetReaderMessages();
        if (readerMsgs.Count > 0)
        {
            ReaderMessageButton.IsVisible = true;
            int count = readerMsgs.FindAll(x => !x.Notified).Count;
            ReaderMessageButton.Content = count.ToString();
            ReaderMessageButton.Background = Brush.Parse(count > 0 ? "orange" : "#479ef5");
        }
        else
        {
            ReaderMessageButton.IsVisible = false;
            ReaderMessageButton.Content = 0.ToString();
        }
        UpdateSubView();
        RecalculateButton.Content = alreadyRecalculating ? "Working..." : "Recalculate";
    }

    public void UpdateSubView()
    {
        Log.D("UI.MainPages.TimingPage", "Updating sub view.");
        cts?.Cancel();
        cts = new CancellationTokenSource();
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
        ElapsedTime.Text = Constants.Timing.SecondsToTime(Math.Abs(unixElapsed / 1000));
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
        if (oldStartSeconds == theEvent.StartSeconds && oldStartMilliseconds == theEvent.StartMilliseconds) return;
        database.UpdateEvent(theEvent);
        database.UpdateStart(); // This is a MemStore specific database call that updates the Start value for ChipReads.
        database.ResetTimingResultsEvent(theEvent.Identifier);
        UpdateView();
        mWindow.NetworkClearResults();
        mWindow.NotifyTimingWorker();
    }

    private void UpdateStartTime()
    {
        if (!timerStarted)
        {
            timerStarted = true;
            timer.Start();
        }
        string startTimeValue = StartTime.Text!.Replace('_', '0');
        StartRace.IsEnabled = false;
        ElapsedRelativeToBox.IsVisible = waves.Count > 1;
        StartTime.Text = startTimeValue;
        Log.D("UI.MainPages.TimingPage", $"Start time is {startTimeValue}");
        startTime = DateTime.ParseExact($"{startTimeValue}{DateTime.Parse(theEvent!.Date):ddMMyyyy}", "HH:mm:ss.fffddMMyyyy", null);
        Log.D("UI.MainPages.TimingPage", $"Start time is {startTime:yyyy-MM-dd HH:mm:ss.fff}");
    }

    public void NewMessage()
    {
        timeWindow?.UpdateTime();
    }

    public void OpenTimeWindow(TimingSystem system)
    {
        Log.D("UI.MainPages.TimingPage", "Opening Set Time Window.");
        timeWindow = new SetTimeWindow(this, system);
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
            try
            {
                if (sys.Status != SYSTEM_STATUS.CONNECTED) continue;
                sys.SystemInterface?.SetTime(now ? DateTime.Now : time);
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
        ReaderPart? removed = ReadersBox.Items.Cast<ReaderPart?>().FirstOrDefault(box => box!.Reader.SystemIdentifier == sys.SystemIdentifier && sys.Saved());
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

    private void SetRawReadsFinished()
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
        if (ViewOnlyBox.SelectedItem == null) return PeopleType.KNOWN;
        return ((ComboBoxItem)ViewOnlyBox.SelectedItem!).Content switch
        {
            "Show All" => PeopleType.ALL,
            "Show Only Starts" => PeopleType.STARTS,
            "Show Only Finishes" => PeopleType.FINISHES,
            "Show Only Unknown" => PeopleType.UNKNOWN,
            "Show Only Unknown Finishes" => PeopleType.UNKNOWN_FINISHES,
            "Show Only Unknown Starts" => PeopleType.UNKNOWN_STARTS,
            _ => PeopleType.KNOWN
        };
    }

    public SortType GetSortType()
    {
        if (SortBy.SelectedItem == null) return SortType.SYSTIME;
        return ((ComboBoxItem)SortBy.SelectedItem).Content switch
        {
            "Clock Time" => SortType.GUNTIME,
            "Bib" => SortType.BIB,
            "Distance" => SortType.DISTANCE,
            "Age Group" => SortType.AGEGROUP,
            "Gender" => SortType.GENDER,
            "Place" => SortType.PLACE,
            _ => SortType.SYSTIME
        };
    }

    public string GetSearchValue()
    {
        return SearchBox?.Text == null ? "" : SearchBox.Text.Trim();
    }

    public string GetLocation()
    {
        if (LocationBox.SelectedItem == null) return "";
        ComboBoxItem locItem = (ComboBoxItem)LocationBox.SelectedItem;
        return locItem.Content!.ToString()!;
    }

    private async void UploadResults()
    {
        try
        {
            // Get API to upload.
            if (theEvent!.ApiId < 0 && theEvent.ApiEventId.Length > 1)
            {
                return;
            }
            ApiObject api = database.GetApi(theEvent.ApiId)!;
            string[] eventIds = theEvent.ApiEventId.Split(',');
            if (eventIds.Length != 2)
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
                Application.Current!.Dispatcher.Invoke(delegate
                {
                    ManualApiButton.Content = "Manual Upload";
                });
                return;
            }
            // Upload results
            Log.D("UI.MainPages.TimingPage", $"Results count: {results.Count}");
            if (ApiController.GetUploadable(3000))
            {
                await ApiController.UploadResults(results, api, eventIds, database, null, null, theEvent);
            }
            Application.Current!.Dispatcher.Invoke(delegate
            {
                ManualApiButton.Content = "Manual Upload";
            });
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.TimingPage", "Error uploading results.");
        }
    }

    private void UpdateDnsButton()
    {
        DnsMode.Content = mWindow.InDidNotStartMode() ? "Stop DNS Mode" : "Start DNS Mode";
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

    private void ElapsedRelativeToBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Log.E("UI.MainPages.TimingPage", "ElapsedRelativeToBox selection changed.");
        selectedWave = -1;
        if (ElapsedRelativeToBox.SelectedIndex >= 0 && ElapsedRelativeToBox.SelectedItem is TimeRelativeWave wave)
        {
            selectedWave = wave.Wave;
        }
    }

    private void StartTimeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Return) return;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Log.D("UI.MainPages.TimingPage", "User wants to reset start time value.");
            theEvent!.StartSeconds = 0;
            theEvent!.StartMilliseconds = 0;
            if (timerStarted)
            {
                timerStarted = false;
                timer.Stop();
            }
            database.UpdateEvent(theEvent);
            StartTime.Text = "";
            ElapsedTime.Text = "00:00:00";
            StartRace.IsEnabled = true;
            ElapsedRelativeToBox.IsEnabled = false;
            ElapsedRelativeToBox.IsVisible = false;
            return;
        }
        Log.D("UI.MainPages.TimingPage", "Start Time Box return key found.");
        UpdateStartTime();
    }

    private void StartRaceClick(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Starting race.");
        StartTime.Text = DateTime.Now.ToString("HH:mm:ss.fff");
        StartRace.IsEnabled = false;
        ElapsedRelativeToBox.IsEnabled = true;
        ElapsedRelativeToBox.IsVisible = waves.Count > 1;
        foreach (Chronoclock clock in database.GetClocks().Where(clock => clock.Enabled))
        {
            try
            {
                _ = clock.StartCountUp();
            }
            catch
            {
                Log.D("UI.MainPages.TimingPage", "Error starting countup.");
            } // Exception may get thrown due to not waiting on the async method
            // The clocks need to start as fast as possible, and it does not matter if the
            // call fails (the clock is probably not connected to the same network)
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
        WaveWindow waveWin = new(mWindow, database);
        mWindow.AddWindow(waveWin);
        waveWin.ShowDialog((Window)mWindow);
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
        if (ManualEntryWindow.NewWindow(mWindow, database) is not { } manualEntryWindow) return;
        mWindow.AddWindow(manualEntryWindow);
        manualEntryWindow.ShowDialog((Window)mWindow);
    }

    private void ManualEntry(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Manual Entry selected.");
        ManualEntryWindow manualEntryWindow = ManualEntryWindow.NewWindow(mWindow, database, locations!);
        mWindow.AddWindow(manualEntryWindow);
        manualEntryWindow.ShowDialog((Window)mWindow);
    }

    private async void LoadLog(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.MainPages.TimingPage", "Loading from log.");
            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            IStorageFolder? startingFolder;
            try
            {
                startingFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(database.GetAppSetting(Settings.DEFAULT_EXPORT_DIR)!.Value));
            }
            catch
            {
                startingFolder = null;
            }
            IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                FileTypeFilter = [Utils.LogType, FilePickerFileTypes.All],
                AllowMultiple = false,
                SuggestedStartLocation = startingFolder,
            });
            if (files.Count <= 0) return;
            try
            {
                LogImporter importer = new(files[0].TryGetLocalPath()!);
                await Task.Run(importer.FindType);
                ImportLogWindow logWindow = ImportLogWindow.NewWindow(mWindow, importer, database);
                mWindow.AddWindow(logWindow);
                await logWindow.ShowDialog((Window)mWindow);
            }
            catch (Exception ex)
            {
                Log.E("UI.MainPages.TimingPage", "Something went wrong when trying to read the CSV file.");
                Log.E("UI.MainPages.TimingPage", ex.StackTrace!);
            }
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.TimingPage", "Error loading logs.");
        }
    }

    private async void SaveLog(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.MainPages.TimingPage", "Save Log clicked.");
            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            IStorageFolder? startingFolder;
            try
            {
                startingFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(database.GetAppSetting(Settings.DEFAULT_EXPORT_DIR)!.Value));
            }
            catch
            {
                startingFolder = null;
            }
            IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                FileTypeChoices = [Utils.CsvType],
                SuggestedFileName = $"{theEvent!.YearCode} {theEvent.Name} Log.csv",
                SuggestedStartLocation = startingFolder,
            });
            if (file is null) return;
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
            Log.D("UI.MainPages.TimingPage", $"The format is '{format}'");
            if (locationReadDict.Keys.Count == 1)
            {
                List<object[]> data = [];
                data.AddRange(chipReads.Select(read => (object[])[read.Status, read.ChipNumber, read.Seconds, read.Milliseconds, read.TimeSeconds, read.TimeMilliseconds, read.Antenna, read.Reader, read.Box, read.LogId, read.Rssi, read.IsRewind, read.ReaderTime, read.StartTime, read.ReadBib, read.Type]));
                CsvExporter exporter = new(format.ToString());
                exporter.SetData(headers, data);
                exporter.ExportData(file.TryGetLocalPath()!);
            }
            // Multiple locations, save each individually.
            else
            {
                foreach (string key in locationReadDict.Keys)
                {
                    List<object[]> data = [];
                    data.AddRange(locationReadDict[key].Select(read => (object[])[read.Status, read.ChipNumber, read.Seconds, read.Milliseconds, read.TimeSeconds, read.TimeMilliseconds, read.Antenna, read.Reader, read.Box, read.LogId, read.Rssi, read.IsRewind, read.ReaderTime, read.StartTime, read.ReadBib, read.Type]));
                    CsvExporter exporter = new(format.ToString());
                    exporter.SetData(headers, data);
                    string outFileName =
                        $"{Path.GetDirectoryName(file.TryGetLocalPath()!)}\\{FileSaveRegex().Replace(key.ToLower(), "")}-{file.Name}";
                    Log.D("UI.MainPages.TimingPage", $"Saving file to: {outFileName}");
                    exporter.ExportData(outFileName);
                }
            }
            DialogBox.AsyncShow("File saved.");
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.TimingPage", "Error saving log.");
        }
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Search box text has changed");
        cts?.Cancel();
        cts = null;
        cts = new CancellationTokenSource();
        try
        {
            subPage!.Search(cts.Token);
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
        cts = null;
        cts = new CancellationTokenSource();
        try
        {
            subPage!.Search(cts.Token);
            cts = null;
        }
        catch
        {
            Log.D("UI.MainPages.TimingPage", "Update cancelled.");
        }
    }

    private void ReaderSelectionBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!loaded) { return; }
        cts = null;
        cts = new CancellationTokenSource();
        try
        {
            subPage!.Search(cts.Token);
            cts = null;
        }
        catch
        {
            Log.D("UI.MainPages.TimingPage", "Update cancelled.");
        }
    }

    private void LocationBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!loaded) { return; }
        if (subPage == null)
        {
            return;
        }
        cts = null;
        cts = new CancellationTokenSource();
        try
        {
            subPage!.Search(cts.Token);
            cts = null;
        }
        catch
        {
            Log.D("UI.MainPages.TimingPage", "Update cancelled.");
        }
    }

    private void SortBy_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!loaded) { return; }
        cts = null;
        cts = new CancellationTokenSource();
        try
        {
            subPage!.Search(cts.Token);
            cts = null;
        }
        catch
        {
            Log.D("UI.MainPages.TimingPage", "Update cancelled.");
        }
    }

    private void StatsListView_MouseDoubleClick(object? sender, TappedEventArgs e)
    {
        DistanceStat selected = (DistanceStat)StatsListView.SelectedItem;
        if (selected == null)
        {
            return;
        }
        Log.D("UI.MainPages.TimingPage", $"Stats double clicked. Distance is {selected.DistanceName}");
        SetRawReadsFinished();
        subPage = new DistanceStatsPage(this, mWindow, database, selected.DistanceId, selected.DistanceName, CondenseSwitch.IsChecked == false);
        TimingFrame.Content = subPage;
    }

    private async void Recalculate_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.MainPages.TimingPage", "Recalculate results clicked.");
            if ((string)RecalculateButton.Content! == "Working..." || alreadyRecalculating)
            {
                return;
            }
            RecalculateButton.Content = "Working...";
            alreadyRecalculating = true;
            if (ApiController.SetUploadableFalse(UploadTimer))
            {
                bool canRecalculate = await Task.Run(() =>
                {
                    int counter = 0;
                    while (true)
                    {
                        if (counter > 5)
                        {
                            return false;
                        }
                        if (!ApiController.IsUploading())
                        {
                            return true;
                        }
                        counter++;
                        //Log.D("UI.MainPages.TimingPage", $"APIController is uploading. Sleeping for 1 second. Counter is {counter.ToString()}");
                        Thread.Sleep(1000);
                    }
                });
                if (!canRecalculate)
                {
                    await Task.Run(() =>
                    {
                        int counter = 0;
                        while (!ApiController.SetUploadableTrue(UploadTimer))
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
                    DialogBox.AsyncShow("Unable to recalculate results.");
                    return;
                }
            }
            else
            {
                RecalculateButton.Content = "Recalculate";
                alreadyRecalculating = false;
                return;
            }
            ApiObject? api = null;
            try
            {
                api = database.GetApi(theEvent!.ApiId);
                Log.D("UI.MainPages.TimingPage", "API found.");
            }
            catch
            {
                Log.D("UI.MainPages.TimingPage", "Error getting API while recalculating.");
            }
            // Get the event id values. Exit if not valid.
            string[] eventIds = theEvent!.ApiEventId.Split(',');
            Log.D("UI.MainPages.TimingPage", $"Event Id's found: {eventIds.Length} API is null? {api == null}");
            if (eventIds.Length == 2 && api != null)
            {
                try
                {
                    Log.D("UI.MainPages.TimingPage", "Deleting results from API.");
                    if (theEvent.UploadSpecific)
                    {
                        foreach (Distance d in database.GetDistances(theEvent.Identifier).Where(d => d is { Upload: true, LinkedDistance: Constants.Timing.DISTANCE_NO_LINKED_ID }))
                        {
                            await ApiController.DeleteResults(api, eventIds[0], eventIds[1], d.Name);
                        }
                    }
                    else
                    {
                        await ApiController.DeleteResults(api, eventIds[0], eventIds[1], null);
                    }
                }
                catch (ApiException ex)
                {

                    DialogBox.AsyncShow(ex.Message);
                }
            }
            // We do this because we want to ensure we've reset all the results before we allow
            // the auto uploader to start uploading any more results so we don't upload
            // old results over our brand-new results.
            database.ResetTimingResultsEvent(theEvent.Identifier);
            await Task.Run(() =>
            {
                int counter = 0;
                while (!ApiController.SetUploadableTrue(UploadTimer))
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
        catch (Exception)
        {
            Log.D("UI.MainPages.TimingPage", "Error recalculating.");
        }
    }

    private void AutoAPI_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Auto API clicked.");
        if ((string)AutoApiButton.Content! == "Auto Upload")
        {
            AutoApiButton.Content = "Starting...";
            mWindow.StartApiController();
        }
        else
        {
            AutoApiButton.Content = "Stopping...";
            mWindow.StopApiController();
        }
    }

    private async void ManualAPI_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.MainPages.TimingPage", "Manual API clicked.");
            if (ManualApiButton.Content!.ToString() != "Uploading")
            {
                Log.D("UI.MainPages.TimingPage", "Uploading data.");
                ManualApiButton.Content = "Uploading";
                await Task.Run(UploadResults);
                return;
            }
            Log.D("UI.MainPages.TimingPage", "Already uploading.");
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.TimingPage", "Error on manual upload.");
        }
    }

    private async void SendEmailsButton_Click(object? sender, RoutedEventArgs e)
    {
        try
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
                foreach (int esId in idents)
                {
                    sentIDs.Add(esId);
                }
                List<TimeResult> finishTimes = database.GetFinishTimes(theEvent.Identifier);
                ApiObject api = database.GetApi(theEvent.ApiId)!;
                Dictionary<string, Participant> participantDictionary = [];
                foreach (Participant p in database.GetParticipants(theEvent.Identifier))
                {
                    participantDictionary[p.Identifier.ToString()] = p;
                }

                int distances = database.GetDistances(theEvent.Identifier).Count(d => Constants.Timing.DISTANCE_NO_LINKED_ID == d.LinkedDistance);
                GlobalVars.UpdateBannedEmails();
                HttpClient client = new();
                MailgunCredentials credentials = MailgunCredentials.GetCredentials(database);
                if (!credentials.Valid())
                {
                    return;
                }
                string base64String = Convert.ToBase64String(Encoding.ASCII.GetBytes(
                    $"{credentials.Username}:{credentials.ApiKey}"));
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(@"Basic", base64String);
                foreach (TimeResult result in finishTimes)
                {
                    Participant? part = participantDictionary.GetValueOrDefault(result.ParticipantId);
                    if (part == null || result.EventSpecificId == Constants.Timing.EVENTSPECIFIC_UNKNOWN) continue;
                    if (part.Email.Length <= 0 || GlobalVars.BannedEmails.Contains(part.Email) ||
                        sentIDs.Contains(result.EventSpecificId)) continue;
                    MultipartFormDataContent postData = new()
                    {
                        { new StringContent(credentials.From()), "from" },
                        { new StringContent(part.Email), "to" },
                        { new StringContent($"{theEvent.Year} {theEvent.Name}"), "subject" },
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
                        client.PostAsync($"https://api.mailgun.net/v3/{credentials.Domain}/messages", postData);
                    }
                    catch
                    {
                        Log.E("UI.MainPages.TimingPage", "Error sending email.");
                    }
                    database.AddEmailAlert(theEvent.Identifier, result.EventSpecificId);
                }
                Log.D("UI.MainPages.TimingPage", "Async operation to send emails finished.");
            });
            Log.D("UI.MainPages.TimingPage", "Changing button back and sending dialog box.");
            DialogBox.AsyncShow("Emails sent.");
            SendEmailsButton.Content = "Send Emails";
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.TimingPage", "Error sending emails.");
        }
    }

    private void ModifySMSButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Modify SMS button clicked.");
        SmsWaveEnabledWindow smsWindow = new(mWindow, database);
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
            DialogBox.AsyncShow("An error occurred entering DNS mode.");
        }
        UpdateDnsButton();
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
                DialogBox.AsyncShow("Unable to start the web server. Please type this command in an elevated command prompt:", "netsh http add urlacl url=http://*:6933/ user=everyone");
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
        try
        {
            Log.D("UI.MainPages.TimingPage", "Create HTML clicked.");
            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            IStorageFolder? startingFolder;
            try
            {
                startingFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(database.GetAppSetting(Settings.DEFAULT_EXPORT_DIR)!.Value));
            }
            catch
            {
                startingFolder = null;
            }
            IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                FileTypeChoices = [Utils.HtmlType],
                SuggestedFileName = $"{theEvent!.YearCode} {theEvent.Name} Web.html",
                SuggestedStartLocation = startingFolder,
            });
            if (file is null) return;
            List<TimeResult> finishResults = database.GetFinishTimes(theEvent!.Identifier);
            HtmlResultsTemplate template = new(theEvent, finishResults);
            await File.WriteAllTextAsync(file.TryGetLocalPath()!, template.TransformText());
            DialogBox.AsyncShow("File saved.");
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.TimingPage", "Error creating HTML.");
        }
    }

    private void Export_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Export clicked.");
        ExportResults exportResults = new(mWindow, database);
        if (exportResults.SetupError()) return;
        mWindow.AddWindow(exportResults);
        exportResults.ShowDialog((Window)mWindow);
    }

    private void Export_BAA_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Export BAA Clicked.");
        if (theEvent!.EventType == Constants.Timing.EVENT_TYPE_TIME)
        {
            DialogBox.AsyncShow("Exporting time based events not supported.");
            return;
        }
        ExportDistanceResults exportBaa = new(mWindow, database);
        if (exportBaa.SetupError()) return;
        mWindow.AddWindow(exportBaa);
        exportBaa.ShowDialog((Window)mWindow);
    }

    private void Export_Abbott_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Export Abbott Clicked.");
        ExportDistanceResults exportAbbott = new(mWindow, database, OutputType.Abbott);
        if (exportAbbott.SetupError()) return;
        mWindow.AddWindow(exportAbbott);
        exportAbbott.ShowDialog((Window)mWindow);
    }

    private void Export_US_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Export UltraSignup Clicked.");
        if (theEvent!.EventType == Constants.Timing.EVENT_TYPE_TIME)
        {
            DialogBox.AsyncShow("Exporting time based events not supported.");
            return;
        }
        ExportDistanceResults exportUs = new(mWindow, database, OutputType.UltraSignup);
        if (exportUs.SetupError()) return;
        mWindow.AddWindow(exportUs);
        exportUs.ShowDialog((Window)mWindow);
    }

    private void Export_RunSignup_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.TimingPage", "Export RunSignup Clicked.");
        if (theEvent!.EventType == Constants.Timing.EVENT_TYPE_TIME)
        {
            DialogBox.AsyncShow("Exporting time based events not supported.");
            return;
        }
        ExportDistanceResults exportRunSignup = new(mWindow, database, OutputType.RunSignup);
        if (exportRunSignup.SetupError()) return;
        mWindow.AddWindow(exportRunSignup);
        exportRunSignup.ShowDialog((Window)mWindow);
    }

    private void Expander_Expanded(object? sender, RoutedEventArgs e)
    {
        if (RemoteReadersButton == null) { return; }
        if (ReaderExpander.IsExpanded && remoteApi)
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
        CondenseSwitch.IsVisible = StatsExpander.IsExpanded;
    }
}