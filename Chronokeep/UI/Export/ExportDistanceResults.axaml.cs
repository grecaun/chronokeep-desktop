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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Chronokeep.Constants;

namespace Chronokeep.UI.Export;

public partial class ExportDistanceResults : ChronokeepWindow
{
    private readonly IMainWindow window;
    private readonly IdbInterface database;
    private readonly Event? theEvent;

    private readonly OutputType type;

    private readonly bool noOpen;

    private readonly Dictionary<string, Distance>? distanceDictionary;

    public ExportDistanceResults(IMainWindow window, IdbInterface database, OutputType type = OutputType.Boston)
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
        distanceDictionary = [];
        Log.D("ExportDistanceResults", "Adding distances to combobox.");
        foreach (Distance distance in database.GetDistances(theEvent.Identifier).Where(distance => Constants.Timing.DISTANCE_NO_LINKED_ID == distance.LinkedDistance))
        {
            distanceDictionary[distance.Identifier.ToString()] = distance;
            DistanceBox.Items.Add(new ComboBoxItem()
            {
                Content = distance.Name,
                Tag = distance.Identifier.ToString(),
            });
        }
        this.type = type;
        this.CanResize = false;
        bool supported = false;
        switch (type)
        {
            case OutputType.UltraSignup:
                Title = "Export UltraSignup Results";
                supported = true;
                break;
            case OutputType.RunSignup:
                Title = "Export RunSignup Results";
                break;
            case OutputType.Abbott:
                Title = "Export AbbottWMM Results";
                break;
            case OutputType.Boston:
            default:
                Title = "Export Boston Results";
                break;
        }
        switch (theEvent.EventType)
        {
            case Constants.Timing.EVENT_TYPE_TIME when !supported:
                DialogBox.Show("Exporting for a Time based event is not supported.");
                noOpen = true;
                return;
            case Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA when !supported:
                DialogBox.Show("Exporting for a Backyard Ultra event is not supported.");
                noOpen = true;
                return;
            default:
                switch (DistanceBox.Items.Count)
                {
                    case < 1:
                        DialogBox.Show("Oops, you don't appear to have any distances set up.");
                        noOpen = true;
                        return;
                    // don't open the window if we've only got one to output
                    case 1:
                    {
                        DistanceBox.SelectedIndex = 0;
                        Distance selected;
                        if (DistanceBox.SelectedItem != null && distanceDictionary.TryGetValue((string)((ComboBoxItem)DistanceBox.SelectedItem).Tag!, out Distance? oDist))
                        {
                            selected = oDist;
                        }
                        else
                        {
                            DialogBox.Show("Something went wrong with the distance. Exiting.");
                            noOpen = true;
                            return;
                        }
                        switch (type)
                        {
                            case OutputType.Boston:
                                SaveBoston(selected.Name);
                                break;
                            case OutputType.UltraSignup:
                                SaveUltraSignup(selected.Name);
                                break;
                            case OutputType.RunSignup:
                                SaveRunSignup(selected.Name);
                                break;
                            case OutputType.Abbott:
                                SaveAbbot(selected.Name);
                                break;
                            default:
                                DialogBox.Show("Something went wrong. No known output type specified.");
                                break;
                        }
                        noOpen = true;
                        break;
                    }
                }

                DistanceBox.Items.Insert(0, new ComboBoxItem()
                {
                    Content = "All",
                    Tag = "ALL_DISTANCES",
                });
                break;
        }
    }

    public bool SetupError()
    {
        return noOpen;
    }

    private async void SaveAllBoston()
    {
        try
        {
            TopLevel? topLevel = GetTopLevel(this);
            if (topLevel == null) return;
            IStorageFolder? startingFolder;
            try
            {
                startingFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(database.GetAppSetting(Settings.DEFAULT_EXPORT_DIR)!.Value));
            }
            catch
            {
                startingFolder = null;
            }
            IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                FileTypeChoices = [Utils.ExcelType],
                SuggestedFileName = $"{theEvent!.YearCode} {theEvent.Name} Boston.xlsx",
                SuggestedStartLocation = startingFolder,
            });
            if (file is null) return;
            string extension = Path.GetExtension(file.Name);
            string fileName = Path.GetFileNameWithoutExtension(file.Name);
            string filePath = file.TryGetLocalPath()!;
            foreach (Distance distance in distanceDictionary!.Values)
            {
                SaveBostonInternal(
                    distance.Name,
                    Path.Combine(filePath, $"{fileName} {distance.Name}{extension}"),
                    extension
                );
            }
            DialogBox.Show("Files saved.");
        }
        catch (Exception)
        {
            Log.D("ExportDistanceResults", "Error saving all boston.");
        }
    }

    private async void SaveAllUltraSignup()
    {
        try
        {
            TopLevel? topLevel = GetTopLevel(this);
            if (topLevel == null) return;
            IStorageFolder? startingFolder;
            try
            {
                startingFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(database.GetAppSetting(Settings.DEFAULT_EXPORT_DIR)!.Value));
            }
            catch
            {
                startingFolder = null;
            }
            IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                FileTypeChoices = [Utils.CsvType],
                SuggestedFileName = $"{theEvent!.YearCode} {theEvent.Name} UltraSignup.csv",
                SuggestedStartLocation = startingFolder,
            });
            if (file is null) return;
            string extension = Path.GetExtension(file.Name);
            string fileName = Path.GetFileNameWithoutExtension(file.Name);
            string filePath = file.TryGetLocalPath()!;
            foreach (Distance distance in distanceDictionary!.Values)
            {
                SaveUltraSignupInternal(
                    distance.Name,
                    Path.Combine(filePath, $"{fileName} {distance.Name}{extension}")
                );
            }
            DialogBox.Show("Files saved.");
        }
        catch (Exception)
        {
            Log.D("ExportDistanceResults", "Error saving all UltraSignup.");
        }
    }

    private async void SaveAllRunSignup()
    {
        try
        {
            TopLevel? topLevel = GetTopLevel(this);
            if (topLevel == null) return;
            IStorageFolder? startingFolder;
            try
            {
                startingFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(database.GetAppSetting(Settings.DEFAULT_EXPORT_DIR)!.Value));
            }
            catch
            {
                startingFolder = null;
            }
            IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                FileTypeChoices = [Utils.CsvType],
                SuggestedFileName = $"{theEvent!.YearCode} {theEvent.Name} RunSignup.csv",
                SuggestedStartLocation = startingFolder,
            });
            if (file is null) return;
            string extension = Path.GetExtension(file.Name);
            string fileName = Path.GetFileNameWithoutExtension(file.Name);
            string filePath = file.TryGetLocalPath()!;
            foreach (Distance distance in distanceDictionary!.Values)
            {
                SaveRunSignupInternal(
                    distance.Name,
                    Path.Combine(filePath, $"{fileName} {distance.Name}{extension}")
                );
            }
            DialogBox.Show("Files saved.");
        }
        catch (Exception)
        {
            Log.D("ExportDistanceResults", "Error saving all RunSignup.");
        }
    }

    private async void SaveAbbot(string distance)
    {
        try
        {
            TopLevel? topLevel = GetTopLevel(this);
            if (topLevel == null) return;
            IStorageFolder? startingFolder;
            try
            {
                startingFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(database.GetAppSetting(Settings.DEFAULT_EXPORT_DIR)!.Value));
            }
            catch
            {
                startingFolder = null;
            }
            IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                FileTypeChoices = [Utils.ExcelType],
                SuggestedFileName = $"{theEvent!.YearCode} {theEvent.Name} {distance} AbbotWMM.xlsx",
                SuggestedStartLocation = startingFolder,
            });
            if (file is null) return;
            SaveAbbotInternal(distance, file.TryGetLocalPath()!, Path.GetExtension(file.Name));
            DialogBox.Show("File saved.");
        }
        catch (Exception)
        {
            Log.D("ExportDistanceResults", "Error saving abbot.");
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
            if (Constants.Timing.SEGMENT_FINISH != result.SegmentId ||
                !participantDictionary.TryGetValue(result.Bib, out Participant? oPart) ||
                (result.DistanceName != distance) || result.Time.Length <= 4) continue;
            string country = oPart.Country;
            if (country.Length != 3)
            {
                if (country.Equals("ca", StringComparison.OrdinalIgnoreCase) || country.Equals("canada", StringComparison.OrdinalIgnoreCase))
                {
                    country = "CAN";
                }
                else if (country.Equals("ae", StringComparison.OrdinalIgnoreCase))
                {
                    country = "ARE";
                }
                else if (country.Equals("au", StringComparison.OrdinalIgnoreCase))
                {
                    country = "AUS";
                }
                else if (country.Equals("br", StringComparison.OrdinalIgnoreCase))
                {
                    country = "BRA";
                }
                else if (country.Equals("United States of America", StringComparison.OrdinalIgnoreCase))
                {
                    country = "USA";
                }
                else if (country.Equals("cr", StringComparison.OrdinalIgnoreCase))
                {
                    country = "CRI";
                }
                else if (country.Equals("cw", StringComparison.OrdinalIgnoreCase))
                {
                    country = "CUW";
                }
                else if (country.Equals("ch", StringComparison.OrdinalIgnoreCase))
                {
                    country = "CHE";
                }
                else if (country.Equals("de", StringComparison.OrdinalIgnoreCase))
                {
                    country = "DEU";
                }
                else if (country.Equals("do", StringComparison.OrdinalIgnoreCase))
                {
                    country = "DOM";
                }
                else if (country.Equals("es", StringComparison.OrdinalIgnoreCase))
                {
                    country = "ESP";
                }
                else if (country.Equals("gb", StringComparison.OrdinalIgnoreCase))
                {
                    country = "GBR";
                }
                else if (country.Equals("hn", StringComparison.OrdinalIgnoreCase))
                {
                    country = "HND";
                }
                else if (country.Equals("ie", StringComparison.OrdinalIgnoreCase))
                {
                    country = "IRL";
                }
                else if (country.Equals("jp", StringComparison.OrdinalIgnoreCase))
                {
                    country = "JPN";
                }
                else if (country.Equals("lv", StringComparison.OrdinalIgnoreCase))
                {
                    country = "LVA";
                }
                else if (country.Equals("mx", StringComparison.OrdinalIgnoreCase))
                {
                    country = "MEX";
                }
                else if (country.Equals("nl", StringComparison.OrdinalIgnoreCase))
                {
                    country = "NLD";
                }
                else if (country.Equals("nz", StringComparison.OrdinalIgnoreCase))
                {
                    country = "NZL";
                }
                else if (country.Equals("ru", StringComparison.OrdinalIgnoreCase))
                {
                    country = "RUS";
                }
                else if (country.Equals("tw", StringComparison.OrdinalIgnoreCase))
                {
                    country = "TWN";
                }
                else if (country.Equals("um", StringComparison.OrdinalIgnoreCase))
                {
                    country = "UMI";
                }
                else if (country.Equals("za", StringComparison.OrdinalIgnoreCase))
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
                result.Gender.Equals("Man", StringComparison.OrdinalIgnoreCase) ? "M" : result.Gender.Equals("Woman", StringComparison.OrdinalIgnoreCase) ? "F" : "",
                result.ChipTime[..(result.ChipTime.Length > 4 ? result.ChipTime.Length -4 : 0)],
                result.PlaceStr,
                result.GenderPlaceStr
            ]);
        }
        IDataExporter exporter;
        Log.D("UI.Export.ExportDistanceResults", $"Extension is '{extension}'");
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
            Log.D("UI.Export.ExportDistanceResults", $"The format is '{format}'");
            exporter = new CsvExporter(format.ToString());
        }
        exporter.SetData(headers, data);
        exporter.ExportData(fileName);
    }

    private async void SaveBoston(string distance)
    {
        try
        {
            TopLevel? topLevel = GetTopLevel(this);
            if (topLevel == null) return;
            IStorageFolder? startingFolder;
            try
            {
                startingFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(database.GetAppSetting(Settings.DEFAULT_EXPORT_DIR)!.Value));
            }
            catch
            {
                startingFolder = null;
            }
            IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                FileTypeChoices = [Utils.ExcelType],
                SuggestedFileName = $"{theEvent!.YearCode} {theEvent.Name} {distance} Boston.xlsx",
                SuggestedStartLocation = startingFolder,
            });
            if (file is null) return;
            SaveBostonInternal(distance, file.TryGetLocalPath()!, Path.GetExtension(file.Name));
            DialogBox.Show("File saved.");
        }
        catch (Exception)
        {
            Log.D("ExportDistanceResults", "Error saving boston.");
        }
    }

    private void SaveBostonInternal(string distance, string fileName, string extension)
    {
        Distance? dist = database.GetDistances(theEvent!.Identifier).FirstOrDefault(d => d.Name == distance);
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
        foreach (Segment seg in segments)
        {
            Log.D("UI.Export.ExportDistanceResults", $"Segment:  {seg.Name}");
            tmp.Add($"{seg.CumulativeDistance} {Distances.DistanceString(seg.DistanceUnit)}");
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
            if (Constants.Timing.SEGMENT_FINISH != result.SegmentId ||
                !participantDictionary.TryGetValue(result.Bib, out Participant? tPart) ||
                (result.DistanceName != distance) || result.Time.Length <= 4) continue;
            List<string> values =
            [
                result.Last,
                result.First,
                tPart.City,
                tPart.State,
                result.Gender.Equals("Man", StringComparison.OrdinalIgnoreCase) ? "M" : result.Gender.Equals("Woman", StringComparison.OrdinalIgnoreCase) ? "F" : result.Gender.Equals("Non-Binary", StringComparison.OrdinalIgnoreCase) ? "X" : "",
                tPart.Birthdate,
                result.Age(theEvent.Date).ToString(),
                result.Time[..(result.Time.Length > 4 ? result.Time.Length - 4 : 0)],
                result.ChipTime[..(result.ChipTime.Length > 4 ? result.ChipTime.Length -4 : 0)],
                ""
            ];
            values.AddRange(segments.Select(seg => segmentResults.TryGetValue((result.Bib, seg.Identifier), out TimeResult? res)
                ? res.ChipTime[..(res.ChipTime.Length > 4 ? res.ChipTime.Length - 4 : 0)]
                : ""));
            data.Add([.. values]);
        }
        IDataExporter exporter;
        Log.D("UI.Export.ExportDistanceResults", $"Extension is '{extension}'");
        if (extension.Contains("xls", StringComparison.CurrentCulture))
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
            Log.D("UI.Export.ExportDistanceResults", $"The format is '{format}'");
            exporter = new CsvExporter(format.ToString());
        }
        exporter.SetData([.. headers], data);
        exporter.ExportData(fileName);
    }

    private async void SaveUltraSignup(string distance)
    {
        try
        {
            TopLevel? topLevel = GetTopLevel(this);
            if (topLevel == null) return;
            IStorageFolder? startingFolder;
            try
            {
                startingFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(database.GetAppSetting(Settings.DEFAULT_EXPORT_DIR)!.Value));
            }
            catch
            {
                startingFolder = null;
            }
            IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                FileTypeChoices = [Utils.CsvType],
                SuggestedFileName = $"{theEvent!.YearCode} {theEvent.Name} {distance} UltraSignup.csv",
                SuggestedStartLocation = startingFolder,
            });
            if (file is null) return;
            string filename = file.TryGetLocalPath()!;
            string[] fileSplit = filename.Split('.');
            if (fileSplit.Length != 2)
            {
                DialogBox.Show("Filename appears to be invalid.");
                return;
            }
            if (!fileSplit[1].Equals("csv"))
            {
                filename = $"{fileSplit[0]}.csv";
            }
            SaveUltraSignupInternal(distance, filename);
            DialogBox.Show("File saved.");
        }
        catch (Exception)
        {
            Log.D("ExportDistanceResults", "Error saving UltraSignup.");
        }
    }

    private async void SaveRunSignup(string distance)
    {
        try
        {
            TopLevel? topLevel = GetTopLevel(this);
            if (topLevel == null) return;
            IStorageFolder? startingFolder;
            try
            {
                startingFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(database.GetAppSetting(Settings.DEFAULT_EXPORT_DIR)!.Value));
            }
            catch
            {
                startingFolder = null;
            }
            IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                FileTypeChoices = [Utils.CsvType],
                SuggestedFileName = $"{theEvent!.YearCode} {theEvent.Name} {distance} RunSignup.csv",
                SuggestedStartLocation = startingFolder,
            });
            if (file is null) return;
            string filename = file.TryGetLocalPath()!;
            string[] fileSplit = filename.Split('.');
            if (fileSplit.Length != 2)
            {
                DialogBox.Show("Filename appears to be invalid.");
                return;
            }
            if (!fileSplit[1].Equals("csv"))
            {
                filename = $"{fileSplit[0]}.csv";
            }
            SaveRunSignupInternal(distance, filename);
            DialogBox.Show("File saved.");
        }
        catch (Exception)
        {
            Log.D("ExportDistanceResults", "Error saving RunSignup.");
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
        if (theEvent.EventType is Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA or Constants.Timing.EVENT_TYPE_TIME)
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
            if (Constants.Timing.SEGMENT_FINISH != result.SegmentId ||
                !participantDictionary.TryGetValue(result.Bib, out Participant? yPart) ||
                (result.DistanceName != distance)) continue;
            int status = 1;
            if (Constants.Timing.TIMERESULT_STATUS_DNF == result.Status)
            {
                status = 2;
            }
            else if (Constants.Timing.DISTANCE_TYPE_UNOFFICIAL == result.Type)
            {
                status = 4;
            }
            object[] newLine =
            [
                result.Place > 0 ? result.Place.ToString() : "",
                result.ChipTime,
                result.First,
                result.Last,
                result.Gender.Equals("Man", StringComparison.OrdinalIgnoreCase) ? "M" : result.Gender.Equals("Woman", StringComparison.OrdinalIgnoreCase) ? "F" : result.Gender.Equals("Non-Binary", StringComparison.OrdinalIgnoreCase) ? "X" : "",
                result.Age(theEvent.Date),
                yPart.Birthdate,
                result.Bib,
                yPart.City,
                yPart.State,
                status
            ];
            if (theEvent.EventType is Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA or Constants.Timing.EVENT_TYPE_TIME)
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
                    newLine[1] = (localLinked.DistanceValue * hour).ToString(CultureInfo.InvariantCulture);
                }
                else if (result.DistanceName.Length > 0
                         && distances.TryGetValue(result.DistanceName, out Distance? localDist)
                         && localDist.DistanceValue > 0)
                {
                    newLine[1] = (localDist.DistanceValue * hour).ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    newLine[1] = "0";
                }
            }
            data.Add(newLine);
        }
        StringBuilder format = new();
        for (int i = 0; i < headers.Length; i++)
        {
            format.Append("\"{");
            format.Append(i);
            format.Append("}\",");
        }
        format.Remove(format.Length - 1, 1);
        Log.D("UI.Export.ExportDistanceResults", $"The format is '{format}'");
        CsvExporter exporter = new(format.ToString());
        exporter.SetData(headers, data);
        exporter.ExportData(fileName);
    }

    private void SaveRunSignupInternal(string distance, string fileName)
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
                        result.Gender.Equals("Man", StringComparison.OrdinalIgnoreCase) ? "M" : result.Gender.Equals("Woman", StringComparison.OrdinalIgnoreCase) ? "F" : result.Gender.Equals("Non-Binary", StringComparison.OrdinalIgnoreCase) ? "X" : "",
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
        Log.D("UI.Export.ExportDistanceResults", $"The format is '{format}'");
        CsvExporter exporter = new(format.ToString());
        exporter.SetData(headers, data);
        exporter.ExportData(fileName);
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        window.WindowFinalize();
    }

    private void Done_Click(object? sender, RoutedEventArgs e)
    {
        // Ensure we've selected a distance and that distance is either known
        if (DistanceBox.SelectedItem != null
            && distanceDictionary!.TryGetValue((string)((ComboBoxItem)DistanceBox.SelectedItem).Tag!, out Distance? tDist))
        {
            switch (type)
            {
                case OutputType.Boston:
                    SaveBoston(tDist.Name);
                    break;
                case OutputType.UltraSignup:
                    SaveUltraSignup(tDist.Name);
                    break;
                case OutputType.RunSignup:
                    SaveRunSignup(tDist.Name);
                    break;
                case OutputType.Abbott:
                    SaveAbbot(tDist.Name);
                    break;
                default:
                    DialogBox.Show("Something went wrong. No known output type specified.");
                    break;
            }
        }
        // Check if they've told us to save all distances.
        else if ((string)((ComboBoxItem)DistanceBox.SelectedItem!).Tag! == "ALL_DISTANCES")
        {
            switch (type)
            {
                case OutputType.Boston:
                    SaveAllBoston();
                    break;
                case OutputType.UltraSignup:
                    SaveAllUltraSignup();
                    break;
                case OutputType.RunSignup:
                    SaveAllRunSignup();
                    break;
                case OutputType.Abbott:
                    DialogBox.Show("Exporting all for Abbott is not supported.");
                    return;
                default:
                    DialogBox.Show("Something went wrong. No known output type specified.");
                    break;
            }
        }
        else
        {
            DialogBox.Show("Something went wrong with the distance. Exiting.");
        }
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Export.ExportDistanceResults", "Cancel clicked.");
        Close();
    }

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}
public enum OutputType
{
    Boston,
    UltraSignup,
    RunSignup,
    Abbott
}