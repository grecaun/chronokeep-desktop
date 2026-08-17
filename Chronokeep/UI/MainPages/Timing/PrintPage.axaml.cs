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
        // Get all results for the race
        List<TimeResult> results = database.GetTimingResults(theEvent!.Identifier);
        // Remove all unknown participants
        results.RemoveAll(x => x.Bib == Constants.Timing.CHIPREAD_DUMMYBIB);
        results.RemoveAll(x => x.DistanceName.Length < 1);
        // REMOVE SOME DEPENDING ON WHO THEY WANT
        if (distances.Count > 0)
        {
            results.RemoveAll(x => !distances.Contains(x.DistanceName));
        }
        // remove all segments that are not finish segments
        results.RemoveAll(x => x.SegmentId != Constants.Timing.SEGMENT_FINISH);
        // if we're a time based event, exclude all but the last result
        if (theEvent.EventType == Constants.Timing.EVENT_TYPE_TIME)
        {
            Dictionary<string, TimeResult> lastResult = [];
            foreach (TimeResult individual in results)
            {
                if (lastResult.TryGetValue(individual.ParticipantName, out TimeResult? oLResult))
                {
                    if (oLResult.Occurrence < individual.Occurrence)
                    {
                        lastResult[individual.ParticipantName] = individual;
                    }
                }
                else
                {
                    lastResult[individual.ParticipantName] = individual;
                }
            }
            results = [.. lastResult.Values];
        }
        Dictionary<string, List<TimeResult>> distanceResults = [];
        Dictionary<string, List<TimeResult>> dnfResultDictionary = [];
        foreach (TimeResult result in results)
        {
            if (!distanceResults.TryGetValue(result.DistanceName, out List<TimeResult>? oDistResultList))
            {
                oDistResultList = [];
                distanceResults[result.DistanceName] = oDistResultList;
            }
            if (result.Status == Constants.Timing.TIMERESULT_STATUS_DNF)
            {
                if (!dnfResultDictionary.TryGetValue(result.DistanceName, out List<TimeResult>? oDnfResList))
                {
                    oDnfResList = [];
                    dnfResultDictionary[result.DistanceName] = oDnfResList;
                }

                oDnfResList.Add(result);
            }
            else
            {
                oDistResultList.Add(result);
            }
        }
        foreach (string divName in distanceResults.Keys.OrderBy(i => i))
        {
            // get rid of all non-finish segments
            distanceResults[divName].RemoveAll(x => x.SegmentId != Constants.Timing.SEGMENT_FINISH);
            // sort by distance place
            distanceResults[divName].Sort(TimeResult.CompareByDistancePlace);
        }
        ResultsPrintableOverall output = new(theEvent, distanceResults, dnfResultDictionary);
        return output.TransformText();
    }

    private string GetGenderPrintableDocument(List<string> distances)
    {
        // Get all finish results for the race
        List<TimeResult> results = database.GetTimingResults(theEvent!.Identifier);
        // Remove all unknown participants
        results.RemoveAll(x => x.Bib == Constants.Timing.CHIPREAD_DUMMYBIB);
        results.RemoveAll(x => x.DistanceName.Length < 1);
        // REMOVE SOME DEPENDING ON WHO THEY WANT
        if (distances.Count > 0)
        {
            results.RemoveAll(x => !distances.Contains(x.DistanceName));
        }
        // remove all segments that are not finish segments
        results.RemoveAll(x => x.SegmentId != Constants.Timing.SEGMENT_FINISH);
        // remove all results without a gender specified
        results.RemoveAll(x => x.Gender == "Not Specified");
        // if we're a time based event, exclude all but the last result
        if (theEvent.EventType == Constants.Timing.EVENT_TYPE_TIME)
        {
            Dictionary<string, TimeResult> lastResult = [];
            foreach (TimeResult individual in results)
            {
                if (lastResult.TryGetValue(individual.ParticipantName, out TimeResult? oLResult))
                {
                    if (oLResult.Occurrence < individual.Occurrence)
                    {
                        lastResult[individual.ParticipantName] = individual;
                    }
                }
                else
                {
                    lastResult[individual.ParticipantName] = individual;
                }
            }
            results = [.. lastResult.Values];
        }
        // separate each grouping by distance, then by gender
        Dictionary<string, Dictionary<string, List<TimeResult>>> distanceResults = [];
        Dictionary<string, Dictionary<string, List<TimeResult>>> dnfResultsDictionary = [];
        foreach (TimeResult result in results)
        {
            if (!distanceResults.TryGetValue(result.DistanceName, out Dictionary<string, List<TimeResult>>? oDistResDict))
            {
                oDistResDict = [];
                distanceResults[result.DistanceName] = oDistResDict;
            }
            if (!oDistResDict.TryGetValue(result.Gender, out List<TimeResult>? oDistGenderResList))
            {
                oDistGenderResList = [];
                oDistResDict[result.Gender] = oDistGenderResList;
            }
            if (result.Status == Constants.Timing.TIMERESULT_STATUS_DNF)
            {
                if (!dnfResultsDictionary.TryGetValue(result.DistanceName, out Dictionary<string, List<TimeResult>>? oDnfResDict))
                {
                    oDnfResDict = [];
                    dnfResultsDictionary[result.DistanceName] = oDnfResDict;
                }
                if (!oDnfResDict.TryGetValue(result.Gender, out List<TimeResult>? oDnfGndResList))
                {
                    oDnfGndResList = [];
                    oDnfResDict[result.Gender] = oDnfGndResList;
                }

                oDnfGndResList.Add(result);
            }
            else
            {
                oDistGenderResList.Add(result);
            }
        }
        foreach (string divName in distanceResults.Keys.OrderBy(i => i))
        {
            foreach (string gender in distanceResults[divName].Keys)
            {
                // get rid of non-finish results
                distanceResults[divName][gender].RemoveAll(x => x.SegmentId != Constants.Timing.SEGMENT_FINISH);
                // sort results
                distanceResults[divName][gender].Sort(TimeResult.CompareByDistancePlace);
            }
        }
        ResultsPrintableGender output = new(theEvent, distanceResults, dnfResultsDictionary);
        return output.TransformText();
    }

    private string GetAgeGroupPrintableDocument(List<string> distances)
    {
        // Get all the age groups for the race
        Dictionary<int, AgeGroup> ageGroups = database.GetAgeGroups(theEvent!.Identifier).ToDictionary(x => x.GroupId, x => x);
        // Add an age group for our unknown age people/
        ageGroups[Constants.Timing.TIMERESULT_DUMMYAGEGROUP] = new AgeGroup(theEvent.Identifier, Constants.Timing.COMMON_AGEGROUPS_DISTANCEID, -1, 3000);
        // Get all finish results for the race
        List<TimeResult> results = database.GetTimingResults(theEvent.Identifier);
        // Remove all unknown participants
        results.RemoveAll(x => x.Bib == Constants.Timing.CHIPREAD_DUMMYBIB);
        results.RemoveAll(x => x.DistanceName.Length < 1);
        // REMOVE SOME DEPENDING ON WHO THEY WANT
        if (distances.Count > 0)
        {
            results.RemoveAll(x => !distances.Contains(x.DistanceName));
        }
        // remove all segments that are not finish segments
        results.RemoveAll(x => x.SegmentId != Constants.Timing.SEGMENT_FINISH);
        // remove all results without a gender specified
        results.RemoveAll(x => x.Gender == "Not Specified");
        // if we're a time based event, exclude all but the last result
        if (theEvent.EventType == Constants.Timing.EVENT_TYPE_TIME)
        {
            Dictionary<string, TimeResult> lastResult = [];
            foreach (TimeResult individual in results)
            {
                if (lastResult.TryGetValue(individual.ParticipantName, out TimeResult? oLastRes))
                {
                    if (oLastRes.Occurrence < individual.Occurrence)
                    {
                        lastResult[individual.ParticipantName] = individual;
                    }
                }
                else
                {
                    lastResult[individual.ParticipantName] = individual;
                }
            }
            results = [.. lastResult.Values];
        }
        Dictionary<string, Dictionary<(int, string), List<TimeResult>>> distanceResults = [];
        Dictionary<string, Dictionary<(int, string), List<TimeResult>>> dnfResultsDictionary = [];
        foreach (TimeResult result in results)
        {
            if (!distanceResults.TryGetValue(result.DistanceName, out Dictionary<(int, string), List<TimeResult>>? oDistResDict))
            {
                oDistResDict = [];
                distanceResults[result.DistanceName] = oDistResDict;
            }
            if (!oDistResDict.TryGetValue((result.AgeGroupId, result.Gender), out List<TimeResult>? oDistResList))
            {
                oDistResList = [];
                oDistResDict[(result.AgeGroupId, result.Gender)] = oDistResList;
            }
            if (result.Status == Constants.Timing.TIMERESULT_STATUS_DNF)
            {
                if (!dnfResultsDictionary.TryGetValue(result.DistanceName, out Dictionary<(int, string), List<TimeResult>>? oDnfResDict))
                {
                    oDnfResDict = [];
                    dnfResultsDictionary[result.DistanceName] = oDnfResDict;
                }
                if (!oDnfResDict.TryGetValue((result.AgeGroupId, result.Gender), out List<TimeResult>? oDnfResList))
                {
                    oDnfResList = [];
                    oDnfResDict[(result.AgeGroupId, result.Gender)] = oDnfResList;
                }
                oDnfResList.Add(result);
            }
            else
            {
                oDistResList.Add(result);
            }
        }
        foreach (string divName in distanceResults.Keys.OrderBy(i => i))
        {
            Dictionary<(int, string), List<TimeResult>> lDistResDict = distanceResults[divName];
            foreach ((int ag, string gender) in lDistResDict.Keys)
            {
                List<TimeResult> lDistResList = lDistResDict[(ag, gender)];
                // get rid of non-finish results
                lDistResList.RemoveAll(x => x.SegmentId != Constants.Timing.SEGMENT_FINISH);
                // sort results
                lDistResList.Sort(TimeResult.CompareByDistancePlace);
            }
        }
        ResultsPrintableAgeGroup output = new(theEvent, distanceResults, dnfResultsDictionary, ageGroups);
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
                    weasyName = Path.Combine(Directory.GetCurrentDirectory(), "weasyprint.exe");
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
                string filePath = file.TryGetLocalPath()!.Replace(' ', '-');
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                // Use weasyprint to convert our temp html file to a saved PDF file.
                using Process createPdf = new();
                createPdf.StartInfo.FileName = weasyName;
                createPdf.StartInfo.Arguments = $" {tmpFile} {filePath}";
                createPdf.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                createPdf.Start();
                // wait for it to exit then kill it, even if the wait timed out
                await createPdf.WaitForExitAsync();
                createPdf.Close();
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
