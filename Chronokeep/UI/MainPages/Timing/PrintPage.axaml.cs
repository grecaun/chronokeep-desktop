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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia.Interactivity;

namespace Chronokeep.UI.MainPages.Timing;

public partial class PrintPage : UserControl, ISubPage
{
    private readonly TimingPage parent;
    private readonly IdbInterface database;
    private readonly Event? theEvent;

    public PrintPage(TimingPage parent, IdbInterface database)
    {
        InitializeComponent();
        this.parent = parent;
        this.database = database;
        theEvent = database.GetCurrentEvent();

        if (theEvent == null || theEvent.Identifier < 0)
        {
            return;
        }

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
    }

    public void UpdateView() { }

    public void CancelableUpdateView(CancellationToken token) { }

    public void Search(CancellationToken token) { }

    private string GetOverallPrintableDocument(List<string> distances)
    {
        (Dictionary<string, List<TimeResult>> distanceResults, Dictionary<string, List<TimeResult>> dnfResults) =
            PrintableResults.Overall(database.GetTimingResults(theEvent!.Identifier), theEvent, distances);
        ResultsPrintableOverall output = new(theEvent, distanceResults, dnfResults);
        return output.TransformText();
    }

    private string GetGenderPrintableDocument(List<string> distances)
    {
        (Dictionary<string, Dictionary<string, List<TimeResult>>> distanceResults, Dictionary<string, Dictionary<string, List<TimeResult>>> dnfResults) =
            PrintableResults.ByGender(database.GetTimingResults(theEvent!.Identifier), theEvent, distances);
        ResultsPrintableGender output = new(theEvent, distanceResults, dnfResults);
        return output.TransformText();
    }

    private string GetAgeGroupPrintableDocument(List<string> distances)
    {
        Dictionary<int, AgeGroup> ageGroups = PrintableResults.AgeGroups(database.GetAgeGroups(theEvent!.Identifier), theEvent);
        (Dictionary<string, Dictionary<(int, string), List<TimeResult>>> distanceResults, Dictionary<string, Dictionary<(int, string), List<TimeResult>>> dnfResults) =
            PrintableResults.ByAgeGroup(database.GetTimingResults(theEvent.Identifier), theEvent, distances);
        ResultsPrintableAgeGroup output = new(theEvent, distanceResults, dnfResults, ageGroups);
        return output.TransformText();
    }

    public void Closing() { }

    public void KeyboardCtrlA() { }

    public void KeyboardCtrlS() { }

    public void KeyboardCtrlZ() { }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.Timing.PrintPage", "All times - save clicked.");
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
                SuggestedFileName = $"{theEvent!.YearCode}-{theEvent.Name}-Results.pdf".Replace(' ', '-'),
                SuggestedStartLocation = startingFolder,
            });
            List<string> divsToPrint = [];
            if (DistancesBox.SelectedItems != null)
            {
                foreach (object? divItem in DistancesBox.SelectedItems)
                {
                    if (divItem is not ListBoxItem { Content: not null } div) continue;
                    if (div.Content.Equals("All"))
                    {
                        divsToPrint.Clear();
                        break;
                    }
                    divsToPrint.Add(div.Content.ToString()!);
                }
            }

            if (file is null) return;
            string htmlString;
            switch (PlacementType.SelectedIndex)
            {
                case 0:
                    htmlString = GetOverallPrintableDocument(divsToPrint);
                    break;
                case 1:
                    htmlString = GetGenderPrintableDocument(divsToPrint);
                    break;
                case 2:
                    htmlString = GetAgeGroupPrintableDocument(divsToPrint);
                    break;
                default:
                    DialogBox.AsyncShow("Please select a type.");
                    return;
            }
            try
            {
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
                    testWeasy.StartInfo.UseShellExecute = true;
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
                DialogBox.AsyncShow($"Unable to save file.");
            }
        }
        catch (Exception)
        {
            Log.D("UI.Timing.PrintPage", "Error saving.");
        }
    }

    private void Done_Click(object? sender, RoutedEventArgs e)
    {
        parent.LoadMainDisplay();
    }
}
