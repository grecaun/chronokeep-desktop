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
using Chronokeep.UI.Util;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Chronokeep.UI.MainPages;

public partial class AboutPage : UserControl, IMainPage
{
    private readonly IMainWindow mWindow;
    private readonly IdbInterface database;

    public AboutPage(IMainWindow mWindow, IdbInterface database)
    {
        InitializeComponent();
        this.mWindow = mWindow;
        string gitVersion;
        using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Chronokeep.version.txt")!)
        {
            using StreamReader reader = new(stream);
            gitVersion = reader.ReadToEnd();
        }
        Log.D("UI.MainPages.AboutPage", $"Version: {gitVersion}");
        string dirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.Settings.HELP_DIR);
        if (Directory.Exists(dirPath))
        {
            dirPath = Path.Combine(dirPath, "index.html");
            HelpDocsButton.Tag = dirPath;
        }
        VersionLabel.Text = gitVersion.Trim();
        HelpDocsButton.NavigateUri = new Uri(Path.Combine(AppContext.BaseDirectory, "help", "index.html"));
        this.database = database;

    }

    public void Closing() { }

    public void KeyboardCtrlA() { }

    public void KeyboardCtrlS() { }

    public void KeyboardCtrlZ() { }

    public void UpdateView() { }

    private void License_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.AboutPage", "License click.");
        LicenseWindow newWin = new();
        newWin.Show();
    }

    private void Update_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.AboutPage", "Version clicked, checking for new version.");
        Updates.Check.Do(mWindow, true);
    }

    private void Changelog_Click(object? sender, RoutedEventArgs e)
    {
        ChangeLogWindow clw = ChangeLogWindow.NewWindow(mWindow, database);
        clw.Show();
    }

    private async void OpenDataFolder_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            string dirPath = App.IsWindows ?
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), Constants.Settings.PROGRAM_DIR)
                : Path.Combine(Directory.GetCurrentDirectory(), "data/");
            if (!Directory.Exists(dirPath))
            {
                return;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer", dirPath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                using Process dbusShowItemsProcess = new();
                dbusShowItemsProcess.StartInfo = new ProcessStartInfo
                {
                    FileName = "dbus-send",
                    Arguments = $"--print-reply --dest=org.freedesktop.FileManager1 /org/freedesktop/FileManager1 org.freedesktop.FileManager1.ShowItems array:string:\"file://{dirPath}\" string:\"\"",
                    UseShellExecute = true
                };
                dbusShowItemsProcess.Start();
                await dbusShowItemsProcess.WaitForExitAsync();
                if (dbusShowItemsProcess.ExitCode != 0)
                {
                    Log.E("UI.MainPages.AboutPage", "Unable to open data directory.");
                }
            }
        }
        catch (Exception)
        {
            Log.E("UI.MainPages.AboutPage", "Error opening data directory.");
        }
    }

    private async void OpenLogsFolder_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            string dirPath = App.IsWindows ?
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), Constants.Settings.PROGRAM_DIR, "logs")
                : Path.Combine(Directory.GetCurrentDirectory(), "logs/");
            if (!Directory.Exists(dirPath))
            {
                return;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer", dirPath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                using Process dbusShowItemsProcess = new();
                dbusShowItemsProcess.StartInfo = new ProcessStartInfo
                {
                    FileName = "dbus-send",
                    Arguments = $"--print-reply --dest=org.freedesktop.FileManager1 /org/freedesktop/FileManager1 org.freedesktop.FileManager1.ShowItems array:string:\"file://{dirPath}\" string:\"\"",
                    UseShellExecute = true
                };
                dbusShowItemsProcess.Start();
                await dbusShowItemsProcess.WaitForExitAsync();
                if (dbusShowItemsProcess.ExitCode != 0)
                {
                    Log.E("UI.MainPages.AboutPage", "Unable to open data directory.");
                }
            }
        }
        catch (Exception)
        {
            Log.E("UI.MainPages.AboutPage", "Error opening data directory.");
        }
    }
}
