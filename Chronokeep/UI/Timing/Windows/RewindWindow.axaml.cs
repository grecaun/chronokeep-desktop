using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.Timing.Interfaces;
using Chronokeep.UI.Util;
using System;
using System.ComponentModel;
using Chronokeep.Constants;

namespace Chronokeep.UI.Timing.Windows;

public partial class RewindWindow : ChronokeepWindow
{
    private readonly ITimingPage parent;
    private readonly TimingSystem system;

    public RewindWindow(TimingSystem system, ITimingPage parent)
    {
        InitializeComponent();
        ChronokeepInitialize();
        SizeToContent = SizeToContent.Height;
        this.system = system;
        this.parent = parent;
        string dateStr = DateTime.Now.ToString("MM/dd/yyyy");
        FromDate.Text = dateStr;
        ToDate.Text = dateStr;
        FromTime.Text = "00:00:00";
        ToTime.Text = "23:59:59";
        if (system.Type != Readers.SYSTEM_IPICO && system.Type != Readers.SYSTEM_IPICO_LITE) return;
        Reader1.IsVisible = true;
        Reader2.IsVisible = true;
    }

    public bool IsTimingSystem(TimingSystem timingSystem)
    {
        return system.Equals(timingSystem);
    }

    private void Window_Closing(object sender, WindowClosingEventArgs e)
    {
        parent.CloseRewindWindow();
    }

    private void SetYesterday_Click(object sender, RoutedEventArgs e)
    {
        string dateStr = DateTime.Now.AddDays(-1).ToString("MM/dd/yyyy");
        FromDate.Text = dateStr;
        ToDate.Text = dateStr;
        FromTime.Text = "00:00:00";
        ToTime.Text = "23:59:59";
    }

    private void SetToday_Click(object sender, RoutedEventArgs e)
    {
        string dateStr = DateTime.Now.ToString("MM/dd/yyyy");
        FromDate.Text = dateStr;
        ToDate.Text = dateStr;
        FromTime.Text = "00:00:00";
        ToTime.Text = "23:59:59";
    }

    private void SetTomorrow_Click(object sender, RoutedEventArgs e)
    {
        string dateStr = DateTime.Now.AddDays(1).ToString("MM/dd/yyyy");
        FromDate.Text = dateStr;
        ToDate.Text = dateStr;
        FromTime.Text = "00:00:00";
        ToTime.Text = "23:59:59";
    }

    private void Rewind_Click(object sender, RoutedEventArgs e)
    {
        if (!DateTime.TryParse($"{FromDate.Text!} {FromTime.Text!.Replace('_', '0')}", out DateTime from))
        {
            from = DateTime.Now;
        }
        if (!DateTime.TryParse($"{ToDate.Text!} {ToTime.Text!.Replace('_', '0')}", out DateTime to))
        {
            to = DateTime.Now;
        }
        if (system.Type is Readers.SYSTEM_IPICO or Readers.SYSTEM_IPICO_LITE)
        {
            DialogBox.Show(
                "This process can take up to 3 minutes to complete. There is no guarantee that other processes will work properly while this is occuring. Are you sure you wish to proceed?",
                "Yes",
                "No",
                () =>
                {
                    BackgroundWorker worker = new();
                    worker.DoWork += (_, _) =>
                    {
                        system.SystemInterface!.Rewind(from, to, Reader1.IsChecked == true ? 1 : 2);
                        ((IpicoInterface)system.SystemInterface).GetRewind();
                    };
                    worker.RunWorkerCompleted += (_, _) =>
                    {
                        BusyIndicator.IsVisible = false;
                    };
                    BusyIndicator.IsVisible = true;
                    worker.RunWorkerAsync();
                });
        }
        else
        {
            system.SystemInterface!.Rewind(from, to);
        }
        Close();
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