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
using System.Text;

namespace Chronokeep.UI.Export;

public partial class ExportDistanceResults : Window
{
    private readonly IMainWindow window;
    private readonly IDBInterface database;
    private readonly Event? theEvent;

    private readonly OutputType type;

    private readonly bool noOpen;

    private readonly Dictionary<string, Distance>? distanceDictionary;

    public ExportDistanceResults(IMainWindow window, IDBInterface database, OutputType type = OutputType.Boston)
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
        distanceDictionary = [];
        Log.D("ExportDistanceResults", "Adding distances to combobox.");
        foreach (Distance distance in database.GetDistances(theEvent.Identifier))
        {
            // Don't list linked distances.
            if (Constants.Timing.DISTANCE_NO_LINKED_ID == distance.LinkedDistance)
            {
                distanceDictionary[distance.Identifier.ToString()] = distance;
                distanceBox.Items.Add(new ComboBoxItem()
                {
                    Content = distance.Name,
                    Tag = distance.Identifier.ToString(),
                });
            }
        }
        this.type = type;
        this.MinWidth = 300;
        this.MinHeight = 200;
        this.Width = 300;
        this.Height = 220;
        this.CanResize = false;
        bool supported = false;
        if (OutputType.Boston == type)
        {
            this.Title = "Export Boston Results";
        }
        else if (OutputType.UltraSignup == type)
        {
            this.Title = "Export UltraSignup Results";
            supported = true;
        }
        else if (OutputType.Runsignup == type)
        {
            this.Title = "Export Runsignup Results";
        }
        else if (OutputType.Abbott == type)
        {
            this.Title = "Export AbbottWMM Results";
        }
        if (Constants.Timing.EVENT_TYPE_TIME == theEvent.EventType && !supported)
        {
            DialogBox.Show("Exporting for a Time based event is not supported.");
            noOpen = true;
            return;
        }
        if (Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA == theEvent.EventType && !supported)
        {
            DialogBox.Show("Exporting for a Backyard Ultra event is not supported.");
            noOpen = true;
            return;
        }
        if (distanceBox.Items.Count < 1)
        {
            DialogBox.Show("Oops, you don't appear to have any distances set up.");
            noOpen = true;
            return;
        }
        // don't open the window if we've only got one to output
        if (distanceBox.Items.Count == 1)
        {
            distanceBox.SelectedIndex = 0;
            Distance selected;
            if (distanceBox.SelectedItem != null && distanceDictionary.TryGetValue((string)((ComboBoxItem)distanceBox.SelectedItem).Tag!, out Distance? oDist))
            {
                selected = oDist;
            }
            else
            {
                DialogBox.Show("Something went wrong with the distance. Exiting.");
                noOpen = true;
                return;
            }
            if (OutputType.Boston == type)
            {
                SaveBoston(selected.Name);
            }
            else if (OutputType.UltraSignup == type)
            {
                SaveUltraSignup(selected.Name);
            }
            else if (OutputType.Runsignup == type)
            {
                SaveRunsignup(selected.Name);
            }
            else if (OutputType.Abbott == type)
            {
                SaveAbbot(selected.Name);
            }
            else
            {
                DialogBox.Show("Something went wrong. No known output type specified.");
            }
            noOpen = true;
        }
        distanceBox.Items.Insert(0, new ComboBoxItem()
        {
            Content = "All",
            Tag = "ALL_DISTANCES",
        });
    }

    public bool SetupError()
    {
        return noOpen;
    }

    private async void SaveAllBoston()
    {
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
                SuggestedFileName = string.Format("{0} {1} Boston.{2}", theEvent!.YearCode, theEvent.Name, "xlsx"),
                SuggestedStartLocation = startingFolder,
            });
            if (file is not null)
            {
                string extension = Path.GetExtension(file.Name);
                string fileName = Path.GetFileNameWithoutExtension(file.Name);
                string filePath = file.TryGetLocalPath()!;
                foreach (Distance distance in distanceDictionary!.Values)
                {
                    SaveBostonInternal(
                        distance.Name,
                        Path.Combine(filePath, string.Format("{0} {1}{2}", fileName, distance.Name, extension)),
                        extension
                        );
                }
                DialogBox.Show("Files saved.");
            }
        }
    }

    private async void SaveAllUltraSignup()
    {
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
                FileTypeChoices = [Utils.CSVType],
                SuggestedFileName = string.Format("{0} {1} Ultrasignup.{2}", theEvent!.YearCode, theEvent.Name, "csv"),
                SuggestedStartLocation = startingFolder,
            });
            if (file is not null)
            {
                string extension = Path.GetExtension(file.Name);
                string fileName = Path.GetFileNameWithoutExtension(file.Name);
                string filePath = file.TryGetLocalPath()!;
                foreach (Distance distance in distanceDictionary!.Values)
                {
                    SaveUltraSignupInternal(
                        distance.Name,
                        Path.Combine(filePath, string.Format("{0} {1}{2}", fileName, distance.Name, extension))
                        );
                }
                DialogBox.Show("Files saved.");
            }
        }
    }

    private async void SaveAllRunsignup()
    {
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
                FileTypeChoices = [Utils.CSVType],
                SuggestedFileName = string.Format("{0} {1} Runsignup.{2}", theEvent!.YearCode, theEvent.Name, "csv"),
                SuggestedStartLocation = startingFolder,
            });
            if (file is not null)
            {
                string extension = Path.GetExtension(file.Name);
                string fileName = Path.GetFileNameWithoutExtension(file.Name);
                string filePath = file.TryGetLocalPath()!;
                foreach (Distance distance in distanceDictionary!.Values)
                {
                    SaveRunsignupInternal(
                        distance.Name,
                        Path.Combine(filePath, string.Format("{0} {1}{2}", fileName, distance.Name, extension))
                        );
                }
                DialogBox.Show("Files saved.");
            }
        }
    }

    private async void SaveAbbot(string distance)
    {
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
                SuggestedFileName = string.Format("{0} {1} {2} AbbotWMM.{3}", theEvent!.YearCode, theEvent.Name, distance, "xlsx"),
                SuggestedStartLocation = startingFolder,
            });
            if (file is not null)
            {
                SaveAbbotInternal(distance, file.TryGetLocalPath()!, Path.GetExtension(file.Name));
                DialogBox.Show("File saved.");
            }
        }
    }
    private void SaveAbbotInternal(string distance, string fileName, string extension)
    {
        string[] headers =
        [
            "name_prefix",  // leave empty
            "name_suffix",  // leave empty
            "first_name",
            "last_name",
            "email",        // leave empty
            "start_num",    // bib
            "date_of_birth",// DD/MM/YYYY or MM/DD/YYYY
            "nationality",  // IOC Code, ISO-3 CODE or IAAF Code
            "gender",       // M or F
            "finish_time",  // Chip time
            "place",        // overall
            "place_no_sex"  // gender place
        ];
        List<object[]> data = [];
        List<Participant> participants = database.GetParticipants(theEvent!.Identifier);
        Dictionary<string, Participant> participantDictionary = [];
        foreach (Participant person in participants)
        {
            participantDictionary[person.Bib] = person;
        }
        List<TimeResult> results = database.GetTimingResults(theEvent.Identifier);
        foreach (TimeResult result in results)
        {
            if (Constants.Timing.SEGMENT_FINISH == result.SegmentId && participantDictionary.TryGetValue(result.Bib, out Participant? oPart) && (result.DistanceName == distance) && result.Time.Length > 4)
            {
                string country = oPart.Country;
                if (country.Length != 3)
                {
                    if (country.Equals("ca", System.StringComparison.OrdinalIgnoreCase) || country.Equals("canada", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "CAN";
                    }
                    else if (country.Equals("ae", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "ARE";
                    }
                    else if (country.Equals("au", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "AUS";
                    }
                    else if (country.Equals("br", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "BRA";
                    }
                    else if (country.Equals("United States of America", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "USA";
                    }
                    else if (country.Equals("cr", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "CRI";
                    }
                    else if (country.Equals("cw", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "CUW";
                    }
                    else if (country.Equals("ch", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "CHE";
                    }
                    else if (country.Equals("de", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "DEU";
                    }
                    else if (country.Equals("do", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "DOM";
                    }
                    else if (country.Equals("es", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "ESP";
                    }
                    else if (country.Equals("gb", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "GBR";
                    }
                    else if (country.Equals("hn", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "HND";
                    }
                    else if (country.Equals("ie", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "IRL";
                    }
                    else if (country.Equals("jp", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "JPN";
                    }
                    else if (country.Equals("lv", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "LVA";
                    }
                    else if (country.Equals("mx", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "MEX";
                    }
                    else if (country.Equals("nl", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "NLD";
                    }
                    else if (country.Equals("nz", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "NZL";
                    }
                    else if (country.Equals("ru", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "RUS";
                    }
                    else if (country.Equals("tw", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "TWN";
                    }
                    else if (country.Equals("um", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "UMI";
                    }
                    else if (country.Equals("za", System.StringComparison.OrdinalIgnoreCase))
                    {
                        country = "ZAF";
                    }
                    else
                    {
                        country = "";
                    }
                }
                data.Add(
                [
                    "",
                    "",
                    result.Last,
                    result.First,
                    "",
                    result.Bib,
                    oPart.Birthdate,
                    country,
                    result.Gender.Equals("Man", System.StringComparison.OrdinalIgnoreCase) ? "M" : result.Gender.Equals("Woman", System.StringComparison.OrdinalIgnoreCase) ? "F" : "",
                    result.ChipTime[..(result.ChipTime.Length > 4 ? result.ChipTime.Length -4 : 0)],
                    result.PlaceStr,
                    result.GenderPlaceStr
                ]);
            }
        }
        IDataExporter exporter;
        Log.D("UI.Export.ExportDistanceResults", string.Format("Extension is '{0}'", extension));
        if (extension.Contains("xls", System.StringComparison.CurrentCulture))
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
            Log.D("UI.Export.ExportDistanceResults", string.Format("The format is '{0}'", format.ToString()));
            exporter = new CSVExporter(format.ToString());
        }
        exporter.SetData(headers, data);
        exporter.ExportData(fileName);
    }

    private async void SaveBoston(string distance)
    {
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
                SuggestedFileName = string.Format("{0} {1} {2} Boston.{3}", theEvent!.YearCode, theEvent.Name, distance, "xlsx"),
                SuggestedStartLocation = startingFolder,
            });
            if (file is not null)
            {
                SaveBostonInternal(distance, file.TryGetLocalPath()!, Path.GetExtension(file.Name));
                DialogBox.Show("File saved.");
            }
        }
    }

    private void SaveBostonInternal(string distance, string fileName, string extension)
    {
        Distance? dist = null;
        foreach (Distance d in database.GetDistances(theEvent!.Identifier))
        {
            if (d.Name == distance)
            {
                dist = d;
                break;
            }
        }
        List<string> headers =
        [
            theEvent.Name,         // event name
                "", "", "", "", "", "", "", "", ""
        ];
        List<Segment> segments = database.GetSegments(theEvent.Identifier);
        segments.RemoveAll(x => dist == null || x.DistanceId != dist.Identifier);
        segments.Sort((a, b) => a.CumulativeDistance.CompareTo(b.CumulativeDistance));
        for (int i = 0; i < segments.Count; i++)
        {
            headers.Add("");
        }
        List<object[]> data = [];
        List<string> tmp =
        [
            theEvent.Date,         // event date
            "", "", "", "", "", "", "", "", ""
        ];
        for (int i = 0; i < segments.Count; i++)
        {
            tmp.Add("");
        }
        data.Add([.. tmp]);
        tmp =
        [
            "INSERT EVENT CERTIFICATION HERE",         // event certification number
            "", "", "", "", "", "", "", "", ""
        ];
        for (int i = 0; i < segments.Count; i++)
        {
            tmp.Add("");
        }
        data.Add([.. tmp]);
        // actual header
        tmp =
        [
            "Last Name",
            "First Name",
            "City",
            "State/Province",
            "Gender",
            "Date of Birth",
            "Age",
            "Clock Time",
            "Chip/Net Time",
            "Wheelchair"
        ];
        // Get segments for header.
        Dictionary<int, int> segmentsHeaderPos = [];
        foreach (Segment seg in segments)
        {
            Log.D("UI.Export.ExportDistanceResults", "Segment:  " + seg.Name);
            segmentsHeaderPos[seg.Identifier] = data[0].Length;
            tmp.Add(string.Format("{0} {1}", seg.CumulativeDistance, Constants.Distances.DistanceString(seg.DistanceUnit)));
        }
        data.Add([.. tmp]);
        List<Participant> participants = database.GetParticipants(theEvent.Identifier);
        Dictionary<string, Participant> participantDictionary = [];
        foreach (Participant person in participants)
        {
            participantDictionary[person.Bib] = person;
        }
        Dictionary<(string, int), TimeResult> segmentResults = [];
        List<TimeResult> results = database.GetTimingResults(theEvent.Identifier);
        foreach (TimeResult result in results)
        {
            segmentResults[(result.Bib, result.SegmentId)] = result;
        }
        foreach (TimeResult result in results)
        {
            if (Constants.Timing.SEGMENT_FINISH == result.SegmentId && participantDictionary.TryGetValue(result.Bib, out Participant? tPart) && (result.DistanceName == distance) && result.Time.Length > 4)
            {
                List<string> values =
                [
                        result.Last,
                            result.First,
                            tPart.City,
                            tPart.State,
                            result.Gender.Equals("Man", System.StringComparison.OrdinalIgnoreCase) ? "M" : result.Gender.Equals("Woman", System.StringComparison.OrdinalIgnoreCase) ? "F" : result.Gender.Equals("Non-Binary", System.StringComparison.OrdinalIgnoreCase) ? "X" : "",
                            tPart.Birthdate,
                            result.Age(theEvent.Date).ToString(),
                            result.Time[..(result.Time.Length > 4 ? result.Time.Length - 4 : 0)],
                            result.ChipTime[..(result.ChipTime.Length > 4 ? result.ChipTime.Length -4 : 0)],
                            ""
                ];
                foreach (Segment seg in segments)
                {
                    if (segmentResults.TryGetValue((result.Bib, seg.Identifier), out TimeResult? res))
                    {
                        values.Add(res.ChipTime[..(res.ChipTime.Length > 4 ? res.ChipTime.Length - 4 : 0)]);
                    }
                    else
                    {
                        values.Add("");
                    }
                }
                data.Add([.. values]);
            }
        }
        IDataExporter exporter;
        Log.D("UI.Export.ExportDistanceResults", string.Format("Extension is '{0}'", extension));
        if (extension.Contains("xls", System.StringComparison.CurrentCulture))
        {
            exporter = new ExcelExporter();
        }
        else
        {
            StringBuilder format = new();
            for (int i = 0; i < headers.Count; i++)
            {
                format.Append("\"{");
                format.Append(i);
                format.Append("}\",");
            }
            format.Remove(format.Length - 1, 1);
            Log.D("UI.Export.ExportDistanceResults", string.Format("The format is '{0}'", format.ToString()));
            exporter = new CSVExporter(format.ToString());
        }
        exporter.SetData([.. headers], data);
        exporter.ExportData(fileName);
    }

    private async void SaveUltraSignup(string distance)
    {
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
                FileTypeChoices = [Utils.CSVType],
                SuggestedFileName = string.Format("{0} {1} {2} Ultrasignup.{3}", theEvent!.YearCode, theEvent.Name, distance, "csv"),
                SuggestedStartLocation = startingFolder,
            });
            if (file is not null)
            {
                string filename = file.TryGetLocalPath()!;
                string[] fileSplit = filename.Split('.');
                if (fileSplit.Length != 2)
                {
                    DialogBox.Show("Filename appears to be invalid.");
                    return;
                }
                if (!fileSplit[1].Equals("csv"))
                {
                    filename = string.Format("{0}.{1}", fileSplit[0], "csv");
                }
                SaveUltraSignupInternal(distance, filename);
                DialogBox.Show("File saved.");
            }
        }
    }

    private async void SaveRunsignup(string distance)
    {
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
                FileTypeChoices = [Utils.CSVType],
                SuggestedFileName = string.Format("{0} {1} {2} Runsignup.{3}", theEvent!.YearCode, theEvent.Name, distance, "csv"),
                SuggestedStartLocation = startingFolder,
            });
            if (file is not null)
            {
                string filename = file.TryGetLocalPath()!;
                string[] fileSplit = filename.Split('.');
                if (fileSplit.Length != 2)
                {
                    DialogBox.Show("Filename appears to be invalid.");
                    return;
                }
                if (!fileSplit[1].Equals("csv"))
                {
                    filename = string.Format("{0}.{1}", fileSplit[0], "csv");
                }
                SaveRunsignupInternal(distance, filename);
                DialogBox.Show("File saved.");
            }
        }
    }

    private void SaveUltraSignupInternal(string distance, string fileName)
    {
        string[] headers =
        [
            "place",
                "time",
                "first",
                "last",
                "gender",
                "age",
                "dob",
                "bib",
                "city",
                "state",
                "status"
        ];
        Dictionary<string, Participant> participantDictionary = [];
        foreach (Participant person in database.GetParticipants(theEvent!.Identifier))
        {
            participantDictionary[person.Bib] = person;
        }
        List<object[]> data = [];
        List<TimeResult> results = database.GetTimingResults(theEvent.Identifier);
        if (Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA == theEvent.EventType || Constants.Timing.EVENT_TYPE_TIME == theEvent.EventType)
        {
            headers[1] = "distance";
            Dictionary<string, TimeResult> finalResult = [];
            foreach (TimeResult result in results)
            {
                if (!finalResult.TryGetValue(result.Identifier, out TimeResult? other))
                {
                    other = result;
                    finalResult[result.Identifier] = result;
                }
                if (other.Occurrence < result.Occurrence && result.Finish)
                {
                    finalResult[result.Identifier] = result;
                }
            }
            results.RemoveAll(x => !finalResult.ContainsValue(x));
        }
        foreach (TimeResult result in results)
        {
            if (Constants.Timing.SEGMENT_FINISH == result.SegmentId && participantDictionary.TryGetValue(result.Bib, out Participant? yPart) && (result.DistanceName == distance))
            {
                int status = 1;
                if (Constants.Timing.TIMERESULT_STATUS_DNF == result.Status)
                {
                    status = 2;
                }
                else if (Constants.Timing.DISTANCE_TYPE_UNOFFICIAL == result.Type)
                {
                    status = 4;
                }
                var newLine = new object[]
                {
                            result.Place > 0 ? result.Place.ToString() : "",
                            result.ChipTime,
                            result.First,
                            result.Last,
                            result.Gender.Equals("Man", System.StringComparison.OrdinalIgnoreCase) ? "M" : result.Gender.Equals("Woman", System.StringComparison.OrdinalIgnoreCase) ? "F" : result.Gender.Equals("Non-Binary", System.StringComparison.OrdinalIgnoreCase) ? "X" : "",
                            result.Age(theEvent.Date),
                            yPart.Birthdate,
                            result.Bib,
                            yPart.City,
                            yPart.State,
                            status
                };
                if (Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA == theEvent.EventType || Constants.Timing.EVENT_TYPE_TIME == theEvent.EventType)
                {
                    Dictionary<string, Distance> distances = [];
                    foreach (Distance dist in database.GetDistances(theEvent.Identifier))
                    {
                        distances[dist.Name] = dist;
                    }
                    int hour = (result.Occurrence / 2) + 1;
                    if (result.LinkedDistanceName.Length > 0
                        && distances.TryGetValue(result.LinkedDistanceName, out Distance? localLinked)
                        && localLinked.DistanceValue > 0)
                    {
                        newLine[1] = (localLinked.DistanceValue * hour).ToString();
                    }
                    else if (result.DistanceName.Length > 0
                        && distances.TryGetValue(result.DistanceName, out Distance? localDist)
                        && localDist.DistanceValue > 0)
                    {
                        newLine[1] = (localDist.DistanceValue * hour).ToString();
                    }
                    else
                    {
                        newLine[1] = "0";
                    }
                }
                data.Add(newLine);
            }
        }
        StringBuilder format = new();
        for (int i = 0; i < headers.Length; i++)
        {
            format.Append("\"{");
            format.Append(i);
            format.Append("}\",");
        }
        format.Remove(format.Length - 1, 1);
        Log.D("UI.Export.ExportDistanceResults", string.Format("The format is '{0}'", format.ToString()));
        CSVExporter exporter = new(format.ToString());
        exporter.SetData(headers, data);
        exporter.ExportData(fileName);
    }

    private void SaveRunsignupInternal(string distance, string fileName)
    {
        string[] headers =
        [
            "place",
                "clock time",
                "chip time",
                "first",
                "last",
                "gender",
                "age",
                "bib",
                "city",
                "state"
        ];
        Dictionary<string, Participant> participantDictionary = [];
        foreach (Participant person in database.GetParticipants(theEvent!.Identifier))
        {
            participantDictionary[person.Bib] = person;
        }
        List<object[]> data = [];
        foreach (TimeResult result in database.GetTimingResults(theEvent.Identifier))
        {
            if (Constants.Timing.SEGMENT_FINISH == result.SegmentId && participantDictionary.TryGetValue(result.Bib, out Participant? zPart) && (result.DistanceName == distance))
            {
                data.Add(
                [
                    result.Place > 0 ? result.Place.ToString() : "",
                        result.Time,
                        result.ChipTime,
                        result.First,
                        result.Last,
                        result.Gender.Equals("Man", System.StringComparison.OrdinalIgnoreCase) ? "M" : result.Gender.Equals("Woman", System.StringComparison.OrdinalIgnoreCase) ? "F" : result.Gender.Equals("Non-Binary", System.StringComparison.OrdinalIgnoreCase) ? "X" : "",
                        result.Age(theEvent.Date),
                        result.Bib,
                        zPart.City,
                        zPart.State,
                    ]);
            }
        }

        StringBuilder format = new();
        for (int i = 0; i < headers.Length; i++)
        {
            format.Append("\"{");
            format.Append(i);
            format.Append("}\",");
        }
        format.Remove(format.Length - 1, 1);
        Log.D("UI.Export.ExportDistanceResults", string.Format("The format is '{0}'", format.ToString()));
        CSVExporter exporter = new(format.ToString());
        exporter.SetData(headers, data);
        exporter.ExportData(fileName);
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        window?.WindowFinalize(this);
    }

    private void Done_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Distance selected;
        // Ensure we've selected a distance and that distance is either known
        if (distanceBox.SelectedItem != null
            && distanceDictionary!.TryGetValue((string)((ComboBoxItem)distanceBox.SelectedItem).Tag!, out Distance? tDist))
        {
            selected = tDist;
            if (OutputType.Boston == type)
            {
                SaveBoston(selected.Name);
            }
            else if (OutputType.UltraSignup == type)
            {
                SaveUltraSignup(selected.Name);
            }
            else if (OutputType.Runsignup == type)
            {
                SaveRunsignup(selected.Name);
            }
            else if (OutputType.Abbott == type)
            {
                SaveAbbot(selected.Name);
            }
            else
            {
                DialogBox.Show("Something went wrong. No known output type specified.");
            }
        }
        // Check if they've told us to save all distances.
        else if ((string)((ComboBoxItem)distanceBox.SelectedItem!).Tag! == "ALL_DISTANCES")
        {
            if (OutputType.Boston == type)
            {
                SaveAllBoston();
            }
            else if (OutputType.UltraSignup == type)
            {
                SaveAllUltraSignup();
            }
            else if (OutputType.Runsignup == type)
            {
                SaveAllRunsignup();
            }
            else if (OutputType.Abbott == type)
            {
                DialogBox.Show("Exporting all for Abbott is not supported.");
                return;
            }
            else
            {
                DialogBox.Show("Something went wrong. No known output type specified.");
            }
        }
        else
        {
            DialogBox.Show("Something went wrong with the distance. Exiting.");
        }
        Close();
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Export.ExportDistanceResults", "Cancel clicked.");
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
public enum OutputType
{
    Boston,
    UltraSignup,
    Runsignup,
    Abbott
}