using Avalonia.Controls;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.UI.Participants;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Chronokeep.UI.MainPages.Timing;

public partial class DistanceStatsPage : UserControl, ISubPage
{
    private readonly IdbInterface database;
    private readonly IMainWindow window;
    private readonly TimingPage parent;
    private readonly Event? theEvent;
    private readonly int distanceId;
    private readonly bool condensed;

    private readonly ObservableCollection<StatsParticipant> activeParticipants = [];
    private readonly ObservableCollection<Participant> dnsParticipants = [];
    private readonly ObservableCollection<Participant> unknownParticipants = [];
    private readonly ObservableCollection<Participant> dnfParticipants = [];
    private readonly ObservableCollection<Participant> finishedParticipants = [];

    public DistanceStatsPage(TimingPage parent, IMainWindow window, IdbInterface database, int distanceId, string distanceName, bool condensed = false)
    {
        InitializeComponent();
        this.parent = parent;
        this.window = window;
        this.database = database;
        this.distanceId = distanceId;
        this.condensed = condensed;
        theEvent = database.GetCurrentEvent();
        if (theEvent == null || theEvent.Identifier < 0)
        {
            Log.E("UI.Timing.DivisionStatsPage", "Something went wrong and no proper event was returned.");
            return;
        }
        Participant.SetCurrentEventDate(theEvent.Date);
        ActiveListView.ItemsSource = activeParticipants;
        DnsListView.ItemsSource = dnsParticipants;
        UnknownListView.ItemsSource = unknownParticipants;
        DnfListView.ItemsSource = dnfParticipants;
        FinishedListView.ItemsSource = finishedParticipants;
        this.DistanceName.Text = distanceName;
        parent.SetReaders([], false);
        UpdateView();
    }

    public void CancelableUpdateView(CancellationToken token) { }

    public void Search(CancellationToken token) { }

    public void Closing() { }

    public void EditSelected() { }

    public void KeyboardCtrlA() { }

    public void KeyboardCtrlS() { }

    public void KeyboardCtrlZ() { }

    public void Show(PeopleType type) { }

    public void SortBy(SortType type) { }

    public void UpdateView()
    {
        activeParticipants.Clear();
        dnsParticipants.Clear();
        unknownParticipants.Clear();
        dnfParticipants.Clear();
        finishedParticipants.Clear();
        Dictionary<int, List<Participant>> partDict = database.GetDistanceParticipantsStatus(theEvent!.Identifier, distanceId);
        if (condensed)
        {
            foreach (Distance d in database.GetDistances(theEvent.Identifier))
            {
                if (d.LinkedDistance == Constants.Timing.DISTANCE_DUMMYIDENTIFIER ||
                    d.LinkedDistance != distanceId) continue;
                Dictionary<int, List<Participant>> partDictLinked = database.GetDistanceParticipantsStatus(theEvent.Identifier, d.Identifier);
                foreach (int status in partDictLinked.Keys)
                {
                    if (!partDict.TryGetValue(status, out List<Participant>? pList))
                    {
                        pList = [];
                    }
                    pList.AddRange(partDictLinked[status]);
                    pList.Sort(Participant.CompareByName);
                    partDict[status] = pList;
                }
            }
        }
        // Bib dictionary to add LastSeen string to active participants for display.
        Dictionary<string, TimeResult> lastSeenDictionary = [];
        foreach (TimeResult timeResult in database.GetLastSeenResults(theEvent.Identifier).Where(timeResult => timeResult.Bib != Constants.Timing.CHIPREAD_DUMMYBIB && timeResult.Bib.Length > 0))
        {
            lastSeenDictionary[timeResult.Bib] = timeResult;
        }
        if (partDict.TryGetValue(Constants.Timing.EVENTSPECIFIC_STARTED, out List<Participant>? oActiveList)) // ACTIVE
        {
            ActivePanel.IsVisible = true;
            foreach (Participant p in oActiveList)
            {
                bool lastSeenExists = lastSeenDictionary.TryGetValue(p.Bib, out TimeResult? oLastSeenRes);
                string lastSeen = lastSeenExists ? oLastSeenRes!.SegmentName : "";
                string lastSeenTime = lastSeenExists ? oLastSeenRes!.SysTime : "";
                activeParticipants.Add(new StatsParticipant(p, lastSeen, lastSeenTime));
            }
        }
        else
        {
            ActivePanel.IsVisible = false;
        }
        if (partDict.TryGetValue(Constants.Timing.EVENTSPECIFIC_DNS, out List<Participant>? oDnsList)) // DNS
        {
            DnsPanel.IsVisible = true;
            foreach (Participant p in oDnsList)
            {
                dnsParticipants.Add(p);
            }
        }
        else
        {
            DnsPanel.IsVisible = false;
        }
        if (partDict.TryGetValue(Constants.Timing.EVENTSPECIFIC_UNKNOWN, out List<Participant>? oUnknownList)) // UNKOWN
        {
            UnknownPanel.IsVisible = true;
            foreach (Participant p in oUnknownList)
            {
                unknownParticipants.Add(p);
            }
        }
        else
        {
            UnknownPanel.IsVisible = false;
        }
        if (partDict.TryGetValue(Constants.Timing.EVENTSPECIFIC_DNF, out List<Participant>? oDnfList)) // DNF
        {
            DnfPanel.IsVisible = true;
            foreach (Participant p in oDnfList)
            {
                dnfParticipants.Add(p);
            }
        }
        else
        {
            DnfPanel.IsVisible = false;
        }
        if (partDict.TryGetValue(Constants.Timing.EVENTSPECIFIC_FINISHED, out List<Participant>? oFinishedList)) // FINISHED
        {
            FinishedPanel.IsVisible = true;
            foreach (Participant p in oFinishedList)
            {
                finishedParticipants.Add(p);
            }
        }
        else
        {
            FinishedPanel.IsVisible = false;
        }
    }

    public void Location(string location) { }

    public void Reader() { }

    private void ListView_MouseDoubleClick(object? sender, TappedEventArgs e)
    {
        Log.D("UI.Timing.DistanceStatsPage", "Mouse double clicked in a listview.");
        if (sender is not DataGrid listView) return;
        if (listView.SelectedItem == null) return;
        Participant? selected;
        if (listView.SelectedItem is StatsParticipant participant)
        {
            selected = participant.GetParticipant();
        }
        else
        {
            selected = listView.SelectedItem as Participant;
        }
        ModifyParticipantWindow modifyParticipant = new(window, database, selected!);
        modifyParticipant.ShowDialog((Window)window);
    }

    private void DoneButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.DistanceStatsPage", "Done button clicked.");
        parent.LoadMainDisplay();
    }
}