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
using Avalonia.Platform.Storage;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.IO;
using Chronokeep.Interfaces.UI;
using Chronokeep.IO;
using Chronokeep.Objects;
using Chronokeep.UI.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Chronokeep.UI.Export;

public partial class ExportResults : ChronokeepWindow
{
    private readonly IMainWindow window;
    private readonly IdbInterface database;
    private readonly Event? theEvent;

    private readonly bool noOpen;

    private readonly List<string> commonHeaders =
    [
        "Place", "Age Group Place", "Gender Place",
            "Bib", "Distance", "Status", "First", "Last", "Birthday",
            "Age", "Gender", "Start", "Street", "Apartment",
            "City", "State", "Zip", "Country", "Mobile", "Email", "Parent", "Comments",
            "Other", "Owes", "Emergency Contact Name", "Emergency Contact Phone",
            "Anonymous", "Apparel", "Division"
    ];
    private readonly List<string> distanceHeaders =
    [
        "Clock Finish", "Chip Finish"
    ];
    private readonly List<string> timeHeaders =
    [
        "Laps Completed", "Elapsed Time (Clock)", "Elapsed Time (Chip)"
    ];

    public ExportResults(IMainWindow window, IdbInterface database)
    {
        InitializeComponent();
        ChronokeepInitialize();
        this.window = window;
        this.database = database;
        theEvent = database.GetCurrentEvent();
        if (theEvent == null || theEvent.Identifier == -1)
        {
            noOpen = true;
            return;
        }
        // Check if we're distance based or time based
        if (Constants.Timing.EVENT_TYPE_DISTANCE == theEvent.EventType)
        {
            commonHeaders.InsertRange(10, distanceHeaders);
            // Get the maximum number of segments.
            // if greater than 0, add (SEGMENT 1...X GUN TIME, SEGMENT 1...X
            // CHIP TIME and SEGMENT 1...X NAME) to the list of common headers
            int maxNumSegments1 = database.GetMaxSegments(theEvent.Identifier);
            if (maxNumSegments1 > 0)
            {
                // Go backwards so we don't have to recalculate where the insert is each lap
                for (int i = maxNumSegments1; i > 0; i--)
                {
                    commonHeaders.Insert(10, $"Segment {i} Chip Time");
                    commonHeaders.Insert(10, $"Segment {i} Clock Time");
                }
                // then do it again so we can add to the end in the right order
                for (int i = 1; i <= maxNumSegments1; i++)
                {
                    commonHeaders.Add($"Segment {i} Name");
                }
            }
        }
        else // Time based
        {
            commonHeaders.InsertRange(10, timeHeaders);
            // Remove "Chip Finish" and "Clock Finish" from the headers list.
            commonHeaders.Remove("Chip Finish");
            commonHeaders.Remove("");
            // Get the maximum number of laps a person completed.
            // if greater than 0, add LAP 1...X to the list of common headers
            int maxNumSegments1 = database.GetSegmentTimes(theEvent.Identifier, Constants.Timing.SEGMENT_FINISH).Select(result => result.Occurrence).Prepend(0).Max();
            for (int i = maxNumSegments1; i > 0; i--)
            {
                commonHeaders.Insert(10, $"Lap {i}");
            }
        }
        foreach (string name in commonHeaders)
        {
            HeadersList.Items.Add(new Parts.HeaderPart(name));
        }
    }

    public bool SetupError()
    {
        return noOpen;
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        window.WindowFinalize();
    }

    private async void Done_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.Export.ExportResults", "Done clicked.");
            List<string> headersToOutput = [];
            Dictionary<string, int> headerIndex = [];
            headersToOutput.AddRange(from headerBox in HeadersList.Items.Cast<Parts.HeaderPart>() where headerBox.Include.IsChecked == true select headerBox.NameValue);
            TopLevel? topLevel = GetTopLevel(this);
            if (topLevel != null)
            {
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
                    FileTypeChoices = [Utils.ExcelType],
                    SuggestedFileName = $"{theEvent!.YearCode} {theEvent.Name} Results.xlsx",
                    SuggestedStartLocation = startingFolder,
                });
                if (file is not null)
                {
                    // write to file
                    List<Participant> participants = database.GetParticipants(theEvent.Identifier);
                    List<TimeResult> results = database.GetTimingResults(theEvent.Identifier);
                    //results.RemoveAll(x => x.EventSpecificId == Constants.Timing.TIMERESULT_DUMMYPERSON);
                    results.Sort(TimeResult.CompareBySystemTime);
                    // Key is BIB -- Using BIB here instead of event specific because we want to know about unknown runners.
                    Dictionary<string, List<TimeResult>> resultDictionary = [];
                    Dictionary<string, bool> outputDictionary = [];
                    // (Bib, Occurence) - for Time Based Race exporting.
                    Dictionary<(string, int), TimeResult> occurrenceResultDictionary = [];
                    int maxLaps = 0;
                    foreach (TimeResult result in results)
                    {
                        if (!resultDictionary.TryGetValue(result.Bib, out List<TimeResult>? value))
                        {
                            value = [];
                            resultDictionary[result.Bib] = value;
                        }
                        value.Add(result);
                        if (result.SegmentId == Constants.Timing.SEGMENT_FINISH)
                        {
                            occurrenceResultDictionary[(result.Bib, result.Occurrence)] = result;
                            maxLaps = result.Occurrence > maxLaps ? result.Occurrence : maxLaps;
                        }
                        outputDictionary[result.Bib] = false;
                    }
                    string[] headers = new string[headersToOutput.Count];
                    foreach (string header in headersToOutput)
                    {
                        headerIndex[header] = headersToOutput.IndexOf(header);
                        headers[headerIndex[header]] = header;
                    }
                    List<object[]> data = [];
                    Dictionary<int, List<Segment>> distanceSegmentDict = [];
                    foreach (Segment seg in database.GetSegments(theEvent.Identifier))
                    {
                        if (!distanceSegmentDict.TryGetValue(seg.DistanceId, out List<Segment>? value))
                        {
                            value = [];
                            distanceSegmentDict[seg.DistanceId] = value;
                        }
                        value.Add(seg);
                    }
                    Dictionary<int, int> segmentNumberDict = [];
                    foreach (List<Segment> segments in distanceSegmentDict.Values)
                    {
                        segments.Sort((a, b) =>
                            // ReSharper disable once CompareOfFloatsByEqualityOperator
                            a.CumulativeDistance == b.CumulativeDistance ? a.Occurrence.CompareTo(b.Occurrence) : a.CumulativeDistance.CompareTo(b.CumulativeDistance)
                        );
                        int count = 1;
                        foreach (Segment segment in segments)
                        {
                            segmentNumberDict[segment.Identifier] = count;
                            count += 1;
                        }
                    }
                    // Output all known participants
                    foreach (Participant participant in participants)
                    {
                        outputDictionary[participant.Bib] = true;
                        object[] line = new object[headersToOutput.Count];
                        if (headerIndex.TryGetValue("Bib", out int bibIx))
                        {
                            line[bibIx] = participant.Bib;
                        }
                        if (headerIndex.TryGetValue("Distance", out int distIx))
                        {
                            line[distIx] = participant.Distance;
                        }
                        if (headerIndex.TryGetValue("Status", out int statIx))
                        {
                            line[statIx] = participant.EventSpecific.StatusStr;
                        }
                        if (headerIndex.TryGetValue("First", out int firstIx))
                        {
                            line[firstIx] = participant.FirstName;
                        }
                        if (headerIndex.TryGetValue("Last", out int lastIx))
                        {
                            line[lastIx] = participant.LastName;
                        }
                        if (headerIndex.TryGetValue("Birthday", out int bdayIx))
                        {
                            line[bdayIx] = participant.Birthdate;
                        }
                        if (headerIndex.TryGetValue("Age", out int agIx))
                        {
                            line[agIx] = participant.Age(theEvent.Date);
                        }
                        if (headerIndex.TryGetValue("Gender", out int gndIx))
                        {
                            line[gndIx] = participant.Gender;
                        }
                        if (headerIndex.TryGetValue("Street", out int streetIx))
                        {
                            line[streetIx] = participant.Street;
                        }
                        if (headerIndex.TryGetValue("Apartment", out int apartmentIx))
                        {
                            line[apartmentIx] = participant.Street2;
                        }
                        if (headerIndex.TryGetValue("City", out int cityIx))
                        {
                            line[cityIx] = participant.City;
                        }
                        if (headerIndex.TryGetValue("State", out int stateIx))
                        {
                            line[stateIx] = participant.State;
                        }
                        if (headerIndex.TryGetValue("Zip", out int zipIx))
                        {
                            line[zipIx] = participant.Zip;
                        }
                        if (headerIndex.TryGetValue("Country", out int countryIx))
                        {
                            line[countryIx] = participant.Country;
                        }
                        if (headerIndex.TryGetValue("Mobile", out int mobileIx))
                        {
                            line[mobileIx] = participant.Mobile;
                        }
                        if (headerIndex.TryGetValue("Email", out int emailIx))
                        {
                            line[emailIx] = participant.Email;
                        }
                        if (headerIndex.TryGetValue("Parent", out int parentIx))
                        {
                            line[parentIx] = participant.Parent;
                        }
                        if (headerIndex.TryGetValue("Comments", out int commentsIx))
                        {
                            line[commentsIx] = participant.Comments;
                        }
                        if (headerIndex.TryGetValue("Other", out int otherIx))
                        {
                            line[otherIx] = participant.Other;
                        }
                        if (headerIndex.TryGetValue("Owes", out int owesIx))
                        {
                            line[owesIx] = participant.Owes;
                        }
                        if (headerIndex.TryGetValue("Emergency Contact Name", out int emergencyNameIx))
                        {
                            line[emergencyNameIx] = participant.EcName;
                        }
                        if (headerIndex.TryGetValue("Emergency Contact Phone", out int emergencyPhoneIx))
                        {
                            line[emergencyPhoneIx] = participant.EcPhone;
                        }
                        if (headerIndex.TryGetValue("Anonymous", out int anonymousIx))
                        {
                            line[anonymousIx] = participant.PrettyAnonymous;
                        }
                        if (headerIndex.TryGetValue("Apparel", out int apparelIx))
                        {
                            line[apparelIx] = participant.EventSpecific.Apparel;
                        }
                        if (headerIndex.TryGetValue("Division", out int divIx))
                        {
                            line[divIx] = participant.EventSpecific.Division;
                        }
                        if (Constants.Timing.EVENT_TYPE_DISTANCE == theEvent.EventType)
                        {
                            if (resultDictionary.TryGetValue(participant.EventSpecific.Bib, out List<TimeResult>? oResList))
                            {
                                int segmentNum = 1;
                                foreach (TimeResult result in oResList)
                                {
                                    switch (result.SegmentId)
                                    {
                                        case Constants.Timing.SEGMENT_START:
                                        {
                                            if (headerIndex.TryGetValue("Start", out int startIx))
                                            {
                                                line[startIx] = result.Time;
                                            }

                                            break;
                                        }
                                        case Constants.Timing.SEGMENT_FINISH:
                                        {
                                            if (headerIndex.TryGetValue("Place", out int placeIx))
                                            {
                                                line[placeIx] = result.Place == -1 ? "" : result.Place;
                                            }
                                            if (headerIndex.TryGetValue("Age Group Place", out int agPlIx))
                                            {
                                                line[agPlIx] = result.AgePlace == -1 ? "" : result.AgePlace;
                                            }
                                            if (headerIndex.TryGetValue("Gender Place", out int gndPlIx))
                                            {
                                                line[gndPlIx] = result.GenderPlace == -1 ? "" : result.GenderPlace;
                                            }
                                            if (headerIndex.TryGetValue("Chip Finish", out int chipFinIx))
                                            {
                                                line[chipFinIx] = result.ChipTime;
                                            }
                                            if (headerIndex.TryGetValue("Clock Finish", out int clockFinIx))
                                            {
                                                line[clockFinIx] = result.Time;
                                            }

                                            break;
                                        }
                                        default:
                                        {
                                            if (Constants.Timing.SEGMENT_NONE != result.SegmentId)
                                            {
                                                if (segmentNumberDict.TryGetValue(result.SegmentId, out int segNumber))
                                                {
                                                    segmentNum = segNumber;
                                                }
                                                string key = $"Segment {segmentNum} Chip Time";
                                                if (headerIndex.TryGetValue(key, out int segChipTimeIx))
                                                {
                                                    line[segChipTimeIx] = result.ChipTime;
                                                }
                                                key = $"Segment {segmentNum} Clock Time";
                                                if (headerIndex.TryGetValue(key, out int segTimeIx))
                                                {
                                                    line[segTimeIx] = result.Time;
                                                }
                                                key = $"Segment {segmentNum++} Name";
                                                if (headerIndex.TryGetValue(key, out int segNameIx))
                                                {
                                                    line[segNameIx] = result.SegmentName;
                                                }
                                            }

                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        else // Time Based
                        {
                            int finalLap = -1;
                            if (headerIndex.TryGetValue("Start", out int startIx) && occurrenceResultDictionary.TryGetValue((participant.EventSpecific.Bib, 0), out TimeResult? startRes))
                            {
                                line[startIx] = startRes.Time;
                            }
                            for (int i = 1; i <= maxLaps; i++)
                            {
                                string key = $"Lap {i}";
                                if (!occurrenceResultDictionary.TryGetValue((participant.EventSpecific.Bib, i),
                                        out TimeResult? occRes)) continue;
                                finalLap = i;
                                if (headerIndex.TryGetValue(key, out int occIx))
                                {
                                    line[occIx] = occRes.LapTime;
                                }
                            }
                            if (occurrenceResultDictionary.TryGetValue((participant.EventSpecific.Bib, finalLap), out TimeResult? finalLapRes))
                            {
                                if (headerIndex.TryGetValue("Place", out int placeIx))
                                {
                                    line[placeIx] = finalLapRes.Place;
                                }
                                if (headerIndex.TryGetValue("Age Group Place", out int agePlaceIx))
                                {
                                    line[agePlaceIx] = finalLapRes.AgePlace;
                                }
                                if (headerIndex.TryGetValue("Gender Place", out int genderPlaceIx))
                                {
                                    line[genderPlaceIx] = finalLapRes.GenderPlace;
                                }
                                if (headerIndex.TryGetValue("Laps Completed", out int lapsCompletedIx))
                                {
                                    line[lapsCompletedIx] = finalLapRes.Occurrence;
                                }
                                if (headerIndex.TryGetValue("Elapsed Time (Clock)", out int clockElapsedIx))
                                {
                                    line[clockElapsedIx] = finalLapRes.Time;
                                }
                                if (headerIndex.TryGetValue("Elapsed Time (Chip)", out int chipElapsedIx))
                                {
                                    line[chipElapsedIx] = finalLapRes.ChipTime;
                                }
                            }
                        }
                        data.Add(line);
                    }
                    // Add data for unknown runners
                    foreach (string bib in outputDictionary.Keys)
                    {
                        if (outputDictionary[bib] || !string.IsNullOrEmpty(bib)) continue;
                        object[] line = new object[headersToOutput.Count];
                        if (headerIndex.TryGetValue("Bib", out int bibIx))
                        {
                            line[bibIx] = bib;
                        }
                        if (Constants.Timing.EVENT_TYPE_DISTANCE == theEvent.EventType)
                        {
                            if (resultDictionary.TryGetValue(bib, out List<TimeResult>? resList))
                            {
                                int segmentNum = 1;
                                foreach (TimeResult result in resList)
                                {
                                    switch (result.SegmentId)
                                    {
                                        case Constants.Timing.SEGMENT_START:
                                        {
                                            if (headerIndex.TryGetValue("Start", out int startIx))
                                            {
                                                line[startIx] = result.Time;
                                            }

                                            break;
                                        }
                                        case Constants.Timing.SEGMENT_FINISH:
                                        {
                                            if (headerIndex.TryGetValue("Place", out int plIx))
                                            {
                                                line[plIx] = result.Place == -1 ? "" : result.Place;
                                            }
                                            if (headerIndex.TryGetValue("Age Group Place", out int agPlIx))
                                            {
                                                line[agPlIx] = result.AgePlace == -1 ? "" : result.AgePlace;
                                            }
                                            if (headerIndex.TryGetValue("Gender Place", out int gndPlIx))
                                            {
                                                line[gndPlIx] = result.GenderPlace == -1 ? "" : result.GenderPlace;
                                            }
                                            if (headerIndex.TryGetValue("Chip Finish", out int chipFinIx))
                                            {
                                                line[chipFinIx] = result.ChipTime;
                                            }
                                            if (headerIndex.TryGetValue("Clock Finish", out int clockFinIx))
                                            {
                                                line[clockFinIx] = result.Time;
                                            }

                                            break;
                                        }
                                        default:
                                        {
                                            if (Constants.Timing.SEGMENT_NONE != result.SegmentId)
                                            {
                                                string key = $"Segment {segmentNum} Chip Time";
                                                if (headerIndex.TryGetValue(key, out int segChipTimeIx))
                                                {
                                                    line[segChipTimeIx] = result.ChipTime;
                                                }
                                                key = $"Segment {segmentNum} Clock Time";
                                                if (headerIndex.TryGetValue(key, out int segTimeIx))
                                                {
                                                    line[segTimeIx] = result.Time;
                                                }
                                                key = $"Segment {segmentNum++} Name";
                                                if (headerIndex.TryGetValue(key, out int segNameIx))
                                                {
                                                    line[segNameIx] = result.SegmentName;
                                                }
                                            }

                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        else // Time Based
                        {
                            int finalLap = -1;
                            if (headerIndex.TryGetValue("Start", out int startIx) && occurrenceResultDictionary.TryGetValue((bib, 0), out TimeResult? startRes))
                            {
                                line[startIx] = startRes.Time;
                            }
                            for (int i = 1; i <= maxLaps; i++)
                            {
                                string key = $"Lap {i}";
                                if (!occurrenceResultDictionary.TryGetValue((bib, i), out TimeResult? lapRes))
                                    continue;
                                finalLap = i;
                                if (headerIndex.TryGetValue(key, out int lapTimeIx))
                                {
                                    line[lapTimeIx] = lapRes.LapTime;
                                }
                            }
                            if (occurrenceResultDictionary.TryGetValue((bib, finalLap), out TimeResult? finRes))
                            {
                                if (headerIndex.TryGetValue("Place", out int plIx))
                                {
                                    line[plIx] = finRes.Place;
                                }
                                if (headerIndex.TryGetValue("Age Group Place", out int agePlaceIx))
                                {
                                    line[agePlaceIx] = finRes.AgePlace;
                                }
                                if (headerIndex.TryGetValue("Gender Place", out int genderPlaceIx))
                                {
                                    line[genderPlaceIx] = finRes.GenderPlace;
                                }
                                if (headerIndex.TryGetValue("Laps Completed", out int lapsCompletedIx))
                                {
                                    line[lapsCompletedIx] = finRes.Occurrence;
                                }
                                if (headerIndex.TryGetValue("Elapsed Time (Clock)", out int clockElapsedIx))
                                {
                                    line[clockElapsedIx] = finRes.Time;
                                }
                                if (headerIndex.TryGetValue("Elapsed Time (Chip)", out int chipElapsedIx))
                                {
                                    line[chipElapsedIx] = finRes.ChipTime;
                                }
                            }
                        }
                        data.Add(line);
                    }
                    IDataExporter exporter;
                    string extension = Path.GetExtension(file.Name);
                    Log.D("UI.Export.ExportResults", $"Extension is '{extension}'");
                    if (extension.Contains("xls", StringComparison.CurrentCulture))
                    {
                        exporter = new ExcelExporter();
                    }
                    else
                    {
                        StringBuilder format = new();
                        for (int i = 0; i < headers.Length; i++)
                        {
                            format.Append("\"{");
                            format.Append(i);
                            format.Append("}\",");
                        }
                        format.Remove(format.Length - 1, 1);
                        Log.D("UI.Export.ExportResults", $"The format is '{format}'");
                        exporter = new CsvExporter(format.ToString());
                    }
                    exporter.SetData(headers, data);
                    try
                    {
                        exporter.ExportData(file.TryGetLocalPath()!);
                        DialogBox.AsyncShow("File saved.");
                    }
                    catch (Exception ex)
                    {
                        Log.E("UI.Export.ExportResults.Error", ex.ToString());
                        DialogBox.AsyncShow("Error saving file.");
                        return;
                    }
                }
            }
            Close();
        }
        catch (Exception)
        {
            Log.D("UI.Export.ExportResults", "Error finishing export.");
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Export.ExportResults", "Cancel clicked.");
        Close();
    }

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}
