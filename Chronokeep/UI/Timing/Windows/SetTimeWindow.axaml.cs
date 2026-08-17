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
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using System;

namespace Chronokeep.UI.Timing.Windows;

public partial class SetTimeWindow : ChronokeepWindow
{
    private readonly ITimingPage parent;
    private readonly TimingSystem timingSystem;

    public SetTimeWindow(ITimingPage parent, TimingSystem timingSystem)
    {
        InitializeComponent();
        ChronokeepInitialize();
        this.parent = parent;
        this.timingSystem = timingSystem;
    }

    public bool IsTimingSystem(TimingSystem iTimingSystem)
    {
        return timingSystem.Equals(iTimingSystem);
    }

    public void UpdateTime()
    {
        TimeLabel.Text = $"Reader time is {timingSystem.SystemTime}";
        CurrentTimeLabel.Text = $"System time is {DateTime.Now:dd MMM yyyy HH:mm:ss}";
        TimeLabel.IsVisible = true;
        CurrentTimeLabel.IsVisible = true;
    }

    private void Window_Closing(object sender, WindowClosingEventArgs e)
    {
        parent.CloseTimeWindow();
    }

    private void Check_Click(object sender, RoutedEventArgs e)
    {
        timingSystem.SystemInterface!.GetTime();
    }

    private void Set_Click(object sender, RoutedEventArgs e)
    {
        if (!DateTime.TryParse($"{SpecificDateBox.Text!.Replace('_', '0')} {SpecificTimeBox.Text!.Replace('_', '0')}", out DateTime alternateDate))
        {
            alternateDate = DateTime.Now;
        }
        if (SetAllCheckBox.IsChecked == true)
        {
            parent.SetAllTimingSystemsToTime(alternateDate, NowTimeRadioButton.IsChecked == true);
        }
        else if (NowTimeRadioButton.IsChecked == true)
        {
            timingSystem.SystemInterface!.SetTime(DateTime.Now);
        }
        else
        {
            timingSystem.SystemInterface!.SetTime(alternateDate);
        }
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}
