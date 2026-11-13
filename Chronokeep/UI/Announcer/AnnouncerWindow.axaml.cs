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
using Avalonia.Interactivity;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.Timing.Announcer;
using System;
using System.Collections.Generic;

namespace Chronokeep.UI.Announcer;

public partial class AnnouncerWindow : ChronokeepWindow
{
    private readonly IMainWindow window;
    private readonly IdbInterface database;

    private readonly Event? theEvent;
    private Dictionary<string, Participant>? participantsByBib;
    private bool participantsReloaded;

    public AnnouncerWindow(IMainWindow window, IdbInterface database)
    {
        InitializeComponent();
        ChronokeepInitialize();
        this.window = window;
        this.database = database;
        theEvent = database.GetCurrentEvent();
        AnnouncerParticipant.TheEvent = theEvent;
        window.StartAnnouncerWorker();
        UpdateView();
    }

    private void Window_Closing(object sender, WindowClosingEventArgs e)
    {
        Log.D("UI.Announcer.AnnouncerWindow", "Announcer window is closing!");
        AnnouncerWorker.Shutdown();
        window.AnnouncerClosing();
    }

    public void UpdateTiming() => UpdateView();

    private void ShowUnknownSwitch_IsCheckedChanged(object? sender, RoutedEventArgs e) => UpdateView();

    /// <summary>Shows everyone read at the announcer location or crossing the finish line recently, newest first.</summary>
    public void UpdateView()
    {
        if (theEvent == null) { return; }
        List<AnnouncerParticipant> announced = [];
        List<TimeResult> results = [];
        try
        {
            announced = AnnouncerWorker.GetList();
            results = database.GetTimingResults(theEvent.Identifier);
        }
        catch (Exception e)
        {
            Log.E("UI.Announcer.AnnouncerWindow", $"Error getting announcer reads or results. {e}");
        }
        participantsReloaded = false;
        AnnouncerBox.ItemsSource = AnnouncerParticipant.Merge(announced, results, FindParticipant,
            DateTime.Now.AddSeconds(-1 * Globals.AnnouncerWindow), ShowUnknownSwitch.IsChecked == true);
    }

    private Participant? FindParticipant(string bib)
    {
        if (participantsByBib == null || (!participantsByBib.ContainsKey(bib) && !participantsReloaded))
        {
            // Someone registered after the window opened; reload at most once per refresh.
            participantsReloaded = true;
            participantsByBib = [];
            foreach (Participant part in database.GetParticipants(theEvent!.Identifier))
            {
                participantsByBib.TryAdd(part.Bib, part);
            }
        }
        return participantsByBib.GetValueOrDefault(bib);
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
