using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.UI.API.Windows;
using Chronokeep.UI.EventWindows;
using Chronokeep.UI.MainPages.Dashboard;
using Chronokeep.UI.UhfRfidReader;
using Chronokeep.UI.Util;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;

namespace Chronokeep.UI.MainPages;

public partial class DashboardPage : UserControl, IMainPage
{
    private readonly IMainWindow mWindow;
    private readonly IdbInterface database;
    private Event? theEvent;

    private bool loaded = false;

    public DashboardPage(IMainWindow mainWindow, IdbInterface db)
    {
        InitializeComponent();
        mWindow = mainWindow;
        database = db;
        theEvent = database.GetCurrentEvent();
        UpdateView();
    }

    public void UpdateView()
    {
        int oldEventId = theEvent?.Identifier ?? -1;
        theEvent = database.GetCurrentEvent();
        if (theEvent != null && oldEventId != -1 && oldEventId != theEvent.Identifier)
        {
            mWindow.NotifyTimingWorker();
        }
        if (theEvent == null || theEvent.Identifier == -1)
        {
            LeftPanel.IsVisible = false;
            RightPanel.IsVisible = false;
            return;
        }
        LeftPanel.IsVisible = true;
        RightPanel.IsVisible = true;
        EventNameTextBox.Text = theEvent.Name;
        EventYearCodeTextBox.Text = theEvent.YearCode;
        EventDatePicker.SelectedDate = DateTime.Parse(theEvent.Date);
        RankBox.Items.Clear();
        if (theEvent.EventType == Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA)
        {
            RankBox.Items.Add(new ComboBoxItem
            {
                Content = "Elapsed"
            });
            RankBox.Items.Add(new ComboBoxItem
            {
                Content = "Cumulative"
            });
            if (theEvent.RankedBy == RankingType.Clock)
            {
                RankBox.SelectedIndex = 0;
            }
            else
            {
                RankBox.SelectedIndex = 1;
            }
        }
        else
        {
            RankBox.Items.Add(new ComboBoxItem
            {
                Content = "Clock"
            });
            RankBox.Items.Add(new ComboBoxItem
            {
                Content = "Chip"
            });
            RankBox.Items.Add(new ComboBoxItem
            {
                Content = "Mixed"
            });
            RankBox.SelectedIndex = theEvent.RankedBy switch
            {
                RankingType.Chip => 1,
                RankingType.Mixed => 2,
                _ => 0
            };
        }
        CommonAgeCheckBox.IsChecked = theEvent.CommonAgeGroups;
        CommonStartCheckBox.IsChecked = theEvent.CommonStartFinish;
        SegmentCheckBox.IsChecked = theEvent.DistanceSpecificSegments;
        PlacementsCheckBox.IsChecked = theEvent.DisplayPlacements;
        UploadSpecificDistanceResults.IsChecked = theEvent.UploadSpecific;
        if (TypeBox.Items.Count < 1)
        {
            TypeBox.Items.Add(new ComboBoxItem
            {
                Content = "Distance",
                Tag = Constants.Timing.EVENT_TYPE_DISTANCE
            });
            TypeBox.Items.Add(new ComboBoxItem
            {
                Content = "Time",
                Tag = Constants.Timing.EVENT_TYPE_TIME
            });
            TypeBox.Items.Add(new ComboBoxItem
            {
                Content = "Backyard Ultra",
                Tag = Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA
            });
        }
        ComboBoxItem? eventType = null;
        foreach (object? item in TypeBox.Items)
        {
            if (item is ComboBoxItem combo)
            {
                if (combo.Tag != null && (int)combo.Tag == theEvent.EventType)
                {
                    eventType = combo;
                }
            }
        }
        if (eventType != null)
        {
            TypeBox.SelectedItem = eventType;
        }
        else
        {
            TypeBox.SelectedIndex = 0;
        }
        EditButton.Content = Constants.DashboardLabels.EDIT;
        CancelButton.IsVisible = false;
        if (theEvent.ApiId > 0 && theEvent.ApiEventId != "")
        {
            ApiLinkButton.Content = "Event Linked";
        }
        else
        {
            ApiLinkButton.Content = "Link to API Event";
        }
        RegistrationButton.Content = mWindow.IsRegistrationRunning() ? "Stop Registration" : "Start Registration";
        GenderBox.SelectedIndex = theEvent.UseMaleFemale ? 1 : 0;
        loaded = true;
    }

    private void DisableEditableFields()
    {
        EventNameTextBox.IsEnabled = false;
        EventYearCodeTextBox.IsEnabled = false;
        EventDatePicker.IsEnabled = false;
        RankBox.IsEnabled = false;
        CommonAgeCheckBox.IsEnabled = false;
        CommonStartCheckBox.IsEnabled = false;
        SegmentCheckBox.IsEnabled = false;
        PlacementsCheckBox.IsEnabled = false;
        UploadSpecificDistanceResults.IsEnabled = false;
        TypeBox.IsEnabled = false;
        GenderBox.IsEnabled = false;
    }

    private void EnableEditableFields()
    {
        EventNameTextBox.IsEnabled = true;
        EventYearCodeTextBox.IsEnabled = true;
        EventDatePicker.IsEnabled = true;
        RankBox.IsEnabled = true;
        if (TypeBox.SelectedItem != null && (int)((ComboBoxItem)TypeBox.SelectedItem).Tag! == Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA)
        {
            CommonAgeCheckBox.IsEnabled = false;
            SegmentCheckBox.IsEnabled = false;
            CommonStartCheckBox.IsEnabled = false;
        }
        else
        {
            CommonAgeCheckBox.IsEnabled = true;
            SegmentCheckBox.IsEnabled = true;
            CommonStartCheckBox.IsEnabled = true;
        }
        TypeBox.IsEnabled = true;
        PlacementsCheckBox.IsEnabled = true;
        UploadSpecificDistanceResults.IsEnabled = true;
        GenderBox.IsEnabled = true;
    }

    private bool CancelEventChangeAsync(EventClickType clickType)
    {
        Log.D("UI.DashboardPage", "Checking if we need to cancel the change.");
        if (!mWindow.BackgroundProcessesRunning()) return false;
        DialogBox.AsyncShow(
            "There are processes running in the background. Do you wish to stop these and continue?",
            "Yes",
            "No",
            async void () =>
            {
                try
                {
                    mWindow.StopBackgroundProcesses();
                    switch (clickType)
                    {
                        case EventClickType.NewEvent:
                            NewEventWindow newEventWindow = NewEventWindow.NewWindow(mWindow, database);
                            mWindow.AddWindow(newEventWindow);
                            _ = newEventWindow.ShowDialog((Window)mWindow);
                            break;
                        case EventClickType.ImportEvent:
                            TopLevel? topLevel = TopLevel.GetTopLevel(this);
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
                                IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                                {
                                    FileTypeFilter = [Utils.SqLiteType, FilePickerFileTypes.All],
                                    AllowMultiple = false,
                                    SuggestedStartLocation = startingFolder,
                                });
                                if (files.Count > 0)
                                {
                                    SqLiteInterface savedDatabase = new(files[0].TryGetLocalPath()!);
                                    savedDatabase.Initialize();
                                    List<Event> events = savedDatabase.GetEvents();
                                    int lastId = -1;
                                    foreach (int tmp in events.Select(ev => Save_Event(ev, savedDatabase, database)).Where(tmp => tmp > 0))
                                    {
                                        lastId = tmp;
                                    }
                                    database.SetCurrentEvent(lastId);
                                    UpdateView();
                                    mWindow.UpdateStatus();
                                }
                            }
                            break;
                        case EventClickType.ChangeEvent:
                            ChangeEventWindow changeEventWindow = ChangeEventWindow.NewWindow(mWindow, database);
                            mWindow.AddWindow(changeEventWindow);
                            _ = changeEventWindow.ShowDialog((Window)mWindow);
                            break;
                        case EventClickType.DeleteEvent:
                            try
                            {
                                Log.D("UI.DashboardPage", "Attempting to delete.");
                                DialogBox.AsyncShow(
                                    "Are you sure you want to delete this event? This cannot be undone.",
                                    "Yes",
                                    "No",
                                    () =>
                                    {
                                        database.RemoveEvent(theEvent!.Identifier);
                                        database.SetCurrentEvent(-1);
                                        mWindow.WindowFinalize();
                                    }
                                );
                            }
                            catch
                            {
                                Log.D("UI.DashboardPage", "Unable to remove the event.");
                                DialogBox.AsyncShow("Unable to remove the event.");
                            }
                            UpdateView();
                            mWindow.UpdateStatus();
                            break;
                    }
                }
                catch (Exception)
                {
                    Log.D("UI.DashboardPage", "Unable to remove the event.");
                }
            }
        );
        return true;
    }

    private enum EventClickType
    {
        NewEvent,
        ImportEvent,
        ChangeEvent,
        DeleteEvent,
    }

    private static int Save_Event(Event oldEvent, IdbInterface loadFrom, IdbInterface saveTo)
    {
        // Make some modifications, note that we cannot guarantee API compatibility between events.
        Event newEvent = new();
        newEvent.CopyAll(oldEvent);
        newEvent.ApiEventId = Constants.ApiConstants.NULL_EVENT_ID;
        newEvent.ApiId = Constants.ApiConstants.NULL_ID;
        newEvent.Identifier = -1;
        saveTo.AddEvent(newEvent);
        newEvent.Identifier = saveTo.GetEventId(newEvent);
        // Only proceed if we managed to add the event, or we can find it.
        if (newEvent.Identifier <= 0) return newEvent.Identifier;
        // Get all the parts that don't depend on other parts, then parts that do.
        // Order of operation matters here.
        // Bib chip associations do not have any linked ID's.
        Log.D("UI.DashboardPage", "Adding bib chip associations.");
        List<BibChipAssociation> bibChipAssociations = loadFrom.GetBibChips(oldEvent.Identifier);
        saveTo.AddBibChipAssociation(newEvent.Identifier, bibChipAssociations);
        // Distances can link to themselves. DistanceID is also used by EVENT_SPECIFIC, SEGMENTS, and AGE_GROUPS
        Log.D("UI.DashboardPage", "Adding distances.");
        Dictionary<int, int> distanceIdTranslation = [];
        Dictionary<string, int> oldDistanceIdDictionary = [];
        List<Distance> normalDistances = [];
        List<Distance> linkedDistances = [];
        foreach (Distance item in loadFrom.GetDistances(oldEvent.Identifier))
        {
            // Set event identifier to new event id.
            item.EventIdentifier = newEvent.Identifier;
            // Check if it's a linked distance and place it in the correct list.
            if (item.LinkedDistance == Constants.Timing.DISTANCE_NO_LINKED_ID)
            {
                normalDistances.Add(item);
            }
            else
            {
                linkedDistances.Add(item);
            }
            // Set it so we can get the old ID by the name of the distance.
            oldDistanceIdDictionary[item.Name] = item.Identifier;
        }
        // Insert the old distances
        saveTo.AddDistances(normalDistances);
        // Loop through all the distances we just added and update our dictionary with their new ids.
        foreach (Distance item in saveTo.GetDistances(newEvent.Identifier))
        {
            if (oldDistanceIdDictionary.TryGetValue(item.Name, out int oldDistId))
            {
                distanceIdTranslation[oldDistId] = item.Identifier;
            }
        }
        // Update linked distances to their new division ID or set it to no linked if we can't find it.
        foreach (Distance item in linkedDistances)
        {
            item.LinkedDistance = distanceIdTranslation.GetValueOrDefault(item.LinkedDistance, Constants.Timing.DISTANCE_NO_LINKED_ID);
        }
        saveTo.AddDistances(linkedDistances);
        // Age groups rely only on the event, and the distance.
        // Age group id is used by EVENT_SPECIFIC
        Log.D("UI.DashboardPage", "Adding age groups.");
        List<AgeGroup> ageGroups = [];
        Dictionary<int, int> ageGroupIdTranslation = [];
        // Key is START AGE
        Dictionary<int, int> oldAgeGroupDictionary = [];
        foreach (AgeGroup item in loadFrom.GetAgeGroups(oldEvent.Identifier))
        {
            item.EventId = newEvent.Identifier;
            oldAgeGroupDictionary[item.StartAge] = item.GroupId;
            // Add the item to our list to save IFF it has a common DistanceID set to it.
            if (item.DistanceId != Constants.Timing.COMMON_AGEGROUPS_DISTANCEID)
            {
                if (!distanceIdTranslation.TryGetValue(item.DistanceId, out int oDistId)) continue;
                item.DistanceId = oDistId;
            }
            ageGroups.Add(item);
        }
        saveTo.AddAgeGroups(ageGroups);
        foreach (AgeGroup item in saveTo.GetAgeGroups(newEvent.Identifier))
        {
            if (oldAgeGroupDictionary.TryGetValue(item.StartAge, out int oAgId))
            {
                ageGroupIdTranslation[oAgId] = item.GroupId;
            }
        }
        // Locations are relied upon by SEGMENTS, CHIP_READS, and TIME_RESULTS
        Log.D("UI.DashboardPage", "Adding locations.");
        List<TimingLocation> locations = loadFrom.GetTimingLocations(oldEvent.Identifier);
        Dictionary<int, int> locationIdTranslation = [];
        Dictionary<string, int> oldLocationDictionary = [];
        foreach (TimingLocation item in locations)
        {
            item.EventIdentifier = newEvent.Identifier;
            oldLocationDictionary[item.Name] = item.Identifier;
        }
        saveTo.AddTimingLocations(locations);
        // Update the location translation dictionary with oldID key and new id value.
        foreach (TimingLocation item in saveTo.GetTimingLocations(newEvent.Identifier))
        {
            if (oldLocationDictionary.TryGetValue(item.Name, out int oLocId))
            {
                locationIdTranslation[oLocId] = item.Identifier;
            }
        }
        locationIdTranslation[Constants.Timing.LOCATION_FINISH] = Constants.Timing.LOCATION_FINISH;
        locationIdTranslation[Constants.Timing.LOCATION_START] = Constants.Timing.LOCATION_START;
        locationIdTranslation[Constants.Timing.LOCATION_ANNOUNCER] = Constants.Timing.LOCATION_ANNOUNCER;
        locationIdTranslation[Constants.Timing.LOCATION_DUMMY] = Constants.Timing.LOCATION_DUMMY;
        // Segments rely on Locations and Distances
        // Segment ids are used by TIME_RESULTS
        Log.D("UI.DashboardPage", "Adding segments");
        List<Segment> segments = [];
        Dictionary<int, int> segmentIdTranslator = [];
        // key here is DISTANCE_ID, LOCATION_ID, OCCURRENCE (new values)
        Dictionary<(int, int, int), int> oldSegmentDictionary = [];
        foreach (Segment item in loadFrom.GetSegments(oldEvent.Identifier))
        {
            item.EventId = newEvent.Identifier;
            // only insert segments when there were no issues with the distance and location translations
            // Make sure to check if we're using common segments.
            if (item.DistanceId == Constants.Timing.COMMON_SEGMENTS_DISTANCEID)
            {
                if (!locationIdTranslation.TryGetValue(item.LocationId, out int tLocIt)) continue;
                item.LocationId = tLocIt;
            }
            else
            {
                if (!distanceIdTranslation.TryGetValue(item.DistanceId, out int tDistId) ||
                    !locationIdTranslation.TryGetValue(item.LocationId, out int yLocId)) continue;
                item.DistanceId = tDistId;
                item.LocationId = yLocId;
            }
            oldSegmentDictionary[(item.DistanceId, item.LocationId, item.Occurrence)] = item.Identifier;
            segments.Add(item);
        }
        saveTo.AddSegments(segments);
        // Update our segmentIDTranslator
        foreach (Segment item in saveTo.GetSegments(newEvent.Identifier))
        {
            if (oldSegmentDictionary.TryGetValue((item.DistanceId, item.LocationId, item.Occurrence), out int oSegId))
            {
                segmentIdTranslator[oSegId] = item.Identifier;
            }
        }
        segmentIdTranslator[Constants.Timing.SEGMENT_FINISH] = Constants.Timing.SEGMENT_FINISH;
        segmentIdTranslator[Constants.Timing.SEGMENT_START] = Constants.Timing.SEGMENT_START;
        segmentIdTranslator[Constants.Timing.SEGMENT_NONE] = Constants.Timing.SEGMENT_NONE;
        // Participants contain EVENT_SPECIFIC which relies on distance and age groups.
        // EventSpecific ID is used by TIME_RESULT
        Log.D("UI.DashboardPage", "Adding participants.");
        List<Participant> participants = [];
        Dictionary<int, int> eventSpecificIdTranslation = [];
        // Bib is the key here
        Dictionary<string, int> oldEventSpecificDictionary = [];
        foreach (Participant item in loadFrom.GetParticipants(oldEvent.Identifier))
        {
            item.EventSpecific.EventIdentifier = newEvent.Identifier;
            oldEventSpecificDictionary[item.EventSpecific.Bib] = item.EventSpecific.Identifier;
            // Only add the participant if we can translate their distance identifier.
            if (!distanceIdTranslation.TryGetValue(item.EventSpecific.DistanceIdentifier, out int oDistId)) continue;
            item.EventSpecific.DistanceIdentifier = oDistId;
            item.EventSpecific.AgeGroupId = ageGroupIdTranslation.GetValueOrDefault(item.EventSpecific.AgeGroupId, Constants.Timing.TIMERESULT_DUMMYAGEGROUP);
            participants.Add(item);
        }
        saveTo.AddParticipants(participants);
        // Translate old ID's to new ID's
        foreach (Participant item in saveTo.GetParticipants(newEvent.Identifier))
        {
            if (oldEventSpecificDictionary.TryGetValue(item.Bib, out int oEsId))
            {
                eventSpecificIdTranslation[oEsId] = item.EventSpecific.Identifier;
            }
        }
        // ChipReads depend on location_id.
        Log.D("UI.DashboardPage", "Adding ChipReads.");
        List<ChipRead> chipReads = [];
        Dictionary<int, int> readIdTranslation = [];
        // (CHIP_NUMBER, BIB, SECONDS, MILLISECONDS) for the key
        Dictionary<(string, string, long, int), int> oldReadDictionary = [];
        foreach (ChipRead item in loadFrom.GetChipReads(oldEvent.Identifier))
        {
            item.EventId = newEvent.Identifier;
            oldReadDictionary[(item.ChipNumber, item.Bib, item.Seconds, item.Milliseconds)] = item.ReadId;
            // If the location is not a pre-set location, i.e. a custom location
            if (item.LocationId != Constants.Timing.LOCATION_START && item.LocationId != Constants.Timing.LOCATION_FINISH && item.LocationId != Constants.Timing.LOCATION_ANNOUNCER)
            {
                if (!locationIdTranslation.TryGetValue(item.LocationId, out int oLocId)) continue;
                item.LocationId = oLocId;
            }
            // this is a known location (start, finish, or announce)
            chipReads.Add(item);
        }
        saveTo.AddChipReads(chipReads);
        foreach (ChipRead item in saveTo.GetChipReads(newEvent.Identifier))
        {
            if (oldReadDictionary.TryGetValue((item.ChipNumber, item.Bib, item.Seconds, item.Milliseconds), out int oReadId))
            {
                readIdTranslation[oReadId] = item.ReadId;
            }
        }
        // Results rely upon read_id, location_id, and segment_id.
        Log.D("UI.DashboardPage", "Adding results.");
        List<TimeResult> results = [];
        foreach (TimeResult item in loadFrom.GetTimingResults(oldEvent.Identifier))
        {
            item.EventIdentifier = newEvent.Identifier;
            if (!readIdTranslation.TryGetValue(item.ReadId, out int tReadId) || !locationIdTranslation.TryGetValue(
                                                                                 item.LocationId, out int tLocId)
                                                                             || !segmentIdTranslator.TryGetValue(
                                                                                 item.SegmentId, out int tSegId) ||
                                                                             !eventSpecificIdTranslation.TryGetValue(
                                                                                 item.EventSpecificId, out int tEsId))
                continue;
            item.ReadId = tReadId;
            item.LocationId = tLocId;
            item.SegmentId = tSegId;
            item.EventSpecificId = tEsId;
            results.Add(item);
        }
        saveTo.AddTimingResults(results);
        return newEvent.Identifier;
    }

    public static void UpdateDatabase() { }

    public void KeyboardCtrlA() { }

    public void KeyboardCtrlS() { }

    public void KeyboardCtrlZ() { }

    public void Closing()
    {
        if (database.GetAppSetting(Constants.Settings.UPDATE_ON_PAGE_CHANGE)!.Value == Constants.Settings.SETTING_TRUE)
        {
            UpdateDatabase();
        }
    }

    private void NewEvent_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.DashboardPage", "New event clicked.");
        if (CancelEventChangeAsync(EventClickType.NewEvent))
        {
            return;
        }
        NewEventWindow newEventWindow = NewEventWindow.NewWindow(mWindow, database);
        mWindow.AddWindow(newEventWindow);
        newEventWindow.ShowDialog((Window)mWindow);
    }

    private void ChangeEvent_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.DashboardPage", "Change event clicked.");
        if (CancelEventChangeAsync(EventClickType.ChangeEvent))
        {
            return;
        }
        ChangeEventWindow changeEventWindow = ChangeEventWindow.NewWindow(mWindow, database);
        mWindow.AddWindow(changeEventWindow);
        changeEventWindow.ShowDialog((Window)mWindow);
    }

    private async void SaveEvent_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.DashboardPage", "Saving event.");
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
                FileTypeChoices = [Utils.SqLiteType],
                SuggestedFileName = $"{theEvent!.YearCode} {theEvent.Name}.sqlite",
                SuggestedStartLocation = startingFolder,
            });
            if (file is null) return;
            Log.D("UI.DashboardPage", "Creating database file.");
            string? filePath = file.TryGetLocalPath();
            try
            {
                SQLiteConnection.CreateFile(filePath);
            }
            catch
            {
                DialogBox.AsyncShow("Unable to save to file");
                return;
            }
            if (filePath == null)
            {
                return;
            }
            SqLiteInterface savedDatabase = new(filePath);
            savedDatabase.Initialize();
            Event currentEvent = database.GetCurrentEvent()!;
            Save_Event(currentEvent, database, savedDatabase);
            Log.D("UI.DashboardPage", "Done saving file.");
            DialogBox.AsyncShow("Event saved successfully.");
        }
        catch (Exception)
        {
            Log.D("UI.DashboardPage", "Error saving file.");
        }
    }

    private async void ImportEvent_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.DashboardPage", "Import event clicked.");
            if (CancelEventChangeAsync(EventClickType.ImportEvent))
            {
                return;
            }
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
                FileTypeFilter = [Utils.SqLiteType, FilePickerFileTypes.All],
                AllowMultiple = false,
                SuggestedStartLocation = startingFolder,
            });
            if (files.Count <= 0) return;
            SqLiteInterface savedDatabase = new(files[0].TryGetLocalPath()!);
            savedDatabase.Initialize();
            List<Event> events = savedDatabase.GetEvents();
            int lastId = -1;
            foreach (int tmp in events.Select(ev => Save_Event(ev, savedDatabase, database)).Where(tmp => tmp > 0))
            {
                lastId = tmp;
            }
            database.SetCurrentEvent(lastId);
            UpdateView();
            mWindow.UpdateStatus();
        }
        catch (Exception)
        {
            Log.D("UI.DashboardPage", "Error importing.");
        }
    }

    private void DeleteEvent_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.DashboardPage", "Delete event clicked.");
        if (CancelEventChangeAsync(EventClickType.DeleteEvent))
        {
            return;
        }
        try
        {
            Log.D("UI.DashboardPage", "Attempting to delete.");
            DialogBox.AsyncShow(
                "Are you sure you want to delete this event? This cannot be undone.",
                "Yes",
                "No",
                () =>
                {
                    database.RemoveEvent(theEvent!.Identifier);
                    database.SetCurrentEvent(-1);
                    mWindow.WindowFinalize();
                }
                );
        }
        catch
        {
            Log.D("UI.DashboardPage", "Unable to remove the event.");
            DialogBox.AsyncShow("Unable to remove the event.");
        }
        UpdateView();
        mWindow.UpdateStatus();
    }

    private void TypeBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Log.D("UI.DashboardPage", "TypeBox selection changed.");
        if (theEvent == null) { return; }
        if (!loaded) { return; }
        int eventType = 0;
        try
        {
            eventType = TypeBox.SelectedIndex;
        }
        catch
        {
            CommonAgeCheckBox.IsEnabled = true;
            SegmentCheckBox.IsEnabled = true;
            CommonStartCheckBox.IsEnabled = true;
        }
        // Common age groups when backyard ultra is the event type.
        RankBox.Items.Clear();
        if (eventType == Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA)
        {
            CommonAgeCheckBox.IsEnabled = false;
            CommonAgeCheckBox.IsChecked = true;
            SegmentCheckBox.IsEnabled = false;
            SegmentCheckBox.IsChecked = false;
            CommonStartCheckBox.IsEnabled = false;
            CommonStartCheckBox.IsChecked = true;
            RankBox.Items.Add(new ComboBoxItem
            {
                Content = "Elapsed"
            });
            RankBox.Items.Add(new ComboBoxItem
            {
                Content = "Cumulative"
            });
            if (theEvent.RankedBy == RankingType.Clock)
            {
                RankBox.SelectedIndex = 0;
            }
            else
            {
                RankBox.SelectedIndex = 1;
            }
        }
        else if (EditButton != null && EditButton.Content!.ToString() == Constants.DashboardLabels.SAVE)
        {
            CommonAgeCheckBox.IsEnabled = true;
            SegmentCheckBox.IsEnabled = true;
            CommonStartCheckBox.IsEnabled = true;
            RankBox.Items.Add(new ComboBoxItem
            {
                Content = "Clock"
            });
            RankBox.Items.Add(new ComboBoxItem
            {
                Content = "Chip"
            });
            RankBox.Items.Add(new ComboBoxItem
            {
                Content = "Mixed"
            });
            RankBox.SelectedIndex = theEvent.RankedBy switch
            {
                RankingType.Chip => 1,
                RankingType.Mixed => 2,
                _ => 0
            };
        }
        else
        {
            RankBox.Items.Add(new ComboBoxItem
            {
                Content = "Clock"
            });
            RankBox.Items.Add(new ComboBoxItem
            {
                Content = "Chip"
            });
            RankBox.Items.Add(new ComboBoxItem
            {
                Content = "Mixed"
            });
            RankBox.SelectedIndex = theEvent.RankedBy switch
            {
                RankingType.Chip => 1,
                RankingType.Mixed => 2,
                _ => 0
            };
        }
    }

    private void EditButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.DashboardPage", "Edit Button Clicked.");
        if (EditButton.Content!.ToString() == Constants.DashboardLabels.EDIT)
        {
            Log.D("UI.DashboardPage", "Editing.");
            EditButton.Content = Constants.DashboardLabels.WORKING;
            EnableEditableFields();
            EditButton.Content = Constants.DashboardLabels.SAVE;
            CancelButton.IsVisible = true;
        }
        else if (EditButton.Content.ToString() == Constants.DashboardLabels.SAVE)
        {
            Log.D("UI.DashboardPage", "Saving");
            EditButton.Content = Constants.DashboardLabels.WORKING;
            DisableEditableFields();
            // If distance specific segments are being enabled/disabled then reset all segments
            // so no residual segments stay around.
            if (theEvent!.DistanceSpecificSegments != SegmentCheckBox.IsChecked)
            {
                database.ResetSegments(theEvent.Identifier);
            }
            theEvent.Name = EventNameTextBox.Text!;
            theEvent.YearCode = EventYearCodeTextBox.Text!;
            theEvent.Date = EventDatePicker.SelectedDate?.ToString("M/d/yyyy") ?? DateTime.Now.ToString("M/d/yyyy");
            theEvent.RankedBy = RankBox.SelectedIndex switch
            {
                1 => RankingType.Chip,
                2 => RankingType.Mixed,
                _ => RankingType.Clock
            };
            theEvent.CommonAgeGroups = CommonAgeCheckBox.IsChecked ?? false;
            theEvent.CommonStartFinish = CommonStartCheckBox.IsChecked ?? false;
            theEvent.DistanceSpecificSegments = SegmentCheckBox.IsChecked ?? false;
            theEvent.DisplayPlacements = PlacementsCheckBox.IsChecked ?? true;
            theEvent.UploadSpecific = UploadSpecificDistanceResults.IsChecked ?? false;
            bool useMaleFemale = GenderBox.SelectedIndex == 1;
            if (theEvent.UseMaleFemale != useMaleFemale)
            {
                List<Participant> participants = database.GetParticipants(theEvent.Identifier);
                foreach (Participant part in participants)
                {
                    part.FormatData(useMaleFemale);
                }
                database.UpdateParticipants(participants);
            }
            theEvent.UseMaleFemale = useMaleFemale;
            try
            {
                theEvent.EventType = (int)((ComboBoxItem)TypeBox.SelectedItem!).Tag!;
            }
            catch
            {
                theEvent.EventType = Constants.Timing.EVENT_TYPE_DISTANCE;
            }
            Log.D("UI.DashboardPage", "Updating database.");
            // Check if we've changed the segment option
            Event oldEvent = database.GetCurrentEvent()!;
            if (oldEvent.DistanceSpecificSegments != theEvent.DistanceSpecificSegments)
            {
                Log.D("UI.DashboardPage", "Distance Specific Segments value has changed.");
                database.ResetSegments(theEvent.Identifier);
            }
            if (oldEvent.CommonAgeGroups != theEvent.CommonAgeGroups)
            {
                Log.D("UI.DashboardPage", "Common Age Groups value has changed.");
                database.ResetAgeGroups(theEvent.Identifier);
            }
            if (oldEvent.DivisionsEnabled != theEvent.DivisionsEnabled)
            {
                Log.D("UI.DashboardPage", "Divisions Enabled value has changed.");
                database.UpdateDivisionsEnabled();
            }
            database.UpdateEvent(theEvent);
            Log.D("UI.DashboardPage", "Updating view.");
            mWindow.NotifyTimingWorker();
            UpdateView();
        }
        else
        {
            Log.D("UI.DashboardPage", "Crying.");
        }
    }

    private void ApiPageButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.DashboardPage", "Results API button clicked.");
        mWindow.SwitchPage(new ApiPage(mWindow, database));
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.DashboardPage", "Cancel clicked.");
        DisableEditableFields();
        UpdateView();
        EditButton.Content = Constants.DashboardLabels.EDIT;
        CancelButton.IsVisible = false;
    }

    private void ApiLinkButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.DashboardPage", "Link/Edit API Event.");
        if (theEvent!.ApiId > 0 && theEvent.ApiEventId != "")
        {
            EditApiWindow editWindow = EditApiWindow.NewWindow(mWindow, database);
            mWindow.AddWindow(editWindow);
            editWindow.ShowDialog((Window)mWindow);
        }
        else
        {
            ApiWindow apiWindow = ApiWindow.NewWindow(mWindow, database);
            mWindow.AddWindow(apiWindow);
            apiWindow.ShowDialog((Window)mWindow);
        }
    }

    private void TagTesterButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.DashboardPage", "Tag Tester clicked.");
        ChipReaderWindow crWindow = ChipReaderWindow.NewWindow(mWindow, database);
        mWindow.AddWindow(crWindow);
        crWindow.Show();
    }

    private void RegistrationButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.MainPages.DashboardPage", "Registration button clicked.");
        if (mWindow.IsRegistrationRunning())
        {
            mWindow.StopRegistration();
            RegistrationButton.Content = "Start Registration";
        }
        else
        {
            mWindow.StartRegistration();
            RegistrationButton.Content = "Stop Registration";
        }
    }
}