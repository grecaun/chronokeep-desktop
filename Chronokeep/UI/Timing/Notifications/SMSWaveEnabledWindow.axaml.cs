using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Database;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using System.Collections.Generic;
using System.Linq;

namespace Chronokeep.UI.Timing.Notifications;

public partial class SmsWaveEnabledWindow : ChronokeepWindow
{
    private readonly IMainWindow window;
    private readonly IDBInterface database;

    private readonly Dictionary<int, bool> initialValues = [];
    private readonly Dictionary<int, bool> updatedValues = [];
    private readonly Dictionary<int, List<Distance>> waveDistanceDictionary = [];

    public SmsWaveEnabledWindow(IMainWindow window, IDBInterface database)
    {
        InitializeComponent();
        MinHeight = 275;
        MinWidth = 300;
        Width = 300;
        Topmost = true;
        this.window = window;
        this.database = database;
        Event? theEvent1 = database.GetCurrentEvent();
        if (theEvent1 == null)
        {
            return;
        }
        foreach (Distance dist in database.GetDistances(theEvent1.Identifier))
        {
            initialValues[dist.Wave] = dist.SmsEnabled;
            if (!waveDistanceDictionary.TryGetValue(dist.Wave, out List<Distance>? oDistList))
            {
                oDistList = [];
                waveDistanceDictionary[dist.Wave] = oDistList;
            }
            oDistList.Add(dist);
        }
        List<int> sortedWaves = [.. initialValues.Keys];
        sortedWaves.Sort();
        List<WaveSms> waves = [];
        waves.AddRange(sortedWaves.Select(waveNum => new WaveSms { Wave = waveNum, SmsEnabled = initialValues[waveNum] }));
        WaveList.ItemsSource = waves;
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        window.WindowFinalize(this);
        if (updatedValues.Keys.Count > 0)
        {
            window.NotifyTimingWorker();
        }
    }

    private void Set_Click(object? sender, RoutedEventArgs e)
    {
        foreach (object? waveSms in WaveList.Items)
        {
            if (waveSms is not WaveSms wave) continue;
            if (initialValues[wave.Wave] != wave.SmsEnabled)
            {
                updatedValues[wave.Wave] = wave.SmsEnabled;
            }
        }
        foreach (int wave in updatedValues.Keys)
        {
            if (!waveDistanceDictionary.TryGetValue(wave, out List<Distance>? tDistList)) continue;
            foreach (Distance dist in tDistList)
            {
                dist.SmsEnabled = updatedValues[wave];
                database.UpdateDistance(dist);
            }
        }
        Close();
    }

    private void Done_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void Maximize()
    {
        WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
    }
}