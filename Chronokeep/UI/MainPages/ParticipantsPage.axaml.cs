using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.IO;
using Chronokeep.Interfaces.UI;
using Chronokeep.IO;
using Chronokeep.Network.API;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronoKeepAPI;
using Chronokeep.UI.Import;
using Chronokeep.UI.Participants;
using Chronokeep.UI.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chronokeep.UI.MainPages;

public partial class ParticipantsPage : UserControl, IMainPage
{
    private readonly IMainWindow mWindow;
    private readonly IdbInterface database;
    private readonly Event? theEvent;
    private List<Participant> allParticipants = [];
    private readonly List<Participant> conflicts = [];

    private readonly bool loaded;

    public ParticipantsPage(IMainWindow mainWindow, IdbInterface database)
    {
        InitializeComponent();
        mWindow = mainWindow;
        this.database = database;
        theEvent = database.GetCurrentEvent();
        SortBox.SelectedIndex = 0;
        loaded = true;
        UpdateDistancesBox();
    }

    public async void UpdateView()
    {
        try
        {
            Log.D("UI.MainPages.ParticipantsPage", "Updating Participants Page.");
            if (theEvent == null || theEvent.Identifier < 0)
            {
                return;
            }
            int distanceId = DistanceBox.SelectedItem != null ? Convert.ToInt32(((ComboBoxItem)DistanceBox.SelectedItem).Tag) : -1;
            Log.D("PartPage", $"Distance ID is {distanceId}");
            List<Participant> newList = [];
            List<Participant> allParts = [];
            await Task.Run(() =>
            {
                allParticipants = database.GetParticipants(theEvent.Identifier);
                newList.AddRange(distanceId == -1
                    ? allParticipants
                    : database.GetParticipants(theEvent.Identifier, distanceId));
            });
            UpdateBibStats();
            if (SortBox.SelectedItem != null)
            {
                switch (((ComboBoxItem)SortBox.SelectedItem).Content)
                {
                    case "Name":
                        newList.Sort(Participant.CompareByName);
                        break;
                    case "Bib":
                        newList.Sort(Participant.CompareByBib);
                        break;
                    default:
                        newList.Sort();
                        break;
                }
            }
            else
            {
                newList.Sort();
            }
            string search = SearchBox is { Text: not null } ? SearchBox.Text.Trim() : "";
            newList.RemoveAll(x => x.IsNotMatch(search));
            ParticipantsList.ItemsSource = newList;
            if (theEvent.ApiId > 0 && theEvent.ApiEventId.Length > 1)
            {
                ApiPanel.IsVisible = true;
            }
            else
            {
                ApiPanel.IsVisible = false;
            }
            conflicts.Clear();
            HashSet<(int, int)> conflictParticipantIdentifiers = [];
            foreach (Participant outer in allParticipants)
            {
                foreach (Participant inner in allParticipants)
                {
                    if (outer.Equals(inner)) continue;
                    // Check for bib conflicts
                    // Check if the bib matches (and isn't empty string) and they don't have the same Basic (FirstName, LastName, Gender, and Birthday) values.
                    if ((outer.Bib.Equals(inner.Bib, StringComparison.OrdinalIgnoreCase) && outer.Bib.Length > 0 && !outer.IsBasicMatch(inner))
                        // Or they do have the same Basic Values but their Distance or Bib has changed.
                        || (outer.IsBasicMatch(inner) && !(outer.Distance.Equals(inner.Distance, StringComparison.OrdinalIgnoreCase) && outer.Bib.Equals(inner.Bib, StringComparison.OrdinalIgnoreCase))))
                    {
                        // 
                        if (!conflictParticipantIdentifiers.Contains((outer.Identifier, inner.Identifier))) {
                            conflicts.Add(outer);
                            conflicts.Add(inner);
                        }
                        // Add both values to the list of conflicts so it always matches the previous line when checked later.
                        conflictParticipantIdentifiers.Add((outer.Identifier, inner.Identifier));
                        conflictParticipantIdentifiers.Add((inner.Identifier, outer.Identifier));
                    }
                }
            }
            if (conflicts.Count > 0)
            {
                ConflictsBtn.Content = $"Conflicts - {conflicts.Count}";
                ConflictsBtn.IsVisible = true;
            }
            else
            {
                ConflictsBtn.IsVisible = false;
            }
            Log.D("UI.MainPages.ParticipantsPage", "Participants updated.");
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.ParticipantsPage", "Error updating view.");
        }
    }

    private void UpdateDistancesBox()
    {
        Log.D("UI.MainPages.ParticipantsPage", "Updating distances box.");
        DistanceBox.Items.Clear();
        DistanceBox.Items.Add(new ComboBoxItem()
        {
            Content = "All",
            Tag = "-1"
        });
        if (theEvent == null || theEvent.Identifier < 0)
        {
            return;
        }
        List<Distance> distances = database.GetDistances(theEvent.Identifier);
        distances.Sort();
        foreach (Distance d in distances)
        {
            DistanceBox.Items.Add(new ComboBoxItem()
            {
                Content = d.Name,
                Tag = d.Identifier.ToString()
            });
        }
        DistanceBox.SelectedIndex = 0;
    }

    private static void UpdateDatabase() { }

    public void KeyboardCtrlA()
    {
        Add_Click(null, null);
    }

    public void KeyboardCtrlS() { }

    public void KeyboardCtrlZ() { }

    public void Closing()
    {
        if (database.GetAppSetting(Constants.Settings.UPDATE_ON_PAGE_CHANGE)!.Value == Constants.Settings.SETTING_TRUE)
        {
            UpdateDatabase();
        }
    }

    private async void DownloadParticipants()
    {
        try
        {
            // Get API to upload.
            if (theEvent!.ApiId < 0 && theEvent.ApiEventId.Length > 1)
            {
                Download.Content = "Download";
                return;
            }
            ApiObject api = database.GetApi(theEvent.ApiId)!;
            string[] eventIds = theEvent.ApiEventId.Split(',');
            if (eventIds.Length != 2)
            {
                Download.Content = "Download";
                return;
            }
            try
            {
                int page = 1;
                List<ApiPerson> newPersons = [];
                do
                {
                    GetParticipantsResponse response = await ApiHandlers.GetParticipants(api, eventIds[0], eventIds[1], 50, page);
                    newPersons.AddRange(response.Participants);
                    Log.D("UI.MainPages.ParticipantsPage", $"{response.Participants.Count} participants downloaded.");
                    if (response.Participants.Count != 50)
                    {
                        break;
                    }
                    page++;
                } while (true);
                Log.D("UI.MainPages.ParticipantsPage", $"{newPersons.Count} total participants downloaded.");
                // Key is (First, Last, Birthdate, Distance)
                Dictionary<(string, string, string, string), Participant> partDictionary = [];
                Dictionary<string, Participant> partEsDictionary = [];
                Dictionary<string, Distance> distDictionary = [];
                string uniqueId = "";
                if (database.GetAppSetting(Constants.Settings.PROGRAM_UNIQUE_MODIFIER) is AppSetting programId)
                {
                    uniqueId = $"{programId.Value}-";
                }
                foreach (Participant p in database.GetParticipants(theEvent.Identifier))
                {
                    partDictionary[(p.FirstName, p.LastName, p.Birthdate, p.Distance.ToLower())] = p;
                    partEsDictionary[$"{uniqueId}{p.EventSpecific.Identifier}"] = p;
                }
                foreach (Distance d in database.GetDistances(theEvent.Identifier))
                {
                    distDictionary[d.Name.ToLower()] = d;
                }
                List<Participant> partsToUpdate = [];
                List<Participant> partsToAdd = [];
                foreach (ApiPerson person in newPersons)
                {
                    person.Trim();
                    person.FormatData();
                    // A person must have a distance and they must have a first or last name.
                    // All other values are optional.
                    if (person.Distance.Length < 1 || (person.First.Length < 1 && person.Last.Length < 1))
                    {
                        continue;
                    }
                    if (!distDictionary.TryGetValue(person.Distance.ToLower(), out Distance? _))
                    {
                        Distance newDistance = new(person.Distance, theEvent.Identifier);
                        newDistance.Identifier = database.AddDistance(newDistance);
                        distDictionary.Add(newDistance.Name.ToLower(), newDistance);
                    };
                    if (partEsDictionary.TryGetValue(person.Identifier, out Participant? old) && old.IsSimilar(person))
                    {
                        // Only update if a bib exists, and it has not been updated in the software since it was uploaded.
                        // Uploaded Version should equal Version, Version will be higher if it was updated after upload.
                        if (person.Bib.Length < 1 || old.EventSpecific.UploadedVersion < old.EventSpecific.Version)
                        {
                            continue;
                        }
                        Participant newPart = new(
                            old.Identifier,
                            person.First.Length > 0 ? person.First : old.FirstName,
                            person.Last.Length > 0 ? person.Last : old.LastName,
                            old.Street,
                            old.City,
                            old.State,
                            old.Zip,
                            person.Birthdate,
                            new EventSpecific(
                                old.EventSpecific.Identifier,
                                theEvent.Identifier,
                                distDictionary[person.Distance.ToLower()].Identifier,
                                distDictionary[person.Distance.ToLower()].Name,
                                person.Bib,
                                old.EventSpecific.CheckedIn,
                                old.EventSpecific.Comments,
                                old.EventSpecific.Owes,
                                old.EventSpecific.Other,
                                old.EventSpecific.Status,
                                old.EventSpecific.AgeGroupName,
                                old.EventSpecific.AgeGroupId,
                                person.Anonymous,
                                person.SmsEnabled,
                                person.Apparel,
                                old.EventSpecific.Division,
                                old.EventSpecific.Version,
                                old.EventSpecific.UploadedVersion
                            ),
                            old.Email,
                            old.Phone,
                            person.Mobile.Length > 0 ? person.Mobile : old.Mobile,
                            old.Parent,
                            old.Country,
                            old.Street2,
                            person.Gender,
                            old.EcName,
                            old.EcPhone
                        );
                        // Check if the bib has changed
                        if (old.Bib.Length > 0 && !old.Bib.Equals(person.Bib, StringComparison.OrdinalIgnoreCase))
                        {
                            // Add the old value so we can track it.
                            old.Identifier = -1;
                            partsToAdd.Add(old);
                        }
                        partsToUpdate.Add(newPart);
                    }
                    else if (partDictionary.TryGetValue((person.First, person.Last, person.Birthdate, person.Distance.ToLower()), out Participant? oldTwo))
                    {
                        // Only update if a bib exists, and it has not been updated in the software since it was uploaded.
                        // Uploaded Version should equal Version, Version will be higher if it was updated after upload.
                        if (person.Bib.Length <= 0 || oldTwo.EventSpecific.UploadedVersion < oldTwo.EventSpecific.Version)
                        {
                            continue;
                        }
                        Participant newPart = new(
                            oldTwo.Identifier,
                            person.First.Length > 0 ? person.First : oldTwo.FirstName,
                            person.Last.Length > 0 ? person.Last : oldTwo.LastName,
                            oldTwo.Street,
                            oldTwo.City,
                            oldTwo.State,
                            oldTwo.Zip,
                            person.Birthdate,
                            new EventSpecific(
                                oldTwo.EventSpecific.Identifier,
                                theEvent.Identifier,
                                distDictionary[person.Distance.ToLower()].Identifier,
                                distDictionary[person.Distance.ToLower()].Name,
                                person.Bib,
                                oldTwo.EventSpecific.CheckedIn,
                                oldTwo.EventSpecific.Comments,
                                oldTwo.EventSpecific.Owes,
                                oldTwo.EventSpecific.Other,
                                oldTwo.EventSpecific.Status,
                                oldTwo.EventSpecific.AgeGroupName,
                                oldTwo.EventSpecific.AgeGroupId,
                                person.Anonymous,
                                person.SmsEnabled,
                                person.Apparel,
                                oldTwo.EventSpecific.Division,
                                oldTwo.EventSpecific.Version,
                                oldTwo.EventSpecific.UploadedVersion
                            ),
                            oldTwo.Email,
                            oldTwo.Phone,
                            person.Mobile.Length > 0 ? person.Mobile : oldTwo.Mobile,
                            oldTwo.Parent,
                            oldTwo.Country,
                            oldTwo.Street2,
                            person.Gender,
                            oldTwo.EcName,
                            oldTwo.EcPhone
                        );
                        // Check if the bib has changed
                        if (old!.Bib.Length > 0 && !oldTwo.Bib.Equals(person.Bib, StringComparison.OrdinalIgnoreCase))
                        {
                            // Add the old value so we can track it.
                            old.Identifier = -1;
                            partsToAdd.Add(old);
                        }
                        partsToUpdate.Add(newPart);
                    }
                    else
                    {
                        partsToAdd.Add(
                            new Participant(
                                person.First,
                                person.Last,
                                "",
                                "",
                                "",
                                "",
                                person.Birthdate,
                                new EventSpecific(
                                    theEvent.Identifier,
                                    distDictionary[person.Distance.ToLower()].Identifier,
                                    distDictionary[person.Distance.ToLower()].Name,
                                    person.Bib,
                                    0,
                                    "",
                                    "",
                                    "",
                                    person.Anonymous,
                                    person.SmsEnabled,
                                    person.Apparel,
                                    ""
                                ),
                                "",
                                "",
                                person.Mobile,
                                "",
                                "",
                                "",
                                person.Gender,
                                "",
                                ""
                            )
                        );
                    }
                }
                if (partsToUpdate.Count > 0)
                {
                    database.UpdateParticipants(partsToUpdate);
                }
                if (partsToAdd.Count > 0)
                {
                    database.AddParticipants(partsToAdd);
                }
            }
            catch (ApiException ex)
            {
                DialogBox.AsyncShow(ex.Message);
                Download.Content = "Download";
                return;
            }
            Download.Content = "Download";
            UpdateView();
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.ParticipantsPage", "Error downloading participants.");
        }
    }

    private async void UploadParticipants()
    {
        try
        {
            // Get API to upload.
            if (theEvent!.ApiId < 0 || theEvent.ApiEventId.Length < 1)
            {
                Upload.Content = "Upload";
                return;
            }
            ApiObject api = database.GetApi(theEvent.ApiId)!;
            string[] eventIds = theEvent.ApiEventId.Split(',');
            if (eventIds.Length != 2)
            {
                Upload.Content = "Upload";
                return;
            }
            // Get results to upload.
            List<Participant> participants = database.GetParticipants(theEvent.Identifier);
            List<BibChipAssociation> bibChips = database.GetBibChips(theEvent.Identifier);
            if (participants.Count < 1)
            {
                Log.D("UI.MainPages.ParticipantsPage", "Nothing to upload.");
                Upload.Content = "Upload";
                return;
            }
            // Change Participant to APIPerson
            List<ApiPerson> upParticipants = [];
            List<BibChip> upBibChips = [];
            Log.D("UI.MainPages.ParticipantsPage", $"Participants count: {participants.Count}");
            AppSetting programId = database.GetAppSetting(Constants.Settings.PROGRAM_UNIQUE_MODIFIER)!;
            string uniqueId = $"{programId.Value}-";
            upParticipants.AddRange(participants.Select(part => new ApiPerson(part, uniqueId)));
            Log.D("UI.MainPages.ParticipantsPage", $"BibChips count: {bibChips.Count}");
            upBibChips.AddRange(bibChips.Select(bc => new BibChip() { Bib = bc.Bib, Chip = bc.Chip, }));
            Log.D("UI.MainPages.ParticipantsPage", $"Attempting to upload {upParticipants.Count} participants.");
            int total = 0;
            int loops = upParticipants.Count / Constants.Timing.API_LOOP_COUNT;
            AddResultsResponse response;
            for (int i = 0; i < loops; i += 1)
            {
                try
                {
                    response = await ApiHandlers.UploadParticipants(api, eventIds[0], eventIds[1], upParticipants.GetRange(i * Constants.Timing.API_LOOP_COUNT, Constants.Timing.API_LOOP_COUNT));
                }
                catch (ApiException ex)
                {
                    DialogBox.AsyncShow(ex.Message);
                    Upload.Content = "Upload";
                    return;
                }
                total += response.Count;
                Log.D("UI.MainPages.ParticipantsPage", $"Total: {total} Count: {response.Count}");
            }
            int leftovers = upParticipants.Count - (loops * Constants.Timing.API_LOOP_COUNT);
            if (leftovers > 0)
            {
                try
                {
                    response = await ApiHandlers.UploadParticipants(api, eventIds[0], eventIds[1], upParticipants.GetRange(loops * Constants.Timing.API_LOOP_COUNT, leftovers));
                }
                catch (ApiException ex)
                {
                    DialogBox.AsyncShow(ex.Message);
                    Upload.Content = "Upload";
                    return;
                }
                total += response.Count;
                Log.D("UI.MainPages.TimingPage", $"Total: {total} Count: {response.Count}");
                Log.D("UI.MainPages.TimingPage", $"Upload finished. Count total: {total}");
            }
            foreach (Participant part in participants)
            {
                // record the version number that we uploaded, should default to 0 for anything we haven't touched
                part.EventSpecific.UploadedVersion = part.EventSpecific.Version;
            }
            database.UpdateParticipants(participants);
            Log.D("UI.MainPages.ParticipantsPage", $"Attempting to upload {upBibChips.Count} BibChips.");
            total = 0;
            loops = upBibChips.Count / Constants.Timing.API_LOOP_COUNT;
            for (int i = 0; i < loops; i += 1)
            {
                try
                {
                    response = await ApiHandlers.UploadBibChips(api, eventIds[0], eventIds[1], upBibChips.GetRange(i * Constants.Timing.API_LOOP_COUNT, Constants.Timing.API_LOOP_COUNT));
                }
                catch (ApiException ex)
                {
                    DialogBox.AsyncShow(ex.Message);
                    Upload.Content = "Upload";
                    return;
                }
                total += response.Count;
                Log.D("UI.MainPages.ParticipantsPage", $"Total: {total} Count: {response.Count}");
            }
            leftovers = upBibChips.Count - (loops * Constants.Timing.API_LOOP_COUNT);
            if (leftovers > 0)
            {
                try
                {
                    response = await ApiHandlers.UploadBibChips(api, eventIds[0], eventIds[1], upBibChips.GetRange(loops * Constants.Timing.API_LOOP_COUNT, leftovers));
                }
                catch (ApiException ex)
                {
                    DialogBox.AsyncShow(ex.Message);
                    Upload.Content = "Upload";
                    return;
                }
                total += response.Count;
                Log.D("UI.MainPages.TimingPage", $"Total: {total} Count: {response.Count}");
                Log.D("UI.MainPages.TimingPage", $"Upload finished. Count total: {total}");
            }
            Upload.Content = "Upload";
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.ParticipantsPage", "Error uploading participants.");
        }
    }

    private async void Import_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.MainPages.ParticipantsPage", "Import Excel clicked.");
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
            IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                FileTypeFilter = [Utils.ExcelType, FilePickerFileTypes.All],
                AllowMultiple = false,
                SuggestedStartLocation = startingFolder,
            });
            if (files.Count <= 0) return;
            string ext = Path.GetExtension(files[0].Name);
            Log.D("UI.MainPages.ParticipantsPage", $"Extension found: {ext}");
            try
            {
                IDataImporter importer;
                if (ext is ".xlsx" or ".xls")
                {
                    importer = new ExcelImporter(files[0].TryGetLocalPath()!);
                }
                else
                {
                    importer = new CsvImporter(files[0].TryGetLocalPath()!);
                }
                importer.FetchHeaders();
                ImportFileWindow importWindow = ImportFileWindow.NewWindow(mWindow, importer, database);
                mWindow.AddWindow(importWindow);
                _ = importWindow.ShowDialog((Window)mWindow);
            }
            catch (Exception ex)
            {
                DialogBox.AsyncShow("There was a problem importing the file.");
                Log.E("UI.MainPages.ParticipantsPage", $"Something went wrong when trying to read the Excel file. {ex.StackTrace}");
            }
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.ParticipantsPage", "Error importing participants.");
        }
    }

    private async void Export_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.MainPages.ParticipantsPage", "Export clicked.");
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
                FileTypeChoices = [Utils.ExcelType],
                SuggestedFileName = $"{theEvent!.YearCode} {theEvent.Name} Entrants.xlsx",
                SuggestedStartLocation = startingFolder,
            });
            if (file is null) return;
            if (theEvent == null) return;
            await Task.Run(() =>
            {
                Log.D("UI.MainPages.ParticipantsPage", $"Event has name {theEvent.Name} and date of {theEvent.Date} and finally has ID {theEvent.Identifier}");
                List<Participant> parts = database.GetParticipants(theEvent.Identifier);
                string[] headers = [
                    "Bib",
                    "Distance",
                    "Status",
                    "First",
                    "Last",
                    "Birthday",
                    "Age",
                    "Age Group",
                    "Division",
                    "Street",
                    "Apartment",
                    "City",
                    "State",
                    "Zip",
                    "Country",
                    "Phone",
                    "Mobile",
                    "Email",
                    "Parent",
                    "Gender",
                    "Comments",
                    "Other",
                    "Owes",
                    "Emergency Contact Name",
                    "Emergency Contact Phone",
                    "Anonymous",
                    "Apparel" // new
                ];
                List<object[]> data = [];
                data.AddRange(parts.Select(p => (object[])
                [
                    p.Bib, p.Distance, p.EventSpecific.StatusStr, p.FirstName, p.LastName, p.Birthdate, p.Age(theEvent.Date), p.EventSpecific.AgeGroupName, p.EventSpecific.Division, p.Street, p.Street2, p.City, p.State, p.Zip, p.Country, p.Phone, p.Mobile, p.Email, p.Parent, p.Gender,
                    // Get rid of all the quote and newline characters.
                    p.Comments.Replace('\"', ' ').Replace('\n', ' ').Replace('\r', ' ').Replace('\'', ' '), p.Other.Replace('\"', ' ').Replace('\n', ' ').Replace('\r', ' ').Replace('\'', ' '), p.Owes, p.EcName, p.EcPhone, p.PrettyAnonymous, p.Apparel,
                ]));
                IDataExporter? exporter;
                string extension = Path.GetExtension(file.Name);
                Log.D("UI.MainPages.ParticipantsPage", $"Extension is '{extension}'");
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
                    Log.D("UI.MainPages.ParticipantsPage", $"The format is '{format}'");
                    exporter = new CsvExporter(format.ToString());
                }
                exporter.SetData(headers, data);
                exporter.ExportData(file.TryGetLocalPath()!);
            });
            DialogBox.AsyncShow("File saved.");
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.ParticipantsPage", "Error exporting participants.");
        }
    }

    private void Upload_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.ParticipantsPage", "Upload clicked.");
        if (Upload.Content!.ToString() != "Working")
        {
            Log.D("UI.MainPages.TimingPage", "Uploading data.");
            Upload.Content = "Working";
            UploadParticipants();
            return;
        }
        Log.D("UI.MainPages.ParticipantsPage", "Already uploading.");
    }

    private void Download_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.ParticipantsPage", "Download clicked.");
        if (Download.Content!.ToString() != "Working")
        {
            Log.D("UI.MainPages.TimingPage", "Downloading data.");
            Download.Content = "Working";
            DownloadParticipants();
            return;
        }
        Log.D("UI.MainPages.ParticipantsPage", "Already downloading.");
    }

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.MainPages.ParticipantsPage", "Delete clicked.");
            if (Delete.Content!.ToString() != "Working")
            {
                Log.D("UI.MainPages.ParticipantsPage", "Deleting uploaded participants data.");
                DialogBox.AsyncShow("This will delete all participants loaded to the API and may cause issues. Proceed?",
                    "Yes",
                    "No",
                    async () =>
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                            Delete.Content = "Working";
                        });
                        ApiObject? api = null;
                        try
                        {
                            api = database.GetApi(theEvent!.ApiId);
                            Log.D("UI.MainPages.ParticipantsPage", "API found.");
                        }
                        catch
                        {
                            Log.D("UI.MainPages.ParticipantsPage", "Error finding API.");
                        }
                        // Get the event id values. Exit if not valid.
                        string[] eventIds = theEvent!.ApiEventId.Split(',');
                        // Create a bool for checking if we've grabbed the APIController's lock so we release it later
                        if (eventIds.Length == 2)
                        {
                            try
                            {
                                Log.D("UI.MainPages.ParticipantsPage", "Deleting participants from API.");
                                await ApiHandlers.DeleteParticipants(api!, eventIds[0], eventIds[1]);
                                await ApiHandlers.DeleteBibChips(api!, eventIds[0], eventIds[1]);
                            }
                            catch (ApiException ex)
                            {
                                DialogBox.AsyncShow(ex.Message);
                            }
                        }
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                            Delete.Content = "Delete Uploaded";
                        });
                    });
                return;
            }
            Log.D("UI.MainPages.ParticipantsPage", "Already deleting.");
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.ParticipantsPage", "Error deleting participants.");
        }
    }

    private void ConflictsBtn_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.ParticipantsPage", "Conflicts clicked.");
        ParticipantConflicts conflictWindow = ParticipantConflicts.NewWindow(mWindow, conflicts);
        mWindow.AddWindow(conflictWindow);
        conflictWindow.ShowDialog((Window)mWindow);
    }

    private void DistanceBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Log.D("UI.MainPages.ParticipantsPage", "New Distance selected.");
        UpdateView();
    }

    private void SortBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!loaded) return;
        Log.D("UI.MainPages.ParticipantsPage", "Sort style changed.");
        List<Participant> newParts = [.. allParticipants];
        switch (((ComboBoxItem)SortBox.SelectedItem!).Content)
        {
            case "Name":
                newParts.Sort(Participant.CompareByName);
                break;
            case "Bib":
                newParts.Sort(Participant.CompareByBib);
                break;
            default:
                newParts.Sort();
                break;
        }
        ParticipantsList.SelectedItems.Clear();
        ParticipantsList.ItemsSource = newParts;
        Log.D("UI.MainPages.ParticipantsPage", "Done");
    }

    private void Modify_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.ParticipantsPage", "Modify clicked.");
        List<Participant> selected = [];
        selected.AddRange(ParticipantsList.SelectedItems.Cast<Participant>());
        Log.D("UI.MainPages.ParticipantsPage", $"{selected.Count} participants selected.");
        if (selected.Count > 1)
        {
            ChangeMultiParticipantWindow changeMultiParticipantWindow = new(mWindow, database, selected);
            mWindow.AddWindow(changeMultiParticipantWindow);
            changeMultiParticipantWindow.ShowDialog((Window)mWindow);
            return;
        }
        Participant? part = null;
        foreach (Participant p in selected)
        {
            part = p;
        }
        if (part == null) return;
        ModifyParticipantWindow modifyParticipant = ModifyParticipantWindow.NewWindow(mWindow, database, part);
        mWindow.AddWindow(modifyParticipant);
        modifyParticipant.ShowDialog((Window)mWindow);
    }

    private void Add_Click(object? sender, RoutedEventArgs? e)
    {
        Log.D("UI.MainPages.ParticipantsPage", "Add clicked.");
        ModifyParticipantWindow addParticipant = ModifyParticipantWindow.NewWindow(mWindow, database);
        mWindow.AddWindow(addParticipant);
        addParticipant.ShowDialog((Window)mWindow);
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!loaded) return;
        List<Participant> newList = [.. allParticipants];
        switch (((ComboBoxItem)SortBox.SelectedItem!).Content)
        {
            case "Name":
                newList.Sort(Participant.CompareByName);
                break;
            case "Bib":
                newList.Sort(Participant.CompareByBib);
                break;
            default:
                newList.Sort();
                break;
        }
        string search = SearchBox != null ? SearchBox.Text!.Trim() : "";
        newList.RemoveAll(x => x.IsNotMatch(search));
        ParticipantsList.SelectedItems.Clear();
        ParticipantsList.ItemsSource = newList;
    }

    private void Remove_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.ParticipantsPage", "Remove clicked.");
        IList selected = ParticipantsList.SelectedItems;
        List<Participant> parts = [];
        parts.AddRange(selected.Cast<Participant>());
        database.RemoveParticipantEntries(parts);
        UpdateView();
    }

    private void ParticipantsList_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (ParticipantsList.SelectedItem == null) return;
        ModifyParticipantWindow modifyParticipant = ModifyParticipantWindow.NewWindow(mWindow, database, (Participant)ParticipantsList.SelectedItem);
        mWindow.AddWindow(modifyParticipant);
        modifyParticipant.ShowDialog((Window)mWindow);
    }

    private void UpdateBibStats()
    {
        if (CondenseSwitch == null) { return; }
        Dictionary<int, BibStats> bibStats = [];
        // Checked value is the value after the change.
        if (CondenseSwitch.IsChecked != true)
        {
            Dictionary<int, int> divisionTranslator = [];
            Dictionary<int, string> divisionNames = [];
            foreach (Distance d in database.GetDistances(theEvent!.Identifier))
            {
                divisionTranslator[d.Identifier] = d.LinkedDistance >= 0 ? d.LinkedDistance : d.Identifier;
                if (d.LinkedDistance < 0)
                {
                    divisionNames[d.Identifier] = d.Name;
                }
            }
            foreach (Participant p in allParticipants)
            {
                int divId = divisionTranslator[p.EventSpecific.DistanceIdentifier];
                if (!divisionNames.TryGetValue(divId, out string? name))
                {
                    name = p.Distance;
                }
                if (!bibStats.TryGetValue(divId, out BibStats? bStats))
                {
                    bStats = new BibStats
                    {
                        With = 0,
                        Without = 0,
                        DistanceName = name,
                    };
                    bibStats[divId] = bStats;
                }
                if (p.Bib.Length > 0)
                {
                    bStats.With += 1;
                }
                else
                {
                    bStats.Without += 1;
                }
            }
        }
        else
        {
            foreach (Participant p in allParticipants)
            {
                if (!bibStats.TryGetValue(p.EventSpecific.DistanceIdentifier, out BibStats? bStats))
                {
                    bStats = new BibStats
                    {
                        With = 0,
                        Without = 0,
                        DistanceName = p.Distance,
                    };
                    bibStats[p.EventSpecific.DistanceIdentifier] = bStats;
                }
                if (p.Bib.Length > 0)
                {
                    bStats.With += 1;
                }
                else
                {
                    bStats.Without += 1;
                }
            }
        }
        BibStats totals = new()
        {
            With = 0,
            Without = 0,
            DistanceName = "All"
        };
        List<BibStats> listStats = [];
        foreach (BibStats b in bibStats.Values)
        {
            listStats.Add(b);
            totals.With += b.With;
            totals.Without += b.Without;
        }
        if (bibStats.Values.Count > 1)
        {
            listStats.Insert(0, totals);
            ViewPanel.IsVisible = true;
        }
        else
        {
            ViewPanel.IsVisible = false;
        }
        StatsListView.ItemsSource = listStats;
        StatsExpander.IsVisible = totals.Without > 0;
    }

    private void CondenseSwitch_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        UpdateBibStats();
    }

    private void StatsExpander_PropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (CondenseSwitch == null || StatsExpander == null) { return; }
        CondenseSwitch.IsVisible = StatsExpander.IsExpanded;
    }
}