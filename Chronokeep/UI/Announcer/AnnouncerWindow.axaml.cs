using Avalonia.Controls;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronokeepRemote;
using Chronokeep.Timing.Announcer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Chronokeep.UI.Announcer;

public partial class AnnouncerWindow : ChronokeepWindow
{
    private readonly IMainWindow window;
    private readonly IDBInterface database;

    private readonly Event? theEvent;

    public AnnouncerWindow(IMainWindow window, IDBInterface database)
    {
        InitializeComponent();
        this.window = window;
        this.database = database;
        theEvent = database.GetCurrentEvent();
        AnnouncerParticipant.TheEvent = theEvent;
        AnnouncerWorker announcerWorker1 = AnnouncerWorker.NewAnnouncer(window, database);
        Thread announcerThread1 = new(announcerWorker1.Run);
        announcerThread1.Start();
        UpdateView();
        UpdateTiming();
    }

    private void Window_Closing(object sender, WindowClosingEventArgs e)
    {
        Log.D("UI.Announcer.AnnouncerWindow", "Announcer window is closing!");
        AnnouncerWorker.Shutdown();
        window.AnnouncerClosing();
    }

    public void UpdateTiming()
    {
        if (theEvent == null) { return; }
        List<RemoteReader> readers = database.GetRemoteReaders(theEvent.Identifier);
        bool remoteAnnouncer = false;
        foreach (RemoteReader _ in readers.Where(reader => reader.LocationId == Constants.Timing.LOCATION_ANNOUNCER))
        {
            remoteAnnouncer = true;
        }
        if (window.AnnouncerConnected() || remoteAnnouncer) return;
        AnnouncerBox.IsVisible = false;
        ResultsBox.IsVisible = true;
        // Get our list of results to display.
        List<TimeResult> results;
        try
        {
            results = database.GetTimingResults(theEvent.Identifier);
        }
        catch (Exception)
        {
            Log.E("AnnouncerWindow", "Error getting results from database.");
            results = [];
        }
        // Ensure results are sorted.
        results.Sort(TimeResult.CompareBySystemTime);
        results.RemoveAll((x) => TimeResult.IsNotFinish(x) || x.IsDnf());
        DateTime cutoff = DateTime.Now.AddSeconds(-1 * Globals.AnnouncerWindow);
        // Remove all result values where x.SystemTime is less than 0 (i.e. cutoff occurred after x.SystemTime)
        results.RemoveAll((x) => DateTime.Compare(cutoff, x.SystemTime) > 0);
        // Reverse all entries so the last person to cross the line is at the top.
        results.Reverse();
        // Remove old entries.
        ResultsBox.ItemsSource = results;
    }

    public void UpdateView()
    {
        if (theEvent == null) { return; }
        List<RemoteReader> readers = database.GetRemoteReaders(theEvent.Identifier);
        bool remoteAnnouncer = false;
        foreach (RemoteReader _ in readers.Where(reader => reader.LocationId == Constants.Timing.LOCATION_ANNOUNCER))
        {
            remoteAnnouncer = true;
        }
        // Check if we've got an announcer reader connected.
        if (!window.AnnouncerConnected() && !remoteAnnouncer) return;
        AnnouncerBox.IsVisible = true;
        ResultsBox.IsVisible = false;
        // Get our list of people to display. Remove anything older than 45 seconds.
        List<AnnouncerParticipant> participants;
        try
        {
            participants = AnnouncerWorker.GetList();
        }
        catch (Exception)
        {
            Log.E("AnnouncerWindow", "Error getting participants from AnnouncerWorker.");
            participants = [];
        }
        participants.Sort((x1, x2) => x1.CompareTo(x2));
        DateTime cutoff = DateTime.Now.AddSeconds(-1 * Globals.AnnouncerWindow);
        // Remove all participant values where x.When is less than 0 (i.e. cutoff occurred after x.When)
        participants.RemoveAll((x) => (DateTime.Compare(cutoff, x.When) > 0));
        // Reverse all entries so the last person to cross the line is at the top.
        participants.Reverse();
        AnnouncerBox.ItemsSource = participants;
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
}