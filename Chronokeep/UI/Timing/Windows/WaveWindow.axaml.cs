using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.UI.Parts;
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
    }

    private void Window_Closing(object sender, WindowClosingEventArgs e)
    {
        window.WindowFinalize();
    }

    private void TimeofDayButton_Checked(object sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.WaveWindow", "Time of day selected.");
        if (TimeofDayButton.IsChecked == true)
        {
            foreach (WavePart? wave in WaveList.Items.Cast<WavePart?>())
            {
                int waveId = wave!.GetWave();
                wave.SetTime(waveTimes[waveId].seconds + theEvent!.StartSeconds, waveTimes[waveId].milliseconds + theEvent.StartMilliseconds);
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
            if (TimeofDayButton.IsChecked == true)
            {
                seconds -= theEvent!.StartSeconds;
                milliseconds -= theEvent.StartMilliseconds;
                if (milliseconds < 0)
                {
                    seconds -= 1;
                    milliseconds = 1000 - milliseconds;
                }
                if (seconds < 0)
                {
                    seconds = 0;
                    milliseconds = 0;
                }
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
}