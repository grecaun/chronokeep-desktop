using System;
using Avalonia.Controls;
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

public partial class SegmentsPage : UserControl, IMainPage
{
    private readonly IMainWindow mWindow;
    private readonly IdbInterface database;
    private readonly Event? theEvent;
    private readonly List<TimingLocation>? locations;
    private readonly List<Distance> distances = [];

    private bool updateTimingWorker;

    private readonly Dictionary<int, List<Segment>> allSegments = [];

    public SegmentsPage(IMainWindow mWindow, IdbInterface database)
    {
        InitializeComponent();
        this.mWindow = mWindow;
        this.database = database;
        theEvent = database.GetCurrentEvent();
        if (theEvent != null)
        {
            locations = database.GetTimingLocations(theEvent.Identifier);
            if (theEvent.CommonStartFinish)
            {
                locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Start/Finish", theEvent.FinishMaxOccurrences - 1, theEvent.FinishIgnoreWithin));
            }
            else
            {
                locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Finish", theEvent.FinishMaxOccurrences - 1, theEvent.FinishIgnoreWithin));
                locations.Insert(0, new TimingLocation(Constants.Timing.LOCATION_START, theEvent.Identifier, "Start", theEvent.StartMaxOccurrences - 1, theEvent.FinishIgnoreWithin));
            }
            distances = database.GetDistances(theEvent.Identifier);
            distances.Sort((x1, x2) => string.Compare(x1.Name, x2.Name, StringComparison.Ordinal));
            distances.RemoveAll(x => x.LinkedDistance != Constants.Timing.DISTANCE_NO_LINKED_ID);
            if (theEvent.ApiId > 0 && theEvent.ApiEventId.Length > 1)
            {
                ApiPanel.IsVisible = true;
            }
            else
            {
                ApiPanel.IsVisible = false;
            }
        }
        UpdateSegments();
        UpdateView();
    }

    public void UpdateView()
    {
        if (theEvent == null || theEvent.Identifier < 0)
        {
            return;
        }
        List<UserControl> items = [];
        if (theEvent.DistanceSpecificSegments)
        {
            foreach (DistanceSegmentHolderPart newHolder in distances.Select(d => new DistanceSegmentHolderPart(theEvent, this, d, distances, allSegments[d.Identifier], locations!)))
            {
                items.Add(newHolder);
                items.AddRange(newHolder.SegmentItems);
            }
        }
        else
        {
            DistanceSegmentHolderPart newHolder = new(theEvent, this, null, distances, allSegments[Constants.Timing.COMMON_SEGMENTS_DISTANCEID], locations!);
            items.Add(newHolder);
            items.AddRange(newHolder.SegmentItems);
        }
        SegmentsBox.ItemsSource = items;
    }

    private void UpdateSegments()
    {
        allSegments.Clear();
        List<Segment> segments = database.GetSegments(theEvent!.Identifier);
        if (theEvent.DistanceSpecificSegments)
        {
            foreach (Distance d in distances)
            {
                allSegments[d.Identifier] = [];
            }
            foreach (Segment seg in segments)
            {
                if (!allSegments.TryGetValue(seg.DistanceId, out List<Segment>? segList))
                {
                    segList = [];
                    allSegments[seg.DistanceId] = segList;
                }
                segList.Add(seg);
            }
        }
        else
        {
            allSegments[Constants.Timing.COMMON_SEGMENTS_DISTANCEID] = segments;
            allSegments[Constants.Timing.COMMON_SEGMENTS_DISTANCEID].RemoveAll(x => x.DistanceId != Constants.Timing.COMMON_SEGMENTS_DISTANCEID);
        }
    }

    internal void RemoveSegment(Segment mySegment)
    {
        Log.D("UI.MainPages.SegmentsPage", "Removing segment.");
        UpdateDatabase();
        allSegments[mySegment.DistanceId].Remove(mySegment);
        database.RemoveSegment(mySegment);
        UpdateView();
    }

    private void UpdateDatabase()
    {
        List<Segment> upSegments = [];
        List<Segment> newSegments = [];
        foreach (object? seg in SegmentsBox.Items)
        {
            if (seg is not SegmentPart tSeg) continue;
            tSeg.UpdateSegment();
            Segment thisSegment = tSeg.MySegment;
            if (thisSegment.Identifier < 1)
            {
                newSegments.Add(thisSegment);
            }
            else
            {
                upSegments.Add(thisSegment);
            }
        }
        newSegments.RemoveAll(x => x.Occurrence < 0);
        database.AddSegments(newSegments);
        updateTimingWorker = true;
        database.UpdateSegments(upSegments);
        if (database is SqLiteInterface)
        {
            Results.GetStaticVariables(database);
        }
    }

    public void KeyboardCtrlA() { }

    public void KeyboardCtrlS()
    {
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
            bool occurrenceError = false;
            foreach (object? seg in SegmentsBox.Items)
            {
                if (seg is not SegmentPart part) continue;
                Segment thisSegment = part.MySegment;
                if (thisSegment.LocationId == Constants.Timing.LOCATION_FINISH && thisSegment.Occurrence >= theEvent!.FinishMaxOccurrences)
                {
                    occurrenceError = true;
                }
                Log.D("UI.MainPages.SegmentsPage", "Distance ID " + part.MySegment.DistanceId + " Segment Name " + part.MySegment.Name + " segment ID " + part.MySegment.Identifier);
            }
            if (occurrenceError)
            {
                DialogBox.Show("Your finish lines has one or more segments beyond the maximum number it supports (" + (theEvent!.FinishMaxOccurrences - 1) + ").  These will not be added. Update locations and max occurrences to fix this.");
            }
        }
        if (!updateTimingWorker) return;
        database.ResetTimingResultsEvent(theEvent!.Identifier);
        mWindow.NetworkClearResults();
        mWindow.NotifyTimingWorker();
    }

    public void AddSegment(int distanceId)
    {
        Log.D("UI.MainPages.SegmentsPage", "Adding segment.");
        Segment newSeg = new(theEvent!.Identifier, distanceId, Constants.Timing.LOCATION_FINISH, 0, 0.0, 0.0, Constants.Distances.MILES, "", "", "");
        allSegments[distanceId].Add(newSeg);
        UpdateView();
    }

    public void CopyFromDistance(int intoDistance, int fromDistance)
    {
        Log.D("UI.MainPages.SegmentsPage", "Copying segments.");
        if (database.GetAppSetting(Constants.Settings.UPDATE_ON_PAGE_CHANGE)!.Value == Constants.Settings.SETTING_TRUE)
        {
            UpdateDatabase();
        }
        database.RemoveSegments(allSegments[intoDistance]);
        allSegments[intoDistance].Clear();
        foreach (Segment newSeg in allSegments[fromDistance].Select(seg => new Segment(seg)
                 {
                     DistanceId = intoDistance
                 }))
        {
            allSegments[intoDistance].Add(newSeg);
        }
        UpdateView();
    }

    private async void UploadButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.MainPages.SegmentsPage", "Uploading segments.");
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
            // Save segments displayed
            UpdateDatabase();
            UpdateSegments();
            // Get Distances and Locations to get their names
            Dictionary<int, Distance> iDistances = [];
            foreach (Distance d in database.GetDistances(theEvent.Identifier))
            {
                iDistances.Add(d.Identifier, d);
            }
            Dictionary<int, TimingLocation> iLocations = [];
            foreach (TimingLocation l in database.GetTimingLocations(theEvent.Identifier))
            {
                iLocations.Add(l.Identifier, l);
            }
            iLocations.Add(Constants.Timing.LOCATION_ANNOUNCER, new TimingLocation(Constants.Timing.LOCATION_ANNOUNCER, theEvent.Identifier, "Announcer", 0, 0));
            iLocations.Add(Constants.Timing.LOCATION_FINISH, new TimingLocation(Constants.Timing.LOCATION_FINISH, theEvent.Identifier, "Finish", 0, 0));
            iLocations.Add(Constants.Timing.LOCATION_START, new TimingLocation(Constants.Timing.LOCATION_START, theEvent.Identifier, "Start", 0, 0));
            Dictionary<int, string> distanceUnits = new()
            {
                { Constants.Distances.FEET, "ft" },
                { Constants.Distances.KILOMETERS, "km" },
                { Constants.Distances.METERS, "m" },
                { Constants.Distances.YARDS, "yd" },
                { Constants.Distances.MILES, "mi" }
            };
            // Convert Segments to APISegments
            List<ApiSegment> segments = [];
            foreach (Segment seg in database.GetSegments(theEvent.Identifier))
            {
                if (!iLocations.TryGetValue(seg.LocationId, out TimingLocation? segmentLocation)) continue;
                if (theEvent.DistanceSpecificSegments)
                {
                    if (iDistances.TryGetValue(seg.DistanceId, out Distance? segmentDistance))
                    {
                        segments.Add(new ApiSegment
                        {
                            Location = segmentLocation.Name,
                            DistanceName = segmentDistance.Name,
                            Name = seg.Name,
                            DistanceValue = seg.CumulativeDistance,
                            DistanceUnit = distanceUnits[seg.DistanceUnit],
                            Gps = seg.Gps,
                            MapLink = seg.MapLink,
                        });
                    }
                }
                else
                {
                    segments.AddRange(iDistances.Values.Select(dist => new ApiSegment
                    {
                        Location = segmentLocation.Name,
                        DistanceName = dist.Name,
                        Name = seg.Name,
                        DistanceValue = seg.CumulativeDistance,
                        DistanceUnit = distanceUnits[seg.DistanceUnit],
                        Gps = seg.Gps,
                        MapLink = seg.MapLink,
                    }));
                }
            }
            // add finish segments
            foreach (Distance d in iDistances.Values)
            {
                if (Constants.Timing.DISTANCE_NO_LINKED_ID == d.LinkedDistance && distanceUnits.TryGetValue(d.DistanceUnit, out string? oDistUnit))
                {
                    segments.Add(new ApiSegment
                    {
                        Location = "Finish",
                        DistanceName = d.Name,
                        Name = "Finish",
                        DistanceValue = d.DistanceValue,
                        DistanceUnit = oDistUnit,
                        Gps = "",
                        MapLink = "",
                    });
                }
            }
            // Remove all segments without a distance value set.
            segments.RemoveAll(x => x.DistanceValue <= 0);
            Log.D("UI.MainPages.SegmentsPage", "Attempting to upload " + segments.Count.ToString() + " segments.");
            try
            {
                AddSegmentsResponse response = await ApiHandlers.AddSegments(api, eventIds[0], eventIds[1], segments);
                if (response.Segments.Count != segments.Count)
                {
                    DialogBox.Show("Error uploading segments. Uploaded count doesn't match.");
                }
            }
            catch (ApiException ex)
            {
                DialogBox.Show(ex.Message);
            }
            UploadButton.IsEnabled = true;
            UploadButton.Content = "Upload";
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.SegmentsPage", "Error uploading segments.");
        }
    }

    private async void DeleteButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.MainPages.SegmentsPage", "Deleting uploaded segments.");
            if (DeleteButton.Content!.ToString() != "Delete Uploaded") return;
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
                await ApiHandlers.DeleteSegments(api, eventIds[0], eventIds[1]);
            }
            catch (ApiException ex)
            {
                DialogBox.Show(ex.Message);
            }
            DeleteButton.IsEnabled = true;
            DeleteButton.Content = "Delete Uploaded";
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.SegmentsPage", "Error deleting segments");
        }
    }

    private void Update_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        UpdateDatabase();
        UpdateSegments();
        foreach (object? seg in SegmentsBox.Items)
        {
            if (seg is not SegmentPart segment) continue;
            Segment thisSegment = segment.MySegment;
            switch (thisSegment.LocationId)
            {
                case Constants.Timing.LOCATION_FINISH when thisSegment.Occurrence >= theEvent!.FinishMaxOccurrences:
                    DialogBox.Show("Your finish line has one or more segments beyond the maximum number it supports (" + (theEvent.FinishMaxOccurrences - 1) + ").  This could cause errors.");
                    break;
                case Constants.Timing.LOCATION_START when thisSegment.Occurrence >= theEvent!.StartMaxOccurrences:
                    DialogBox.Show("Your start line has one or more segments beyond the maximum number it supports (" + (theEvent.StartMaxOccurrences - 1) + ").  This could cause errors.");
                    break;
            }
            Log.D("UI.MainPages.SegmentsPage", "Distance ID " + segment.MySegment.DistanceId + " Segment Name " + segment.MySegment.Name + " segment ID " + segment.MySegment.Identifier);
        }
        UpdateView();
    }

    private void Reset_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        UpdateSegments();
        UpdateView();
    }
}