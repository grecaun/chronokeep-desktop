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
using Chronokeep.UI.Parts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Chronokeep.UI.Timing.Windows;

public partial class WaveWindow : ChronokeepWindow
{
    private readonly IMainWindow window;
    private readonly IdbInterface database;
    private readonly Event? theEvent;
    private readonly Dictionary<int, Distance> distanceDictionary = [];
    private readonly Dictionary<int, (long seconds, int milliseconds)> waveTimes = [];
    private readonly HashSet<int> waves = [];

    public WaveWindow(IMainWindow window, IdbInterface database)
    {
        InitializeComponent();
        ChronokeepInitialize();
        this.window = window;
        this.database = database;
        theEvent = database.GetCurrentEvent();
        if (theEvent == null || theEvent.Identifier == -1) return;
        foreach (Distance div in database.GetDistances(theEvent.Identifier))
        {
            distanceDictionary[div.Identifier] = div;
            waves.Add(div.Wave);
            waveTimes[div.Wave] = (div.StartOffsetSeconds, div.StartOffsetMilliseconds);
        }
        List<int> sortedWaves = [.. waves];
        sortedWaves.Sort();
        foreach (int waveNum in sortedWaves)
        {
            long seconds = waveTimes[waveNum].seconds;
            int milliseconds = waveTimes[waveNum].milliseconds;
            Log.D("UI.Timing.WaveWindow", $"Seconds {seconds} - Milliseconds {milliseconds}");
            WaveList.Items.Add(new WavePart(waveNum, waveTimes[waveNum].seconds, waveTimes[waveNum].milliseconds));
        }
        NetTimeButton.IsChecked = true;
        if (!App.IsWindows)
        {
            MainGrid.RowDefinitions =
            [
                new RowDefinition(new GridLength(15)),
                new RowDefinition(new GridLength(1, GridUnitType.Star)),
                new RowDefinition(new GridLength(1, GridUnitType.Auto)),
                new RowDefinition(new GridLength(1, GridUnitType.Auto))
            ];
        }
    }

    private void Window_Closing(object sender, WindowClosingEventArgs e)
    {
        window.WindowFinalize();
    }

    private void TimeOfDayButton_Checked(object sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.WaveWindow", "Time of day selected.");
        if (TimeOfDayButton.IsChecked == true)
        {
            foreach (WavePart? wave in WaveList.Items.Cast<WavePart?>())
            {
                int waveId = wave!.GetWave();
                // Work in whole milliseconds so offsets like -0.5 seconds carry into the seconds correctly.
                long timeOfDay = (theEvent!.StartSeconds * 1000) + theEvent.StartMilliseconds
                    + (waveTimes[waveId].seconds * 1000) + waveTimes[waveId].milliseconds;
                wave.SetTime(timeOfDay / 1000, (int)(timeOfDay % 1000));
            }
        }
        else
        {
            foreach (WavePart? wave in WaveList.Items.Cast<WavePart?>())
            {
                int waveId = wave!.GetWave();
                wave.SetTime(waveTimes[waveId].seconds, waveTimes[waveId].milliseconds);
            }
        }
    }

    private void SetButton_Click(object sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.WaveWindow", "Aye aye! Updating!");
        foreach (WavePart? wave in WaveList.Items.Cast<WavePart?>())
        {
            (int waveNo, long seconds, int milliseconds) = wave!.GetValues();
            if (TimeOfDayButton.IsChecked == true)
            {
                // A time of day is always positive; the offset is it minus the race start, which can be
                // negative. Working in whole milliseconds keeps seconds and milliseconds the same sign.
                long offset = (Math.Abs(seconds) * 1000) + Math.Abs(milliseconds)
                    - ((theEvent!.StartSeconds * 1000) + theEvent.StartMilliseconds);
                seconds = offset / 1000;
                milliseconds = (int)(offset % 1000);
            }
            database.SetWaveTimes(theEvent!.Identifier, waveNo, seconds, milliseconds);
        }
        List<Distance> newDistances = database.GetDistances(theEvent!.Identifier);
        bool update = false;
        foreach (Distance div in newDistances)
        {
            if (!distanceDictionary.TryGetValue(div.Identifier, out Distance? oDist)
                || oDist.StartOffsetSeconds != div.StartOffsetSeconds
                || oDist.StartOffsetMilliseconds != div.StartOffsetMilliseconds)
            {
                update = true;
            }
        }
        if (update)
        {
            database.ResetTimingResultsEvent(theEvent.Identifier);
            window.UpdateTiming();
            window.NotifyTimingWorker();
        }
        Close();
    }

    private void DoneButton_Click(object sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.WaveWindow", "We don't really want to set the wave times.");
        Close();
    }

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}
