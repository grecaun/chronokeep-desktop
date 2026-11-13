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
using Avalonia.Input;
using Avalonia.Interactivity;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Network.API;
using Chronokeep.Network.Runsignup;
using Chronokeep.Objects;
using Chronokeep.Objects.Runsignup;
using Chronokeep.UI.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Chronokeep.UI.Runsignup;

/// <summary>
/// Links the current event to a Runsignup race in four steps: the Runsignup account, the race,
/// a distance for each Runsignup event, and the fields copied into Comments. Everything is decided
/// here, so a download needs no questions.
/// </summary>
public partial class RunsignupLinkWindow : ChronokeepWindow
{
    /// <summary>Distance choice tags that aren't distance ids.</summary>
    internal const int CreateNew = -2;
    internal const int Skip = RegistrationDistance.SkipDistance;
    private const int SampleSize = 25;

    private readonly IMainWindow window;
    private readonly IdbInterface database;
    private readonly Event theEvent;
    private readonly List<RegistrationDistance> savedMap;
    private int step = 1;

    private List<RsuRace> races = [];
    private RsuRace? race;
    private List<RsuParticipant> sample = [];
    /// <summary>Comment fields in the order they were ticked.</summary>
    private readonly List<string> chosenFields = [];

    /// <summary>Finishes when the step being shown has loaded what it needs. Tests wait on it.</summary>
    internal Task Loading { get; private set; } = Task.CompletedTask;

    private RunsignupLinkWindow(IMainWindow window, IdbInterface database, Event theEvent)
    {
        InitializeComponent();
        ChronokeepInitialize();
        this.window = window;
        this.database = database;
        this.theEvent = theEvent;
        savedMap = theEvent.IsRegistrationLinked ? database.GetRegistrationDistances(theEvent.Identifier) : [];
        chosenFields.AddRange(theEvent.RegistrationCommentFields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        UnlinkButton.IsVisible = theEvent.IsRegistrationLinked;
        List<ApiObject> apis = [.. database.GetAllApi().Where(a => a.IsRunsignup)];
        foreach (ApiObject api in apis)
        {
            ApiList.Items.Add(new ComboBoxItem { Content = api.Nickname.Length > 0 ? api.Nickname : "Runsignup", Tag = api });
        }
        ApiList.SelectedItem = ApiList.Items.OfType<ComboBoxItem>().FirstOrDefault(i => ((ApiObject)i.Tag!).Identifier == theEvent.RegistrationApiId)
            ?? ApiList.Items.OfType<ComboBoxItem>().FirstOrDefault();
        NoApiText.IsVisible = apis.Count == 0;
        ApiList.IsVisible = apis.Count > 0;
        NextButton.IsEnabled = apis.Count > 0;
        if (theEvent.IsRegistrationLinked)
        {
            RaceIdBox.Text = theEvent.RegistrationRaceId;
        }
    }

    /// <summary>Opens the wizard for the current event, or returns null if there isn't one.</summary>
    public static RunsignupLinkWindow? NewWindow(IMainWindow window, IdbInterface database)
    {
        Event? theEvent = database.GetCurrentEvent();
        return theEvent == null || theEvent.Identifier < 0 ? null : new RunsignupLinkWindow(window, database, theEvent);
    }

    private ApiObject? Api => (ApiList.SelectedItem as ComboBoxItem)?.Tag as ApiObject;

    private void ShowStep(int newStep)
    {
        step = newStep;
        ApiStep.IsVisible = step == 1;
        RaceStep.IsVisible = step == 2;
        DistanceStep.IsVisible = step == 3;
        CommentStep.IsVisible = step == 4;
        StepTitle.Text = step switch
        {
            1 => "Select Runsignup API to use:",
            2 => "Select Race to link to:",
            3 => "Select the distance for each event:",
            _ => "Select what goes in Comments:",
        };
        BackButton.IsVisible = step > 1;
        UnlinkButton.IsVisible = step == 1 && theEvent.IsRegistrationLinked;
        NextButton.Content = step == 4 ? "Save" : "Next";
    }

    private async void Next_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            await Next();
        }
        catch (Exception ex)
        {
            Log.E("UI.Runsignup.RunsignupLinkWindow", $"Error moving to the next step: {ex.Message}");
        }
    }

    internal async Task Next()
    {
        switch (step)
        {
            case 1:
                if (Api == null) return;
                ShowStep(2);
                Loading = LoadRaces();
                await Loading;
                break;
            case 2:
                NextButton.IsEnabled = false;
                try
                {
                    Loading = PickRace();
                    await Loading;
                }
                finally
                {
                    NextButton.IsEnabled = true;
                }
                break;
            case 3:
                ShowStep(4);
                Loading = LoadCommentFields();
                await Loading;
                break;
            default:
                Save();
                break;
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Back_Click(object? sender, RoutedEventArgs e)
    {
        if (step > 1) ShowStep(step - 1);
    }

    private async Task LoadRaces()
    {
        RaceStatus.Text = "Retrieving Races, Please Hold.";
        try
        {
            // Ask for races from a little before the event, so an event that's already happened still finds its race.
            DateTime from = DateTime.Today;
            if (EventDates.TryParse(theEvent.Date, out DateTime eventDate) && eventDate.AddDays(-45) < from)
            {
                from = eventDate.AddDays(-45);
            }
            races = await RunsignupHandlers.GetRaces(Api!, from);
            RaceStatus.Text = races.Count > 0
                ? "Choose the race, or type its race id below."
                : "No races were found for this account. Type the race id below.";
        }
        catch (ApiException ex)
        {
            races = [];
            RaceStatus.Text = $"{ex.Message} You can still type the race id below.";
        }
        FilterRaces();
        // The race already linked, or else the one whose name and date match this event.
        RsuRace? pick = races.FirstOrDefault(r => r.RaceId == RaceIdBox.Text?.Trim());
        if (pick == null && RaceIdBox.Text?.Trim().Length is null or 0)
        {
            pick = RaceMatcher.BestMatch(theEvent, races);
            if (pick != null)
            {
                RaceStatus.Text = "This race looks like a match. Check it and press Next, or choose another.";
            }
        }
        if (pick != null)
        {
            RaceList.SelectedItem = pick;
            RaceList.ScrollIntoView(pick);
        }
    }

    private void FilterRaces()
    {
        string search = RaceSearch.Text?.Trim() ?? "";
        RaceList.ItemsSource = search.Length == 0 ? races : [.. races.Where(r => r.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || r.RaceId == search)];
    }

    private void RaceSearch_TextChanged(object? sender, TextChangedEventArgs e) => FilterRaces();

    private void RaceList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RaceList.SelectedItem is RsuRace selected)
        {
            RaceIdBox.Text = selected.RaceId;
        }
    }

    private async void RaceList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            if (RaceList.SelectedItem is RsuRace) await Next();
        }
        catch (Exception ex)
        {
            Log.E("UI.Runsignup.RunsignupLinkWindow", $"Error choosing race: {ex.Message}");
        }
    }

    /// <summary>Gets the chosen race with its events, then shows the distance map.</summary>
    private async Task PickRace()
    {
        string raceId = RaceIdBox.Text?.Trim() ?? "";
        if (raceId.Length == 0)
        {
            DialogBox.Show("Choose a race or type its race id.");
            return;
        }
        try
        {
            race = await RunsignupHandlers.GetRace(Api!, raceId);
        }
        catch (ApiException ex)
        {
            DialogBox.Show(ex.Message);
            return;
        }
        // Runsignup lists every year's events; only the ones nearest this event's date are mapped.
        List<RsuEvent> current = RunsignupDownload.EventsNearest(theEvent, race.Events);
        if (current.Count == 0)
        {
            DialogBox.Show("That race has no events in Runsignup.");
            return;
        }
        int hidden = race.Events.Count - current.Count;
        race = race with { Events = current };
        DistanceNote.Text = hidden > 0 ? $"Showing the events nearest {EventDates.Display(theEvent.Date)}. {hidden} from other years aren't shown." : "";
        DistanceNote.IsVisible = hidden > 0;
        BuildDistanceRows();
        ShowStep(3);
    }

    private void BuildDistanceRows()
    {
        DistanceRows.Children.Clear();
        List<Distance> distances = database.GetDistances(theEvent.Identifier);
        distances.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        Dictionary<string, RegistrationDistance> saved = savedMap.ToDictionary(m => m.RsuEventId);
        bool sameRace = race!.RaceId == theEvent.RegistrationRaceId;
        foreach (RsuEvent ev in race.Events)
        {
            ComboBox choice = new()
            {
                Name = "DistanceChoice",
                Tag = ev,
                Width = 180,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            };
            foreach (Distance d in distances)
            {
                choice.Items.Add(new ComboBoxItem { Content = d.Name, Tag = d.Identifier });
            }
            choice.Items.Add(new ComboBoxItem { Content = "Create new", Tag = CreateNew });
            choice.Items.Add(new ComboBoxItem { Content = "Skip", Tag = Skip });
            int pick;
            bool isNew = sameRace && theEvent.IsRegistrationLinked && !saved.ContainsKey(ev.EventId);
            if (sameRace && saved.TryGetValue(ev.EventId, out RegistrationDistance? map) && (map.IsSkipped || distances.Any(d => d.Identifier == map.DistanceId)))
            {
                pick = map.DistanceId;
            }
            else
            {
                // Default to a distance with the same name, else make one.
                pick = distances.FirstOrDefault(d => d.Name.Equals(ev.Name, StringComparison.OrdinalIgnoreCase)
                    || (ev.Distance.Length > 0 && d.Name.Equals(ev.Distance, StringComparison.OrdinalIgnoreCase)))?.Identifier ?? CreateNew;
            }
            choice.SelectedItem = choice.Items.OfType<ComboBoxItem>().First(i => (int)i.Tag! == pick);
            Grid row = new() { ColumnDefinitions = new ColumnDefinitions("200,180"), Margin = new Avalonia.Thickness(0, 0, 0, 5) };
            TextBlock label = new()
            {
                Text = isNew ? $"{ev.Name} (new)" : ev.Name,
                FontSize = 15,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                TextAlignment = Avalonia.Media.TextAlignment.Right,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Padding = new Avalonia.Thickness(0, 0, 5, 0),
            };
            Grid.SetColumn(choice, 1);
            row.Children.Add(label);
            row.Children.Add(choice);
            DistanceRows.Children.Add(row);
        }
    }

    /// <summary>Each Runsignup event's distance choice: a distance id, <see cref="CreateNew"/> or <see cref="Skip"/>.</summary>
    internal List<(RsuEvent Event, int Choice)> DistanceChoices() =>
        [.. DistanceRows.Children.OfType<Grid>()
            .Select(row => row.Children.OfType<ComboBox>().Single())
            .Select(box => ((RsuEvent)box.Tag!, (int)((ComboBoxItem)box.SelectedItem!).Tag!))];

    /// <summary>Lists the built-in fields and the race questions found in a sample of participants.</summary>
    private async Task LoadCommentFields()
    {
        List<string> mapped = [.. DistanceChoices().Where(c => c.Choice != Skip).Select(c => c.Event.EventId)];
        CommentStatus.Text = "Retrieving Questions, Please Hold.";
        CommentStatus.IsVisible = true;
        sample = [];
        if (mapped.Count > 0)
        {
            try
            {
                sample = await RunsignupHandlers.GetParticipantsPage(Api!, race!.RaceId, mapped, 1, SampleSize);
            }
            catch (ApiException ex)
            {
                CommentStatus.Text = $"Questions couldn't be listed: {ex.Message}";
            }
        }
        List<(string Field, string Name)> fields = [.. CommentFields.BuiltIn];
        foreach (RsuAnswer answer in sample.SelectMany(p => p.Answers))
        {
            string field = CommentFields.QuestionPrefix + answer.QuestionId;
            if (answer.QuestionId.Length > 0 && !fields.Any(f => f.Field == field))
            {
                fields.Add((field, answer.QuestionText.Length > 0 ? answer.QuestionText : $"Question {answer.QuestionId}"));
            }
        }
        // Keep questions chosen before even if this sample didn't answer them.
        foreach (string field in chosenFields.Where(f => !fields.Any(x => x.Field == f)))
        {
            fields.Add((field, field.StartsWith(CommentFields.QuestionPrefix) ? $"Question {field[CommentFields.QuestionPrefix.Length..]}" : field));
        }
        if (CommentStatus.Text == "Retrieving Questions, Please Hold.")
        {
            CommentStatus.IsVisible = false;
        }
        CommentFieldList.Children.Clear();
        foreach ((string field, string name) in fields)
        {
            CheckBox box = new() { Content = name, Tag = field, IsChecked = chosenFields.Contains(field), Name = "CommentField", FontSize = 15 };
            box.IsCheckedChanged += CommentField_Changed;
            CommentFieldList.Children.Add(box);
        }
        UpdatePreview();
    }

    private void CommentField_Changed(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: string field } box) return;
        chosenFields.Remove(field);
        if (box.IsChecked == true) chosenFields.Add(field);
        UpdatePreview();
    }

    /// <summary>The chosen comment fields, as stored on the event.</summary>
    internal string CommentFieldSetting => string.Join(",", chosenFields);

    private void UpdatePreview()
    {
        List<string> lines = [.. sample.Take(3).Select(p => $"{p.FirstName} {p.LastName}: {RunsignupDownload.Comments(p, CommentFieldSetting)}")];
        CommentPreview.Text = chosenFields.Count == 0 ? "Comments won't be changed by downloads."
            : lines.Count == 0 ? "No participants to preview yet." : string.Join("\n", lines);
    }

    /// <summary>Saves the link and distance map, creating any new distances.</summary>
    internal void Save()
    {
        List<RegistrationDistance> map = [];
        foreach ((RsuEvent ev, int choice) in DistanceChoices())
        {
            int distanceId = choice;
            if (choice == CreateNew)
            {
                string name = ev.Name.Length > 0 ? ev.Name : $"Event {ev.EventId}";
                Distance? existing = database.GetDistances(theEvent.Identifier).FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                distanceId = existing?.Identifier ?? database.AddDistance(new Distance(name, theEvent.Identifier));
            }
            map.Add(new RegistrationDistance(theEvent.Identifier, ev.EventId, ev.Name, distanceId));
        }
        theEvent.RegistrationApiId = Api!.Identifier;
        theEvent.RegistrationRaceId = race!.RaceId;
        theEvent.RegistrationCommentFields = CommentFieldSetting;
        database.UpdateEvent(theEvent);
        database.SetRegistrationDistances(theEvent.Identifier, map);
        Close();
    }

    private void Unlink_Click(object? sender, RoutedEventArgs e)
    {
        DialogBox.AsyncShow(
            "Unlink this event from Runsignup? Participants already downloaded are kept.",
            "Unlink",
            "Cancel",
            Unlink);
    }

    /// <summary>Removes the Runsignup link and its distance map. Participants stay.</summary>
    internal void Unlink()
    {
        theEvent.RegistrationApiId = Constants.ApiConstants.NULL_ID;
        theEvent.RegistrationRaceId = Constants.ApiConstants.NULL_EVENT_ID;
        theEvent.RegistrationCommentFields = "";
        database.UpdateEvent(theEvent);
        database.SetRegistrationDistances(theEvent.Identifier, []);
        Close();
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        window.WindowFinalize();
    }

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}
