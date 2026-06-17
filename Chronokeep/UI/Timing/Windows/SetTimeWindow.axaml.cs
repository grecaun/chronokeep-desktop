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