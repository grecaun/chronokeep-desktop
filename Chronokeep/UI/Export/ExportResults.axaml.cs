using Avalonia;
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

public partial class ExportResults : Window
{
    private readonly IMainWindow window;
    private readonly IDBInterface database;
    private readonly Event? theEvent;

    private readonly bool noOpen = false;

    private readonly int maxNumSegments;
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
        "Laps Completed", "Ellapsed Time (Clock)", "Ellapsed Time (Chip)"
    ];

    public ExportResults(IMainWindow window, IDBInterface database)
    {
        InitializeComponent();
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
            maxNumSegments = database.GetMaxSegments(theEvent.Identifier);
            if (maxNumSegments > 0)
            {
                // Go backwards so we don't have to recalculate where the insert is each lap
                for (int i = maxNumSegments; i > 0; i--)
                {
                    commonHeaders.Insert(10, string.Format("Segment {0} Chip Time", i));
                    commonHeaders.Insert(10, string.Format("Segment {0} Clock Time", i));
                }
                // then do it again so we can add to the end in the right order
                for (int i = 1; i <= maxNumSegments; i++)
                {
                    commonHeaders.Add(string.Format("Segment {0} Name", i));
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
            maxNumSegments = 0;
            foreach (TimeResult result in database.GetSegmentTimes(theEvent.Identifier, Constants.Timing.SEGMENT_FINISH))
            {
                maxNumSegments = result.Occurrence > maxNumSegments ? result.Occurrence : maxNumSegments;
            }
            for (int i = maxNumSegments; i > 0; i--)
            {
                commonHeaders.Insert(10, string.Format("Lap {0}", i));
            }
        }
        foreach (string name in commonHeaders)
        {
            headersList.Items.Add(new Parts.HeaderPart(name));
        }
    }

    public bool SetupError()
    {
        return noOpen;
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        window?.WindowFinalize(this);
    }

    private async void Done_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Export.ExportResults", "Done clicked.");
        List<string> headersToOutput = [];
        Dictionary<string, int> headerIndex = [];
        foreach (Parts.HeaderPart headerBox in headersList.Items.Cast<Parts.HeaderPart>())
        {
            if (headerBox.Include.IsChecked == true)
            {
                headersToOutput.Add(headerBox.NameValue);
            }
        }
        var topLevel = TopLevel.GetTopLevel(this);
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
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                FileTypeChoices = [Utils.ExcelType],
                SuggestedFileName = string.Format("{0} {1} Results.{2}", theEvent!.YearCode, theEvent.Name, "xlsx"),
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
                        line[emergencyNameIx] = participant.ECName;
                    }
                    if (headerIndex.TryGetValue("Emergency Contact Phone", out int emergencyPhoneIx))
                    {
                        line[emergencyPhoneIx] = participant.ECPhone;
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
                                if (Constants.Timing.SEGMENT_START == result.SegmentId)
                                {
                                    if (headerIndex.TryGetValue("Start", out int startIx))
                                    {
                                        line[startIx] = result.Time;
                                    }
                                }
                                else if (Constants.Timing.SEGMENT_FINISH == result.SegmentId)
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
                                }
                                else if (Constants.Timing.SEGMENT_NONE != result.SegmentId)
                                {
                                    if (segmentNumberDict.TryGetValue(result.SegmentId, out int segNumber))
                                    {
                                        segmentNum = segNumber;
                                    }
                                    string key = string.Format("Segment {0} Chip Time", segmentNum);
                                    if (headerIndex.TryGetValue(key, out int segChipTimeIx))
                                    {
                                        line[segChipTimeIx] = result.ChipTime;
                                    }
                                    key = string.Format("Segment {0} Clock Time", segmentNum);
                                    if (headerIndex.TryGetValue(key, out int segTimeIx))
                                    {
                                        line[segTimeIx] = result.Time;
                                    }
                                    key = string.Format("Segment {0} Name", segmentNum++);
                                    if (headerIndex.TryGetValue(key, out int segNameIx))
                                    {
                                        line[segNameIx] = result.SegmentName;
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
                            string key = string.Format("Lap {0}", i);
                            if (occurrenceResultDictionary.TryGetValue((participant.EventSpecific.Bib, i), out TimeResult? occRes))
                            {
                                finalLap = i;
                                if (headerIndex.TryGetValue(key, out int occIx))
                                {
                                    line[occIx] = occRes.LapTime;
                                }
                            }
                        }
                        if (occurrenceResultDictionary.TryGetValue((participant.EventSpecific.Bib, finalLap), out TimeResult? finalLapRes))
                        {
                            if (headerIndex.TryGetValue("Place", out int placeIx))
                            {
                                line[placeIx] = finalLapRes.Place;
                            }
                            if (headerIndex.TryGetValue("Age Group Place", out int agPlIx))
                            {
                                line[agPlIx] = finalLapRes.AgePlace;
                            }
                            if (headerIndex.TryGetValue("Gender Place", out int gndPlIx))
                            {
                                line[gndPlIx] = finalLapRes.GenderPlace;
                            }
                            if (headerIndex.TryGetValue("Laps Completed", out int lapsComplIx))
                            {
                                line[lapsComplIx] = finalLapRes.Occurrence;
                            }
                            if (headerIndex.TryGetValue("Ellapsed Time (Clock)", out int clockEllapIx))
                            {
                                line[clockEllapIx] = finalLapRes.Time;
                            }
                            if (headerIndex.TryGetValue("Ellapsed Time (Chip)", out int chipEllapIx))
                            {
                                line[chipEllapIx] = finalLapRes.ChipTime;
                            }
                        }
                    }
                    data.Add(line);
                }
                // Add data for unknown runners
                foreach (string bib in outputDictionary.Keys)
                {
                    if (!outputDictionary[bib] && string.IsNullOrEmpty(bib))
                    {
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
                                    if (Constants.Timing.SEGMENT_START == result.SegmentId)
                                    {
                                        if (headerIndex.TryGetValue("Start", out int startIx))
                                        {
                                            line[startIx] = result.Time;
                                        }
                                    }
                                    else if (Constants.Timing.SEGMENT_FINISH == result.SegmentId)
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
                                    }
                                    else if (Constants.Timing.SEGMENT_NONE != result.SegmentId)
                                    {
                                        string key = string.Format("Segment {0} Chip Time", segmentNum);
                                        if (headerIndex.TryGetValue(key, out int segChipTimeIx))
                                        {
                                            line[segChipTimeIx] = result.ChipTime;
                                        }
                                        key = string.Format("Segment {0} Clock Time", segmentNum);
                                        if (headerIndex.TryGetValue(key, out int segTimeIx))
                                        {
                                            line[segTimeIx] = result.Time;
                                        }
                                        key = string.Format("Segment {0} Name", segmentNum++);
                                        if (headerIndex.TryGetValue(key, out int segNameIx))
                                        {
                                            line[segNameIx] = result.SegmentName;
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
                                string key = string.Format("Lap {0}", i);
                                if (occurrenceResultDictionary.TryGetValue((bib, i), out TimeResult? lapRes))
                                {
                                    finalLap = i;
                                    if (headerIndex.TryGetValue(key, out int lapTimeIx))
                                    {
                                        line[lapTimeIx] = lapRes.LapTime;
                                    }
                                }
                            }
                            if (occurrenceResultDictionary.TryGetValue((bib, finalLap), out TimeResult? finRes))
                            {
                                if (headerIndex.TryGetValue("Place", out int plIx))
                                {
                                    line[plIx] = finRes.Place;
                                }
                                if (headerIndex.TryGetValue("Age Group Place", out int agPlIx))
                                {
                                    line[agPlIx] = finRes.AgePlace;
                                }
                                if (headerIndex.TryGetValue("Gender Place", out int gndPlIx))
                                {
                                    line[gndPlIx] = finRes.GenderPlace;
                                }
                                if (headerIndex.TryGetValue("Laps Completed", out int lapsComplIx))
                                {
                                    line[lapsComplIx] = finRes.Occurrence;
                                }
                                if (headerIndex.TryGetValue("Ellapsed Time (Clock)", out int clockEllapIx))
                                {
                                    line[clockEllapIx] = finRes.Time;
                                }
                                if (headerIndex.TryGetValue("Ellapsed Time (Chip)", out int chipEllapIx))
                                {
                                    line[chipEllapIx] = finRes.ChipTime;
                                }
                            }
                        }
                        data.Add(line);
                    }
                }
                IDataExporter exporter;
                string extension = Path.GetExtension(file.Name);
                Log.D("UI.Export.ExportResults", string.Format("Extension is '{0}'", extension));
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
                    Log.D("UI.Export.ExportResults", string.Format("The format is '{0}'", format.ToString()));
                    exporter = new CSVExporter(format.ToString());
                }
                exporter.SetData(headers, data);
                try
                {
                    exporter.ExportData(file.TryGetLocalPath()!);
                    DialogBox.Show("File saved.");
                }
                catch (Exception ex)
                {
                    Log.E("UI.Export.ExportResults.Error", ex.ToString());
                    DialogBox.Show("Error saving file.");
                    return;
                }
            }
        }
        this.Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Export.ExportResults", "Cancel clicked.");
        this.Close();
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