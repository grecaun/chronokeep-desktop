using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Chronokeep.Helpers;
using System;

namespace Chronokeep.UI.Parts;

public partial class WavePart : UserControl
{
    private int Wave { get; set; }
    private string WaveStr => $"{Wave}";
    private bool PlusWave { get; set; }

    private const string TimeFormat = "{0:D2}:{1:D2}:{2:D2}.{3:D3}";

    public WavePart(int num, long startSeconds, int startMilliseconds)
    {
        InitializeComponent();
        Wave = num;
        if (startSeconds >= 0)
        {
            PlusWave = true;
        }
        else
        {
            PlusWave = false;
            startSeconds *= -1;
        }
        if (startMilliseconds < 0)
        {
            startMilliseconds *= -1;
        }
        StartOffset.Text = string.Format(TimeFormat, startSeconds / 3600,
            (startSeconds % 3600) / 60, startSeconds % 60,
            startMilliseconds);
        WaveName.Text = WaveStr;
    }

    public void SetTime(long seconds, int milliseconds)
    {
        StartOffset.Text = string.Format(TimeFormat, seconds / 3600,
            (seconds % 3600) / 60, seconds % 60,
            milliseconds);
    }

    public int GetWave()
    {
        return Wave;
    }

    public (int, long, int) GetValues()
    {
        string[] firstParts = StartOffset.Text!.Replace('_', '0').Split(':');
        string[] secondParts = firstParts[2].Split('.');
        try
        {
            int hours = Convert.ToInt32(firstParts[0]),
                minutes = Convert.ToInt32(firstParts[1]),
                seconds = Convert.ToInt32(secondParts[0]),
                milliseconds = Convert.ToInt32(secondParts[1]);
            seconds = (hours * 3600) + (minutes * 60) + seconds;
            if (PlusWave) return (Wave, seconds, milliseconds);
            Log.D("UI.Timing.WaveWindow", "Negative wave, setting values to match.");
            seconds *= -1;
            milliseconds *= -1;
            return (Wave, seconds, milliseconds);
        }
        catch
        {
            Log.D("UI.Timing.WaveWindow", "Error evaluating values.");
        }
        return (Wave, 0, 0);
    }

    private void SelectAll(object sender, FocusChangedEventArgs e)
    {
        TextBox src = (TextBox)e.Source!;
        src.SelectAll();
    }

    private void SwapWaveType_Click(object sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.WaveWindow", "Plus/Minus sign clicked. WaveType is: " + (PlusWave ? "+" : "-"));
        PlusWave = !PlusWave;
        if (PlusWave)
        {
            PlusIcon.IsVisible = true;
            MinusIcon.IsVisible = false;
        }
        else
        {
            PlusIcon.IsVisible = false;
            MinusIcon.IsVisible = true;
        }
    }
}