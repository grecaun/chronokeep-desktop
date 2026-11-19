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
using Chronokeep.Network.Runsignup;
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
    // The event entries (EventSpecific identifiers) in a conflict, so their rows can be tinted.
    private readonly HashSet<int> conflictEntries = [];

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
            RunsignupPanel.IsVisible = theEvent.IsRegistrationLinked;
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
            if (theEvent.ApiId > 0 && theEvent.ApiEventId.Length > 1)
            {
                ApiPanel.IsVisible = true;
            }
            else
            {
                ApiPanel.IsVisible = false;
            }
            // Conflicts are found before the list is shown so their rows can be tinted.
            conflicts.Clear();
            HashSet<(int, int)> conflictParticipantIdentifiers = [];
            // Removed entries (an old transfer, a changed bib) aren't conflicts with the people who replaced them.
            List<Participant> inRace = [.. allParticipants.Where(p => !p.EventSpecific.IsRemoved)];
            foreach (Participant outer in inRace)
            {
                foreach (Participant inner in inRace)
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
            // By entry rather than object: filtering by distance loads its own copies of each participant.
            conflictEntries.Clear();
            conflictEntries.UnionWith(conflicts.Select(p => p.EventSpecific.Identifier));
            ParticipantsList.ItemsSource = newList;
            if (conflicts.Count > 0)
            {
                ConflictsBtn.Content = $"Conflicts - {conflicts.Count / 2}";
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

    /// <summary>Downloads from Chronokeep Results, showing the page on the button and the total under it.</summary>
    internal async Task DownloadParticipants()
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
                    Download.Content = $"Page {page}";
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
                // Merging and saving runs off the UI thread so the window stays usable.
                Event downloadEvent = theEvent;
                await Task.Run(() => ChronokeepParticipantSource.Download(database, downloadEvent, newPersons));
                ApiStatus.Text = $"{newPersons.Count} participants downloaded";
                ApiStatus.IsVisible = true;
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
        catch (Exception e)
        {
            Log.D("UI.MainPages.ParticipantsPage", $"Error downloading participants. {e}");
        }
    }

    /// <summary>Uploads to Chronokeep Results, showing how many have gone on the button and the total under it.</summary>
    internal async Task UploadParticipants()
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
            // Get results to upload. People removed in Runsignup aren't in the race, so they aren't sent.
            List<Participant> participants = [.. database.GetParticipants(theEvent.Identifier).Where(p => !p.EventSpecific.IsRemoved)];
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
                    Upload.Content = $"{i * Constants.Timing.API_LOOP_COUNT} / {upParticipants.Count}";
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
                    Upload.Content = $"{loops * Constants.Timing.API_LOOP_COUNT} / {upParticipants.Count}";
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
                    Upload.Content = $"Chips {i * Constants.Timing.API_LOOP_COUNT} / {upBibChips.Count}";
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
                    Upload.Content = $"Chips {loops * Constants.Timing.API_LOOP_COUNT} / {upBibChips.Count}";
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
            ApiStatus.Text = $"{upParticipants.Count} participants uploaded";
            ApiStatus.IsVisible = true;
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
            string path = file.TryGetLocalPath()!;
            await Task.Run(() => WriteParticipants(database, theEvent, path));
            DialogBox.AsyncShow("File saved.");
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.ParticipantsPage", "Error exporting participants.");
        }
    }

    /// <summary>Writes every participant in the event to an Excel file, or a CSV file when the path doesn't end in .xls(x).</summary>
    internal static void WriteParticipants(IdbInterface database, Event theEvent, string path)
    {
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
        string extension = Path.GetExtension(path);
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
        exporter.ExportData(path);
    }

    // Set while a Chronokeep upload or download runs, since the buttons show progress rather than a fixed label.
    private bool uploading, downloading;

    private async void Upload_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.ParticipantsPage", "Upload clicked.");
        if (uploading)
        {
            Log.D("UI.MainPages.ParticipantsPage", "Already uploading.");
            return;
        }
        uploading = true;
        try
        {
            await UploadParticipants();
        }
        finally
        {
            uploading = false;
            Upload.Content = "Upload";
        }
    }

    private async void Download_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.ParticipantsPage", "Download clicked.");
        if (downloading)
        {
            Log.D("UI.MainPages.ParticipantsPage", "Already downloading.");
            return;
        }
        downloading = true;
        try
        {
            await DownloadParticipants();
        }
        finally
        {
            downloading = false;
            Download.Content = "Download";
        }
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
                    async () => await DeleteUploaded());
                return;
            }
            Log.D("UI.MainPages.ParticipantsPage", "Already deleting.");
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.ParticipantsPage", "Error deleting participants.");
        }
    }

    /// <summary>Deletes the event's participants and bib/chip pairs from the linked results API.</summary>
    internal async Task DeleteUploaded()
    {
        Delete.Content = "Working";
        try
        {
            ApiObject? api = null;
            try
            {
                api = database.GetApi(theEvent!.ApiId);
            }
            catch
            {
                Log.D("UI.MainPages.ParticipantsPage", "Error finding API.");
            }
            // Get the event id values. Exit if not valid or the API is gone.
            string[] eventIds = theEvent!.ApiEventId.Split(',');
            if (eventIds.Length == 2 && api != null)
            {
                await ApiHandlers.DeleteParticipants(api, eventIds[0], eventIds[1]);
                await ApiHandlers.DeleteBibChips(api, eventIds[0], eventIds[1]);
            }
        }
        catch (ApiException ex)
        {
            DialogBox.AsyncShow(ex.Message);
        }
        catch (Exception ex)
        {
            Log.E("UI.MainPages.ParticipantsPage", $"Error deleting uploaded participants: {ex.Message}");
            DialogBox.AsyncShow("Unable to delete the uploaded participants.");
        }
        finally
        {
            Delete.Content = "Delete Uploaded";
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
        // UpdateView applies the distance filter, the sort and the search together; sorting the whole list
        // here showed every distance even while one was selected.
        UpdateView();
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
        // Search within the selected distance, with the chosen sort, by going through UpdateView.
        UpdateView();
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
                CountBib(bStats, p);
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
                CountBib(bStats, p);
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
            totals.Removed += b.Removed;
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
        StatsExpander.IsVisible = totals.Without > 0 || totals.Removed > 0;
        StatsListView.Columns[3].IsVisible = totals.Removed > 0;
    }

    /// <summary>
    /// Counts a participant's bib. People removed in Runsignup only count when they still hold a bib,
    /// in Removed, since that bib may need reclaiming or watching for.
    /// </summary>
    private static void CountBib(BibStats stats, Participant p)
    {
        if (p.EventSpecific.IsRemoved)
        {
            if (p.Bib.Length > 0) stats.Removed += 1;
        }
        else if (p.Bib.Length > 0)
        {
            stats.With += 1;
        }
        else
        {
            stats.Without += 1;
        }
    }

    /// <summary>
    /// Tints people removed in Runsignup and says why, as the timing page does for their results, and tints
    /// entries in a conflict. Removed people are never counted as conflicts.
    /// </summary>
    private void ParticipantsList_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        Participant? person = e.Row.DataContext as Participant;
        bool removed = person is { EventSpecific.IsRemoved: true };
        bool conflict = !removed && person != null && conflictEntries.Contains(person.EventSpecific.Identifier);
        e.Row.Classes.Set("removed", removed);
        e.Row.Classes.Set("conflict", conflict);
        ToolTip.SetTip(e.Row, removed ? $"Removed in Runsignup: {person!.RemovedReason}"
            : conflict ? "This entry conflicts with another. Use the Conflicts button to sort it out."
            : null);
    }

    private async void RunsignupDownload_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            await DownloadFromRunsignup();
        }
        catch (Exception ex)
        {
            Log.E("UI.MainPages.ParticipantsPage", $"Error downloading from Runsignup: {ex.Message}");
        }
    }

    private bool downloadingRunsignup;

    /// <summary>
    /// Downloads the linked Runsignup race. The button shows each step ("Page 2"), and the total
    /// shows under it afterwards, with what changed in its tooltip.
    /// </summary>
    internal async Task DownloadFromRunsignup()
    {
        if (theEvent == null || !theEvent.IsRegistrationLinked || downloadingRunsignup) return;
        downloadingRunsignup = true;
        RunsignupDownload.Content = "Race";
        try
        {
            // The download is async and does its saving off the UI thread, so the window stays usable.
            // Steps can be reported from other threads, so they come back through the dispatcher.
            RsuDownloadSummary summary = await Network.Runsignup.RunsignupDownload.Download(database, theEvent,
                step => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (downloadingRunsignup) RunsignupDownload.Content = step;
                }));
            mWindow.NotifyTimingWorker();
            RunsignupStatus.Text = $"{summary.Downloaded} participants downloaded";
            ToolTip.SetTip(RunsignupStatus, summary.Describe());
            RunsignupStatus.IsVisible = true;
        }
        catch (ApiException ex)
        {
            DialogBox.Show(ex.Message);
        }
        finally
        {
            downloadingRunsignup = false;
            RunsignupDownload.Content = "Download";
        }
        UpdateDistancesBox();
        UpdateView();
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
