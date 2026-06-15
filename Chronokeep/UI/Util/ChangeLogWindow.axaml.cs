using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.Objects.Changelog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Chronokeep.UI.Util;

public partial class ChangeLogWindow : Window
{
    private readonly IWindowCallback window;
    private readonly IDBInterface database;

    private ChangeLogWindow(IWindowCallback window, IDBInterface database)
    {
        InitializeComponent();
        this.window = window;
        this.database = database;

        string changelogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "changelog");
        if (!Path.Exists(changelogPath))
        {
            Close();
            DialogBox.Show("Unable to find changelog folder.");
            return;
        }
        string[] changelogFiles = Directory.GetFiles(changelogPath);
        if (changelogFiles.Length < 1)
        {
            Close();
            return;
        }
        List<Entry> changelogEntries = [];
        foreach (string file in changelogFiles)
        {
            string jsonData = File.ReadAllText(file);
            Entry entry = JsonSerializer.Deserialize<Entry>(jsonData)!;
            changelogEntries.Add(entry);
        }
        changelogEntries.Sort();
        changelogEntries[0].IsExpanded = true;
        changelogEntries = changelogEntries[..5];
        LogList.ItemsSource = changelogEntries;
        AppSetting autoChangelog = database.GetAppSetting(Constants.Settings.AUTO_SHOW_CHANGELOG)!;
        autoChangelogToggleSwitch.IsChecked = autoChangelog != null && autoChangelog.Value == Constants.Settings.SETTING_TRUE;
    }

    public static ChangeLogWindow NewWindow(IWindowCallback window, IDBInterface database)
    {
        return new(window, database);
    }

    private void Window_Closing(object sender, WindowClosingEventArgs e)
    {
        database.SetAppSetting(Constants.Settings.AUTO_SHOW_CHANGELOG, autoChangelogToggleSwitch.IsChecked == true ? Constants.Settings.SETTING_TRUE : Constants.Settings.SETTING_FALSE);
        window.WindowFinalize(this);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.Notifications.ChangelogWindow", "Done button clicked.");
        Close();
    }

    private void OnMinimize(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximize(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        MaximizeIcon?.IsVisible = WindowState == WindowState.Normal;
        UnMaximizeIcon?.IsVisible = WindowState == WindowState.Maximized;
    }
}