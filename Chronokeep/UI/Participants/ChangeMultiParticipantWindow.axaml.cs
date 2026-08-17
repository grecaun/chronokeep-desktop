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
using System;
using System.Collections.Generic;

namespace Chronokeep.UI.Participants;

public partial class ChangeMultiParticipantWindow : ChronokeepWindow
{
    private readonly IMainWindow window;
    private readonly IdbInterface database;
    private readonly List<Participant> toChange;
    private readonly Event? theEvent;

    public ChangeMultiParticipantWindow(IMainWindow window, IdbInterface database, List<Participant> toChange)
    {
        InitializeComponent();
        ChronokeepInitialize();
        this.window = window;
        this.database = database;
        this.toChange = toChange;
        theEvent = database.GetCurrentEvent();
        if (theEvent == null) return;
        foreach (Distance div in database.GetDistances(theEvent!.Identifier))
        {
            DistanceBox.Items.Add(new ComboBoxItem()
            {
                Content = div.Name,
                Tag = div.Identifier.ToString()
            });
        }
        DistanceBox.SelectedIndex = 0;
        DistanceBox.Focus();
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        window.WindowFinalize();
    }

    private void Change_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Participants.ChangeMultiParticipantWindow", "Change clicked.");
        int distanceId = Convert.ToInt32(((ComboBoxItem)DistanceBox.SelectedItem!).Tag!);
        foreach (Participant part in toChange)
        {
            part.EventSpecific.DistanceIdentifier = distanceId;
        }
        database.UpdateParticipants(toChange);
        database.ResetTimingResultsEvent(theEvent!.Identifier);
        window.NotifyTimingWorker();
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Participants.ChangeMultiParticipantWindow", "Cancel clicked.");
        Close();
    }

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}
