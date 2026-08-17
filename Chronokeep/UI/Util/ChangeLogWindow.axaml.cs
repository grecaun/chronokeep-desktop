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
            DialogBox.AsyncShow("Unable to find changelog folder.");
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
        if (!App.IsWindows)
        {
            MainGrid.RowDefinitions =
            [
                new RowDefinition(new GridLength(10)),
                new RowDefinition(new GridLength(1, GridUnitType.Star)),
                new RowDefinition(new GridLength(55))
            ];
        }
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
