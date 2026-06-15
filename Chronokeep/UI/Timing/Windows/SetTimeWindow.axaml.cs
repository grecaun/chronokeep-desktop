using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using System;

namespace Chronokeep.UI.Timing.Windows;

public partial class SetTimeWindow : Window
{
    private readonly ITimingPage parent;
    private readonly TimingSystem timingSystem;

    public SetTimeWindow(ITimingPage parent, TimingSystem timingSystem)
    {
        InitializeComponent();
        this.parent = parent;
        this.timingSystem = timingSystem;
    }

    public bool IsTimingSystem(TimingSystem timingSystem)
    {
        return this.timingSystem.Equals(timingSystem);
    }

    public void UpdateTime()
    {
        TimeLabel.Text = string.Format("Reader time is {0}", timingSystem.SystemTime);
        CurrentTimeLabel.Text = string.Format("System time is {0}", DateTime.Now.ToString("dd MMM yyyy HH:mm:ss"));
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
        if (DateTime.TryParse(string.Format("{0} {1}", SpecificDateBox.Text!.Replace('_', '0'), SpecificTimeBox.Text!.Replace('_', '0')), out DateTime alternateDate) == false)
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

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}