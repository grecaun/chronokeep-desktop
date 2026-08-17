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
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using System.Collections.Generic;

namespace Chronokeep.UI.Participants;

public partial class ParticipantConflicts : ChronokeepWindow
{
    private readonly IMainWindow window;

    private ParticipantConflicts(IMainWindow window, List<Participant> participants)
    {
        InitializeComponent();
        ChronokeepInitialize();
        this.window = window;

        ParticipantsList.ItemsSource = participants;
        CanResize = true;
    }

    public static ParticipantConflicts NewWindow(IMainWindow window, List<Participant> participants)
    {
        return new ParticipantConflicts(window, participants);
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        window.WindowFinalize();
    }

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}
