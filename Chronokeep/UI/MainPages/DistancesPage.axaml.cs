using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Database;
using Chronokeep.Database.SQLite;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Network.API;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronoKeepAPI;
using Chronokeep.UI.Parts;
using Chronokeep.UI.Util;
using System.Collections.Generic;
using System.Linq;

namespace Chronokeep.UI.MainPages;

public partial class DistancesPage : UserControl, IMainPage
{
    private readonly IMainWindow mWindow;
    private readonly IdbInterface database;
    private readonly Event? theEvent;
    private readonly Dictionary<int, Distance> distanceDictionary = [];
    private readonly Dictionary<int, List<Distance>> subDistanceDictionary = [];
    private readonly HashSet<int> distancesChanged = [];
    private List<Distance>? distances;
    private bool updateTimingWorker;
    private int distanceCount = 1;

    public DistancesPage(IMainWindow mWindow, IdbInterface database)
    {
        InitializeComponent();
        this.mWindow = mWindow;
        this.database = database;
        theEvent = database.GetCurrentEvent();
        if (theEvent!.ApiId > 0 && theEvent.ApiEventId.Length > 1)
        {
            ApiPanel.IsVisible = true;
        }
        else
        {
            ApiPanel.IsVisible = false;
        }
        UpdateView();
    }

    public void UpdateView()
    {
        if (theEvent == null || theEvent.Identifier < 0)
        {
            return;
        }
        DistancesBox.Items.Clear();
        distances = database.GetDistances(theEvent.Identifier);
        distanceCount = 1;
        distances.Sort();
        distanceDictionary.Clear();
        subDistanceDictionary.Clear();
        List<Distance> superDivs = [];
        foreach (Distance div in distances)
        {
            // Check if we're a linked distance
            if (div.LinkedDistance > 0)
            {
                if (!subDistanceDictionary.TryGetValue(div.LinkedDistance, out List<Distance>? oSubDistList))
                {
                    oSubDistList = [];
                    subDistanceDictionary[div.LinkedDistance] = oSubDistList;
                }
                oSubDistList.Add(div);
            }
            else
            {
                superDivs.Add(div);
            }
        }
        foreach (Distance div in superDivs)
        {
            distanceDictionary[div.Identifier] = div;
            DistancePart parent = new(this, div, theEvent.FinishMaxOccurrences, distances, distanceDictionary, theEvent, null);
            DistancesBox.Items.Add(parent);
            distanceCount = div.Identifier > distanceCount - 1 ? div.Identifier + 1 : distanceCount;
            // Add linked distances
            if (subDistanceDictionary.TryGetValue(div.Identifier, out List<Distance>? tSubDistList))
            {
                foreach (Distance sub in tSubDistList)
                {
                    DistancesBox.Items.Add(new DistancePart(this, sub, theEvent.FinishMaxOccurrences, distances, distanceDictionary, theEvent, parent));
                    distanceCount = sub.Identifier > distanceCount - 1 ? sub.Identifier + 1 : distanceCount;
                }
            }
        }
        if (theEvent.EventType == Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA && distances.Count > 0)
        {
            Add.IsEnabled = false;
        }
        else
        {
            Add.IsEnabled = true;
        }
    }

    internal void RemoveDistance(Distance distance)
    {
        Log.D("UI.MainPages.DistancesPage", "Remove distance clicked.");
        if (database.GetAppSetting(Constants.Settings.UPDATE_ON_PAGE_CHANGE)!.Value == Constants.Settings.SETTING_TRUE)
        {
            UpdateDatabase();
        }
        // Check for and delete linked distances
        List<Distance> allDistances = database.GetDistances(theEvent!.Identifier);
        bool keepDeleting = true, ignoreParticipantCheck = false;
        foreach (Distance d in allDistances)
        {
            if (!keepDeleting)
            {
                return;
            }
            if (d.LinkedDistance >= 0 && d.LinkedDistance == distance.Identifier)
            {
                if (!ignoreParticipantCheck && database.GetParticipants(theEvent.Identifier, d.Identifier).Count > 0)
                {
                    keepDeleting = false;
                    DialogBox.Show(
                        "Distance has participants, continue?",
                        "Yes",
                        "No",
                        () =>
                        {
                            keepDeleting = true;
                            ignoreParticipantCheck = true;
                            database.RemoveDistance(d);
                        }
                    );
                }
                else
                {
                    database.RemoveDistance(d);
                }
            }
        }
        if (!keepDeleting)
        {
            return;
        }
        if (!ignoreParticipantCheck && database.GetParticipants(theEvent.Identifier, distance.Identifier).Count > 0)
        {
            DialogBox.Show(
                "Distance has participants, continue?",
                "Yes",
                "No",
                () =>
                {
                    keepDeleting = true;
                    ignoreParticipantCheck = true;
                    database.RemoveDistance(distance);
                }
            );
        }
        else
        {
            database.RemoveDistance(distance);
        }
        updateTimingWorker = true;
        UpdateView();
    }

    private void UpdateDatabase()
    {
        Dictionary<int, Distance> oldDistances = [];
        foreach (Distance distance in database.GetDistances(theEvent!.Identifier))
        {
            oldDistances[distance.Identifier] = distance;
        }
        foreach (DistancePart listDiv in DistancesBox.Items.Cast<DistancePart>())
        {
            listDiv.UpdateDistance();
            int divId = listDiv.GetDistance().Identifier;
            if (oldDistances.TryGetValue(divId, out Distance? oDist) &&
                (oDist.StartOffsetSeconds != listDiv.GetDistance().StartOffsetSeconds
                || oDist.StartOffsetMilliseconds != listDiv.GetDistance().StartOffsetMilliseconds
                || oDist.FinishOccurrence != listDiv.GetDistance().FinishOccurrence))
            {
                distancesChanged.Add(divId);
                updateTimingWorker = true;
            }
            database.UpdateDistance(listDiv.GetDistance());
        }
        if (database is SqLiteInterface)
        {
            Results.GetStaticVariables(database);
        }
    }

    public void KeyboardCtrlA()
    {
        Log.D("UI.MainPages.DistancesPage", "Ctrl + A Passed to this page.");
        Add_Click(null, null);
    }

    public void KeyboardCtrlS()
    {
        Log.D("UI.MainPages.DistancesPage", "Ctrl + S Passed to this page.");
        UpdateDatabase();
        UpdateView();
    }

    public void KeyboardCtrlZ()
    {
        UpdateView();
    }

    public void Closing()
    {
        if (database.GetAppSetting(Constants.Settings.UPDATE_ON_PAGE_CHANGE)!.Value == Constants.Settings.SETTING_TRUE)
        {
            UpdateDatabase();
        }
        if (!updateTimingWorker && distancesChanged.Count <= 0) return;
        database.ResetTimingResultsEvent(theEvent!.Identifier);
        mWindow.NotifyTimingWorker();
        mWindow.UpdateRegistrationDistances();
        mWindow.NetworkUpdateResults();
    }

    public void UpdateDistance(Distance distance)
    {
        int divId = distance.Identifier;
        Distance oldDiv = database.GetDistance(divId)!;
        if (oldDiv.StartOffsetSeconds != distance.StartOffsetSeconds ||
            oldDiv.StartOffsetMilliseconds != distance.StartOffsetMilliseconds
            || oldDiv.FinishOccurrence != distance.FinishOccurrence)
        {
            distancesChanged.Add(divId);
        }
        database.UpdateDistance(distance);
        UpdateView();
    }

    public void AddSubDistance(Distance theDistance)
    {
        if (database.GetAppSetting(Constants.Settings.UPDATE_ON_PAGE_CHANGE)!.Value == Constants.Settings.SETTING_TRUE)
        {
            UpdateDatabase();
        }
        database.AddDistance(new(theDistance.Name + " Linked " + distanceCount, theDistance.EventIdentifier, theDistance.Identifier, Constants.Timing.DISTANCE_TYPE_EARLY, 1, theDistance.Wave, theDistance.StartOffsetSeconds, theDistance.StartOffsetMilliseconds));
        updateTimingWorker = true;
        UpdateView();
    }

    private void Update_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.DistancesPage", "Update clicked.");
        UpdateDatabase();
        UpdateView();
        mWindow.NetworkUpdateResults();
    }

    private void Revert_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.DistancesPage", "Revert clicked.");
        UpdateView();
    }

    private async void DeleteButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.MainPages.DistancesPage", "Deleting uploaded distances.");
            if (DeleteButton.Content!.ToString() == "Delete Uploaded")
            {
                DeleteButton.IsEnabled = false;
                DeleteButton.Content = "Working...";
                if (theEvent!.ApiId < 0 || theEvent.ApiEventId.Length < 1)
                {
                    DeleteButton.Content = "Error";
                    return;
                }
                ApiObject api = database.GetApi(theEvent.ApiId)!;
                string[] eventIds = theEvent.ApiEventId.Split(',');
                if (eventIds.Length != 2)
                {
                    DeleteButton.Content = "Error";
                    return;
                }
                // Delete old information from the API
                try
                {
                    await ApiHandlers.DeleteDistances(api, eventIds[0], eventIds[1]);
                }
                catch (ApiException ex)
                {
                    DialogBox.Show(ex.Message);
                }
                DeleteButton.IsEnabled = true;
                DeleteButton.Content = "Delete Uploaded";
            }
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.DistancesPage", "Error deleting uploaded.");
        }
    }

    private async void UploadButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.MainPages.DistancesPage", "Uploading distances.");
            if (UploadButton.Content!.ToString() != "Upload") return;
            UploadButton.IsEnabled = false;
            UploadButton.Content = "Working...";
            if (theEvent!.ApiId < 0 || theEvent.ApiEventId.Length < 1)
            {
                UploadButton.Content = "Error";
                return;
            }
            ApiObject api = database.GetApi(theEvent.ApiId)!;
            string[] eventIds = theEvent.ApiEventId.Split(',');
            if (eventIds.Length != 2)
            {
                UploadButton.Content = "Error";
                return;
            }
            // Save distances displayed
            UpdateDatabase();
            UpdateView();
            // Get Distances and Locations to get their names
            List<ApiDistance> apiDistances = [];
            apiDistances.AddRange(from d in database.GetDistances(theEvent.Identifier) where d.Certification.Trim().Length > 0 select new ApiDistance { Name = d.Name.Trim(), Certification = d.Certification.Trim(), });
            if (apiDistances.Count > 0)
            {
                Log.D("UI.MainPages.DistancesPage", "Attempting to upload " + apiDistances.Count.ToString() + " distances.");
                try
                {
                    GetDistancesResponse response = await ApiHandlers.AddDistances(api, eventIds[0], eventIds[1], apiDistances);
                    if (response.Distances.Count != apiDistances.Count)
                    {
                        DialogBox.Show("Error uploading distances. Uploaded count doesn't match.");
                    }
                }
                catch (ApiException ex)
                {
                    DialogBox.Show(ex.Message);
                }
            }
            UploadButton.IsEnabled = true;
            UploadButton.Content = "Upload";
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.DistancesPage", "Error uploading distances.");
        }
    }

    private void Add_Click(object? sender, RoutedEventArgs? e)
    {
        Log.D("UI.MainPages.DistancesPage", "Add distance clicked.");
        if (database.GetAppSetting(Constants.Settings.UPDATE_ON_PAGE_CHANGE)!.Value == Constants.Settings.SETTING_TRUE)
        {
            UpdateDatabase();
        }
        database.AddDistance(new Distance("New Distance " + distanceCount, theEvent!.Identifier));
        updateTimingWorker = true;
        UpdateView();
    }
}