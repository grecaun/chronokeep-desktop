using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.IO.HtmlTemplates.Printables;
using Chronokeep.Objects;
using Chronokeep.UI.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace Chronokeep.UI.MainPages.Timing;

public partial class AwardPage : UserControl, ISubPage
{
    private readonly IdbInterface database;
    private readonly TimingPage parent;
    private readonly Event? theEvent;

    private readonly ObservableCollection<AgeGroup> customAgeGroups = [];

    [GeneratedRegex("[^0-9]")]
    private static partial Regex AllowedChars();

    public AwardPage(TimingPage parent, IdbInterface database)
    {
        InitializeComponent();
        this.parent = parent;
        this.database = database;
        theEvent = database.GetCurrentEvent();
        if (theEvent == null || theEvent.Identifier < 0)
        {
            Log.E("UI.Timing.AwardPage", "Something went wrong and no proper event was returned.");
            return;
        }
        CustomGroupsListView.ItemsSource = customAgeGroups;
        List<Distance> distances = database.GetDistances(theEvent.Identifier);
        distances.Sort((x1, x2) => string.Compare(x1.Name, x2.Name, StringComparison.Ordinal));
        foreach (Distance d in distances.Where(d => d.LinkedDistance <= 0))
        {
            DistancesBox.Items.Add(new ListBoxItem()
            {
                Content = d.Name
            });
        }
        parent.SetReaders([], false);
        UpdateView();
    }

    private AwardOptions GetOptions()
    {
        return new AwardOptions
        {
            PrintOverall = OverallYes.IsChecked == true,
            PrintAgeGroups = AgYes.IsChecked == true,
            PrintCustom = CustomYes.IsChecked == true,
            NumOverall = OverallNumberParticipants.Text!.Length == 0 ? 3 : Convert.ToInt32(OverallNumberParticipants.Text),
            NumAgeGroups = AgNumberParticipants.Text!.Length == 0 ? 3 : Convert.ToInt32(AgNumberParticipants.Text),
            NumCustom = CustomNumberParticipants.Text!.Length == 0 ? 3 : Convert.ToInt32(CustomNumberParticipants.Text),
            ExcludeOverallAg = OverallExcludeAg.IsChecked == true,
            ExcludeOverallCustom = OverallExcludeCustom.IsChecked == true,
            ExcludeAgeGroupsCustom = AgExcludeCustom.IsChecked == true
        };
    }

    private void IsNumber(object? sender, Avalonia.Input.TextInputEventArgs e)
    {
        e.Handled = AllowedChars().IsMatch(e.Text!);
    }

    private string GetPrintableAwards(List<string> distances, AwardOptions options)
    {
        // Get all results for the race.
        List<TimeResult> results = database.GetTimingResults(theEvent!.Identifier);
        // Remove all unknown participants.
        results.RemoveAll(x => x.Bib == Constants.Timing.CHIPREAD_DUMMYBIB);
        results.RemoveAll(x => x.DistanceName.Length < 1);
        // Remove all from unselected divisions.
        if (distances.Count > 0)
        {
            results.RemoveAll(x => !distances.Contains(x.DistanceName));
        }
        // Remove all results that are not finish results.
        results.RemoveAll(x => x.SegmentId != Constants.Timing.SEGMENT_FINISH);
        // Remove all DNF results.
        results.RemoveAll(x => x.Status == Constants.Timing.TIMERESULT_STATUS_DNF);
        // If we're a time based event, exclude all but the last result
        if (theEvent.EventType == Constants.Timing.EVENT_TYPE_TIME)
        {
            Dictionary<string, TimeResult> lastResult = [];
            foreach (TimeResult individual in results)
            {
                if (lastResult.TryGetValue(individual.ParticipantName, out TimeResult? oResult))
                {
                    if (oResult.Occurrence < individual.Occurrence)
                    {
                        lastResult[individual.ParticipantName] = individual;
                    }
                }
                else
                {
                    lastResult[individual.ParticipantName] = individual;
                }
            }
            results = [.. lastResult.Values];
        }
        results.Sort(TimeResult.CompareByDistancePlace);
        // This dictionary stores the list of results with the key being the distance and the header for the grouping (Overall, Age Group, Custom)
        Dictionary<string, Dictionary<string, List<TimeResult>>> resultsDictionary = [];
        // This dictionary keeps track of the number of individuals in an age group so we can choose to not print age group awards
        // and still exclude them from the results.
        Dictionary<(string, string), int> ageGroupCounter = [];
        foreach (TimeResult result in results)
        {
            // Gather the gender and modify it to what we want for use in results.
            string gender = result.Gender switch
            {
                "Woman" => "Women",
                "Man" => "Men",
                "Not Specified" => "",
                _ => result.Gender
            };
            bool addedToAgeGroupResults = false;
            if (!resultsDictionary.TryGetValue(result.DistanceName, out Dictionary<string, List<TimeResult>>? distResultsDict))
            {
                distResultsDict = [];
                resultsDictionary[result.DistanceName] = distResultsDict;
            }
            // Get the overall (gender) results.
            if (result.GenderPlace <= options.NumOverall)
            {
                // Check if we're printing the overall results.
                if (options.PrintOverall)
                {
                    if (!distResultsDict.TryGetValue(gender, out List<TimeResult>? ovResults))
                    {
                        ovResults = [];
                        distResultsDict[gender] = ovResults;
                    }
                    ovResults.Add(result);
                }
                // Check if we were told to exclude overall from age group awards.
                // Also ensure we've been told to print age groups and that the person is in the age group results.
                // The place check is easy here because we can check the result.GenderPlace value.
                // Exclude any genders we don't know about.
                if (!options.ExcludeOverallAg
                    && result.GenderPlace <= options.NumAgeGroups
                    && gender != "")
                {
                    string ageGroup = $"{gender} {result.AgeGroupName}";
                    int oAgCount = ageGroupCounter.GetValueOrDefault((result.DistanceName, ageGroup), 0);
                    if (options.PrintAgeGroups)
                    {
                        if (!distResultsDict.TryGetValue(ageGroup, out List<TimeResult>? oResList))
                        {
                            oResList = [];
                            distResultsDict[ageGroup] = oResList;
                        }
                        oResList.Add(result);
                    }
                    ageGroupCounter[(result.DistanceName, ageGroup)] = oAgCount + 1;
                    addedToAgeGroupResults = true;
                }
                // This is almost the same as the age groups category.
                // Check if told to exclude from custom results.
                // If we were told to print custom results
                // exclude any unknown genders
                // check if we were told to exclude age group winners from custom winners, if so only include ones we didn't add above.
                // this will exclude any that would have won an age group award even if we didn't actually print it.
                // this is the behavior we want and should work the same for overall as well
                if (options.ExcludeOverallCustom
                    || !options.PrintCustom
                    || gender == ""
                    || (options.ExcludeAgeGroupsCustom && addedToAgeGroupResults)) continue;
                {
                    int age = result.Age(theEvent.Date);
                    foreach (AgeGroup group in customAgeGroups)
                    {
                        if (age < group.StartAge || age > group.EndAge) continue;
                        string ageGroup = $"{gender} {group.PrettyName()}";
                        if (!distResultsDict.TryGetValue(ageGroup, out List<TimeResult>? oResList))
                        {
                            oResList = [];
                            distResultsDict[ageGroup] = oResList;
                        }
                        // only add to the results if we're under the number of results we can print
                        if (oResList.Count < options.NumCustom)
                        {
                            oResList.Add(result);
                        }
                    }
                }
            }
            else if (gender != "")
            {
                // We're not in the overall results.
                // Check for age groups.
                string ageGroup = $"{gender} {result.AgeGroupName}";
                if (!distResultsDict.TryGetValue(ageGroup, out List<TimeResult>? oResList))
                {
                    oResList = [];
                    distResultsDict[ageGroup] = oResList;
                }
                // We're doing it this way so we can exclude people from custom if we want even if we don't print the age group.
                int oAgCount = ageGroupCounter.GetValueOrDefault((result.DistanceName, ageGroup), 0);
                if (oAgCount < options.NumAgeGroups)
                {
                    if (options.PrintAgeGroups)
                    {
                        oResList.Add(result);
                    }
                    ageGroupCounter[(result.DistanceName, ageGroup)] = oAgCount + 1;
                    addedToAgeGroupResults = true;
                }
                // Check for custom groups.
                // Ensure we don't care about excluding age group winners, or they didn't actually win
                if (!options.PrintCustom ||
                    (options.ExcludeAgeGroupsCustom && addedToAgeGroupResults)) continue;
                Log.D("UI.Timing.AwardPage", "Checking to add to custom award group.");
                int age = result.Age(theEvent.Date);
                foreach (AgeGroup group in customAgeGroups)
                {
                    if (age < group.StartAge || age > group.EndAge) continue;
                    // only add to the results if we're under the number of results we can print
                    if (oResList.Count < options.NumCustom)
                    {
                        oResList.Add(result);
                    }
                }
            }
        }
        // Collect all the groups into lists according to their distance
        // We do this so we can sort them by age group.
        Dictionary<string, List<string>> distanceGroups = [];
        foreach (string dist in resultsDictionary.Keys)
        {
            Dictionary<string, List<TimeResult>> distResultsDictionary = resultsDictionary[dist];
            foreach (string group in distResultsDictionary.Keys.Where(group => distResultsDictionary[group].Count > 0))
            {
                if (!distanceGroups.TryGetValue(dist, out List<string>? distGroupList))
                {
                    distGroupList = [];
                    distanceGroups[dist] = distGroupList;
                }
                if (!distGroupList.Contains(group))
                {
                    distGroupList.Add(group);
                }
            }
        }
        // sort our lists
        foreach (string dist in distanceGroups.Keys)
        {
            distanceGroups[dist].Sort(CompareGroups);
        }
        AwardsPrintable output = new(theEvent, distanceGroups, resultsDictionary);
        return output.TransformText();
    }

    private static int CompareGroups(string group1, string group2)
    {
        if (group1 == "Overall")
        {
            Log.D("Test", "Overall found1");
            return -1;
        }
        if (group2 == "Overall")
        {
            Log.D("Test", "Overall found2");
            return 1;
        }
        string[] firstSplit1 = group1.Split(' ');
        string[] firstSplit2 = group2.Split(' ');
        if (firstSplit1.Length < 2 || firstSplit2.Length < 2)
        {
            return string.Compare(group1, group2, StringComparison.Ordinal);
        }
        // if genders are not equal, sort by gender
        if (firstSplit1[0] != firstSplit2[0])
        {
            return string.Compare(firstSplit1[0], firstSplit2[0], StringComparison.Ordinal);
        }
        if (firstSplit1[1].Equals("Under", StringComparison.OrdinalIgnoreCase))
        {
            Log.D("Test", "Under found1");
            return -1;
        }
        if (firstSplit2[1].Equals("Under", StringComparison.OrdinalIgnoreCase))
        {
            Log.D("Test", "Under found2");
            return 1;
        }
        if (firstSplit1[1].Equals("Over", StringComparison.OrdinalIgnoreCase))
        {
            Log.D("Test", "Over found1");
            return 1;
        }
        if (firstSplit2[1].Equals("Over", StringComparison.OrdinalIgnoreCase))
        {
            Log.D("Test", "Over found2");
            return -1;
        }
        string[] secondSplit1 = firstSplit1[0].Split('-');
        string[] secondSplit2 = firstSplit2[0].Split('-');
        if (secondSplit1.Length < 2 || secondSplit2.Length < 2)
        {
            return string.Compare(firstSplit1[1], firstSplit2[1], StringComparison.Ordinal);
        }
        bool sOneOkay = int.TryParse(secondSplit1[0], out int start1);
        bool sTwoOkay = int.TryParse(secondSplit2[0], out int start2);
        if (!sOneOkay || !sTwoOkay)
        {
            return string.Compare(firstSplit1[1], firstSplit2[1], StringComparison.Ordinal);
        }
        return start1.CompareTo(start2);
    }

    public void CancelableUpdateView(CancellationToken token) { }

    public void Search(CancellationToken token) { }

    public void UpdateView()
    {
        customAgeGroups.Clear();
        foreach (AgeGroup age in database.GetAgeGroups(theEvent!.Identifier, Constants.Timing.AGEGROUPS_CUSTOM_DISTANCEID))
        {
            customAgeGroups.Add(age);
        }
    }

    public void Closing() { }

    public void KeyboardCtrlA() { }

    public void KeyboardCtrlS() { }

    public void KeyboardCtrlZ() { }

    private void AddCustom_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.AwardPage", "Add custom group clicked.");
        try
        {
            int start = Convert.ToInt32(StartCustom.Text);
            int end = Convert.ToInt32(EndCustom.Text);
            string custom = CustomNameBox.Text!;
            if (start > -1 || end < 101)
            {
                database.AddAgeGroup(
                    new AgeGroup(theEvent!.Identifier,
                        Constants.Timing.AGEGROUPS_CUSTOM_DISTANCEID,
                        start,
                        end,
                        custom
                        ));
                UpdateView();
                StartCustom.Text = "";
                EndCustom.Text = "";
                CustomNameBox.Text = "";
                StartCustom.Focus();
            }
            else
            {
                DialogBox.Show("Ages are not in the range of 0 to 100.");
            }
        }
        catch (Exception ex)
        {
            Log.E("UI.Timing.AwardPage", ex.Message);
            DialogBox.Show("Start or end age not specified.");
        }
    }

    private void DeleteCustom_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.AwardPage", "Deleting some entries... maybe.");
        List<AgeGroup> items = [];
        IList selected = CustomGroupsListView.SelectedItems;
        if (selected.Count < 1)
        {
            return;
        }
        items.AddRange(selected.Cast<AgeGroup>());
        database.RemoveAgeGroups(items);
        UpdateView();
    }

    private async void SaveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.Timing.AwardPage", "Save clicked.");
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
                FileTypeChoices = [Utils.PdfType],
                SuggestedFileName = $"{theEvent!.YearCode}-{theEvent.Name}-Awards.pdf".Replace(' ', '-'),
                SuggestedStartLocation = startingFolder,
            });
            AwardOptions options = GetOptions();
            List<string> divsToPrint = [];
            if (DistancesBox.SelectedItems != null)
            {
                foreach (object? divItem in DistancesBox.SelectedItems)
                {
                    if (divItem is not ListBoxItem div || div.Content == null) continue;
                    if (div.Content.Equals("All"))
                    {
                        divsToPrint.Clear();
                        break;
                    }
                    divsToPrint.Add(div.Content.ToString()!);
                }
            }
            if (options is { PrintCustom: false, PrintAgeGroups: false, PrintOverall: false })
            {
                DialogBox.Show("No awards group selected to print/save.");
                return;
            }

            if (file is null) return;
            try
            {
                string htmlString = GetPrintableAwards(divsToPrint, options);
                string weasyName;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    weasyName = Path.Combine(Directory.GetCurrentDirectory(), "weasyprint.exe");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    weasyName = "weasyprint";
                    using Process testWeasy = new();
                    testWeasy.StartInfo.FileName = "which";
                    testWeasy.StartInfo.Arguments = weasyName;
                    testWeasy.Start();
                    await testWeasy.WaitForExitAsync();
                    int exitVal = testWeasy.ExitCode;
                    testWeasy.Close();
                    if (exitVal != 0)
                    {
                        DialogBox.Show("This function requires Weasyprint to function. Please install it and try again.",
                            "https://doc.courtbouillon.org/weasyprint/stable/first_steps.html");
                        return;
                    }
                }
                else
                {
                    DialogBox.Show("Operating System detected does not support this function currently.");
                    return;
                }
                // Write HTML to a temp file.
                string tmpFile = Path.Combine(Path.GetTempPath(), "print_temp.html");
                await using StreamWriter streamWriter = new(File.Open(tmpFile, FileMode.Create));
                await streamWriter.WriteAsync(htmlString);
                streamWriter.Close();
                // Delete old file if it exists.
                string filePath = file.TryGetLocalPath()!.Replace(' ', '-');
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                // Use weasyprint to convert our temp html file to a saved PDF file.
                using Process createPdf = new();
                createPdf.StartInfo.FileName = weasyName;
                createPdf.StartInfo.Arguments = $" {tmpFile} {filePath}";
                createPdf.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                createPdf.Start();
                // wait for it to exit then kill it, even if the wait timed out
                await createPdf.WaitForExitAsync();
                createPdf.Close();
                // delete old file
                File.Delete(tmpFile);
                // DialogBox.Show("File saved.");
                using Process openPdf = new();
                openPdf.StartInfo.FileName = filePath;
                openPdf.StartInfo.UseShellExecute = true;
                openPdf.Start();
                await openPdf.WaitForExitAsync();
                openPdf.Close();
            }
            catch
            {
                DialogBox.Show("Unable to save file.");
            }
        }
        catch (Exception)
        {
            Log.D("UI.Timing.AwardPage", "Error saving.");
        }
    }

    private void DoneButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.AwardPage", "Done clicked.");
        parent.LoadMainDisplay();
    }

    private class AwardOptions
    {
        public bool PrintOverall { get; init; } = true;
        public int NumOverall { get; init; } = 3;
        // Exclude overall winners from age group awards.
        public bool ExcludeOverallAg { get; init; }
        // Exclude overall winners from custom group awards.
        public bool ExcludeOverallCustom { get; init; }
        public bool PrintAgeGroups { get; init; } = true;
        public int NumAgeGroups { get; init; } = 3;
        // Exclude winners in the Age Groups sections from custom sections.
        public bool ExcludeAgeGroupsCustom { get; init; }
        public bool PrintCustom { get; init; } = true;
        public int NumCustom { get; init; } = 3;
    }
}