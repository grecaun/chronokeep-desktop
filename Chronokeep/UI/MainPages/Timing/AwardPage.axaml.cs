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
using Avalonia.Platform.Storage;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.IO.HtmlTemplates.Printables;
using Chronokeep.Objects;
using Chronokeep.UI.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace Chronokeep.UI.MainPages.Timing;

public partial class AwardPage : UserControl, ISubPage
{
    private readonly IdbInterface database;
    private readonly TimingPage parent;
    private readonly Event? theEvent;

    private readonly ObservableCollection<AgeGroup> customAgeGroups = [];

    [GeneratedRegex("[^0-9]")]
    private static partial Regex AllowedChars();

    public AwardPage(TimingPage parent, IdbInterface database)
    {
        InitializeComponent();
        this.parent = parent;
        this.database = database;
        theEvent = database.GetCurrentEvent();
        if (theEvent == null || theEvent.Identifier < 0)
        {
            Log.E("UI.Timing.AwardPage", "Something went wrong and no proper event was returned.");
            return;
        }
        CustomGroupsListView.ItemsSource = customAgeGroups;
        List<Distance> distances = database.GetDistances(theEvent.Identifier);
        distances.Sort((x1, x2) => string.Compare(x1.Name, x2.Name, StringComparison.Ordinal));
        foreach (Distance d in distances.Where(d => d.LinkedDistance <= 0))
        {
            DistancesBox.Items.Add(new ListBoxItem()
            {
                Content = d.Name
            });
        }
        parent.SetReaders([], false);
        UpdateView();
    }

    private AwardOptions GetOptions()
    {
        return new AwardOptions
        {
            PrintOverall = OverallYes.IsChecked == true,
            PrintAgeGroups = AgYes.IsChecked == true,
            PrintCustom = CustomYes.IsChecked == true,
            NumOverall = OverallNumberParticipants.Text!.Length == 0 ? 3 : Convert.ToInt32(OverallNumberParticipants.Text),
            NumAgeGroups = AgNumberParticipants.Text!.Length == 0 ? 3 : Convert.ToInt32(AgNumberParticipants.Text),
            NumCustom = CustomNumberParticipants.Text!.Length == 0 ? 3 : Convert.ToInt32(CustomNumberParticipants.Text),
            ExcludeOverallAg = OverallExcludeAg.IsChecked == true,
            ExcludeOverallCustom = OverallExcludeCustom.IsChecked == true,
            ExcludeAgeGroupsCustom = AgExcludeCustom.IsChecked == true
        };
    }

    private void IsNumber(object? sender, Avalonia.Input.TextInputEventArgs e)
    {
        e.Handled = AllowedChars().IsMatch(e.Text!);
    }

    private string GetPrintableAwards(List<string> distances, AwardOptions options)
    {
        (Dictionary<string, List<string>> distanceGroups, Dictionary<string, Dictionary<string, List<TimeResult>>> resultsDictionary) =
            PrintableResults.Awards(database.GetTimingResults(theEvent!.Identifier), theEvent, distances, options, customAgeGroups);
        AwardsPrintable output = new(theEvent, distanceGroups, resultsDictionary);
        return output.TransformText();
    }

    public void CancelableUpdateView(CancellationToken token) { }

    public void Search(CancellationToken token) { }

    public void UpdateView()
    {
        customAgeGroups.Clear();
        foreach (AgeGroup age in database.GetAgeGroups(theEvent!.Identifier, Constants.Timing.AGEGROUPS_CUSTOM_DISTANCEID))
        {
            customAgeGroups.Add(age);
        }
    }

    public void Closing() { }

    public void KeyboardCtrlA() { }

    public void KeyboardCtrlS() { }

    public void KeyboardCtrlZ() { }

    private void AddCustom_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.AwardPage", "Add custom group clicked.");
        try
        {
            int start = Convert.ToInt32(StartCustom.Text);
            int end = Convert.ToInt32(EndCustom.Text);
            string custom = CustomNameBox.Text!;
            if (start > -1 || end < 101)
            {
                database.AddAgeGroup(
                    new AgeGroup(theEvent!.Identifier,
                        Constants.Timing.AGEGROUPS_CUSTOM_DISTANCEID,
                        start,
                        end,
                        custom
                        ));
                UpdateView();
                StartCustom.Text = "";
                EndCustom.Text = "";
                CustomNameBox.Text = "";
                StartCustom.Focus();
            }
            else
            {
                DialogBox.AsyncShow("Ages are not in the range of 0 to 100.");
            }
        }
        catch (Exception ex)
        {
            Log.E("UI.Timing.AwardPage", ex.Message);
            DialogBox.AsyncShow("Start or end age not specified.");
        }
    }

    private void DeleteCustom_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.AwardPage", "Deleting some entries... maybe.");
        List<AgeGroup> items = [];
        IList selected = CustomGroupsListView.SelectedItems;
        if (selected.Count < 1)
        {
            return;
        }
        items.AddRange(selected.Cast<AgeGroup>());
        database.RemoveAgeGroups(items);
        UpdateView();
    }

    private async void SaveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.Timing.AwardPage", "Save clicked.");
            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            IStorageFolder? startingFolder;
            try
            {
                startingFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(database.GetAppSetting(Constants.Settings.DEFAULT_EXPORT_DIR)!.Value));
            }
            catch
            {
                startingFolder = null;
            }
            IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                FileTypeChoices = [Utils.PdfType],
                SuggestedFileName = $"{theEvent!.YearCode}-{theEvent.Name}-Awards.pdf".Replace(' ', '-'),
                SuggestedStartLocation = startingFolder,
            });
            AwardOptions options = GetOptions();
            List<string> divsToPrint = [];
            if (DistancesBox.SelectedItems != null)
            {
                foreach (object? divItem in DistancesBox.SelectedItems)
                {
                    if (divItem is not ListBoxItem div || div.Content == null) continue;
                    if (div.Content.Equals("All"))
                    {
                        divsToPrint.Clear();
                        break;
                    }
                    divsToPrint.Add(div.Content.ToString()!);
                }
            }
            if (options is { PrintCustom: false, PrintAgeGroups: false, PrintOverall: false })
            {
                DialogBox.AsyncShow("No awards group selected to print/save.");
                return;
            }

            if (file is null) return;
            try
            {
                string htmlString = GetPrintableAwards(divsToPrint, options);
                string weasyName;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // weasyprint.exe ships in the program's folder, which isn't always the current folder.
                    weasyName = Path.Combine(AppContext.BaseDirectory, "weasyprint.exe");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    weasyName = "weasyprint";
                    using Process testWeasy = new();
                    testWeasy.StartInfo.FileName = "which";
                    testWeasy.StartInfo.Arguments = weasyName;
                    testWeasy.Start();
                    await testWeasy.WaitForExitAsync();
                    int exitVal = testWeasy.ExitCode;
                    testWeasy.Close();
                    if (exitVal != 0)
                    {
                        DialogBox.AsyncShow("This function requires Weasyprint to function. Please install it and try again.",
                            "https://doc.courtbouillon.org/weasyprint/stable/first_steps.html");
                        return;
                    }
                }
                else
                {
                    DialogBox.AsyncShow("Operating System detected does not support this function currently.");
                    return;
                }
                // Write HTML to a temp file.
                string tmpFile = Path.Combine(Path.GetTempPath(), "print_temp.html");
                await using StreamWriter streamWriter = new(File.Open(tmpFile, FileMode.Create));
                await streamWriter.WriteAsync(htmlString);
                streamWriter.Close();
                // Delete old file if it exists.
                // Only the file name gets dashes in place of spaces, the folder it's saved in has to stay the same.
                string chosenPath = file.TryGetLocalPath()!;
                string filePath = Path.Combine(Path.GetDirectoryName(chosenPath) ?? "", Path.GetFileName(chosenPath).Replace(' ', '-'));
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                // Use weasyprint to convert our temp html file to a saved PDF file.
                // ArgumentList quotes each path, so paths with spaces work.
                using Process createPdf = new();
                createPdf.StartInfo.FileName = weasyName;
                createPdf.StartInfo.ArgumentList.Add(tmpFile);
                createPdf.StartInfo.ArgumentList.Add(filePath);
                createPdf.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                createPdf.Start();
                // wait for it to exit then kill it, even if the wait timed out
                await createPdf.WaitForExitAsync();
                createPdf.Close();
                // Let every user on the computer open or replace the PDF.
                ProgramFolders.ShareFile(filePath);
                // delete old file
                File.Delete(tmpFile);
                // DialogBox.Show("File saved.");
                using Process openPdf = new();
                openPdf.StartInfo.FileName = filePath;
                openPdf.StartInfo.UseShellExecute = true;
                openPdf.Start();
                await openPdf.WaitForExitAsync();
                openPdf.Close();
            }
            catch
            {
                DialogBox.AsyncShow("Unable to save file.");
            }
        }
        catch (Exception)
        {
            Log.D("UI.Timing.AwardPage", "Error saving.");
        }
    }

    private void DoneButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.AwardPage", "Done clicked.");
        parent.LoadMainDisplay();
    }
}
