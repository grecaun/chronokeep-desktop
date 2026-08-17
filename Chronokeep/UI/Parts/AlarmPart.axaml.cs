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
using Chronokeep.Helpers;
using Chronokeep.Objects;
using Chronokeep.UI.MainPages.Timing;

namespace Chronokeep.UI.Parts;

public partial class AlarmPart : UserControl
{
    private readonly AlarmsPage page;
    private readonly Alarm theAlarm;

    public AlarmPart(AlarmsPage page, Alarm alarm)
    {
        InitializeComponent();
        this.page = page;
        theAlarm = alarm;
        BibBox.Text = theAlarm.Bib;
        ChipBox.Text = theAlarm.Chip;
        EnabledBox.IsChecked = theAlarm.Enabled;
        AlarmSoundBox.SelectedIndex = theAlarm.AlarmSound;
    }

    public Alarm GetUpdatedAlarm()
    {
        theAlarm.Bib = BibBox.Text!.Trim();
        theAlarm.Chip = theAlarm.Bib.Length > 0 ? "" : ChipBox.Text!;
        theAlarm.Enabled = EnabledBox.IsChecked == true;
        theAlarm.AlarmSound = AlarmSoundBox.SelectedIndex;
        return theAlarm;
    }

    private void Remove_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.AlarmsPage", "Removing alarm.");
        page.RemoveAlarm(this);
    }
}
