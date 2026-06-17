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
using System.Linq;
using System.Text.Json;

namespace Chronokeep.UI.Util;

public partial class ChangeLogWindow : ChronokeepWindow
{
    private readonly IWindowCallback window;
    private readonly IdbInterface database;

    private ChangeLogWindow(IWindowCallback window, IdbInterface database)
    {
        InitializeComponent();
        ChronokeepInitialize();
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
        changelogEntries.AddRange(changelogFiles.Select(File.ReadAllText).Select(jsonData => JsonSerializer.Deserialize<Entry>(jsonData)!));
        changelogEntries.Sort();
        changelogEntries[0].IsExpanded = true;
        changelogEntries = changelogEntries[..5];
        LogList.ItemsSource = changelogEntries;
        AppSetting? autoChangelog = database.GetAppSetting(Constants.Settings.AUTO_SHOW_CHANGELOG);
        AutoChangelogToggleSwitch.IsChecked = autoChangelog is { Value: Constants.Settings.SETTING_TRUE };
    }

    public static ChangeLogWindow NewWindow(IWindowCallback window, IdbInterface database)
    {
        return new ChangeLogWindow(window, database);
    }

    private void Window_Closing(object sender, WindowClosingEventArgs e)
    {
        database.SetAppSetting(Constants.Settings.AUTO_SHOW_CHANGELOG, AutoChangelogToggleSwitch.IsChecked == true ? Constants.Settings.SETTING_TRUE : Constants.Settings.SETTING_FALSE);
        window.WindowFinalize();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.Notifications.ChangelogWindow", "Done button clicked.");
        Close();
    }

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}