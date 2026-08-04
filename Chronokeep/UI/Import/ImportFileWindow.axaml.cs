using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.IO;
using Chronokeep.Interfaces.UI;
using Chronokeep.IO;
using Chronokeep.Objects;
using Chronokeep.UI.Parts;
using Chronokeep.UI.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Chronokeep.UI.Import.ImportFilePage2Alt;

namespace Chronokeep.UI.Import;

public partial class ImportFileWindow : ChronokeepWindow
{
    private readonly IDataImporter importer;
    private readonly IMainWindow? window;
    private readonly IdbInterface database;
    private readonly bool init = true;
    internal static readonly string[] HUMAN_FIELDS = [
        "",
        "Age",
        "Anonymous",
        "Apparel",
        "Bib",
        "Birthday",
        "City",
        "Comments",
        "Country",
        "Distance",
        "Division",
        "Email",
        "Emergency Contact Name",
        "Emergency Contact Phone",
        "First Name",
        "Gender",
        "Last Name",
        "Mobile",
        "Other",
        "Owes",
        "Parent",
        "Phone",
        "Registration Date",
        "State",
        "Street",
        "Street 2",
        "Zip"
    ];

    private const int AGE = 1;
    private const int ANONYMOUS = 2;
    private const int APPAREL_ITEM = 3;
    private const int BIB = 4;
    private const int BIRTHDAY = 5;
    private const int CITY = 6;
    private const int COMMENTS = 7;
    private const int COUNTRY = 8;
    private const int DISTANCE = 9;
    private const int DIVISION = 10;
    private const int EMAIL = 11;
    private const int EMERGENCY_NAME = 12;
    private const int EMERGENCY_PHONE = 13;
    internal const int FIRST = 14;
    private const int GENDER = 15;
    internal const int LAST = 16;
    private const int MOBILE = 17;
    private const int OTHER = 18;
    private const int OWES = 19;
    private const int PARENT = 20;
    private const int PHONE = 21;
    private const int REGISTRATION_DATE = 22;
    private const int STATE = 23;
    private const int STREET = 24;
    private const int STREET2 = 25;
    private const int ZIP = 26;
    
    private UserControl? page;
    private int[] keys = [];

    private bool noDistance;

    private readonly Event? theEvent;

    /**
     * VERIFICATION VARIABLES
     */
    private List<Participant> existingParticipants = [];
    private List<Participant> importParticipants = [];
    private readonly List<Participant> updatedParticipants = [];

    private ImportFileWindow(IMainWindow? window, IDataImporter importer, IdbInterface database)
    {
        InitializeComponent();
        ChronokeepInitialize();
        this.importer = importer;
        this.window = window;
        this.database = database;
        theEvent = database.GetCurrentEvent();
        SheetsBox.IsVisible = false;
        if (importer.Data!.Type == ImportData.FileType.EXCEL)
        {
            SheetsBox.ItemsSource = ((ExcelImporter)importer).SheetNames;
            SheetsBox.SelectedIndex = 0;
            init = false;
            SheetsBox.IsVisible = true;
        }
        page = new ImportFilePage1(importer);
        Frame.Content = page;
        if (!App.IsWindows)
        {
            MainGrid.RowDefinitions =
            [
                new RowDefinition(new GridLength(10)),
                new RowDefinition(new GridLength(1, GridUnitType.Auto)),
                new RowDefinition(new GridLength(1, GridUnitType.Star))
            ];
        }
    }

    public static ImportFileWindow NewWindow(IMainWindow window, IDataImporter importer, IdbInterface database)
    {
        return new ImportFileWindow(window, importer, database);
    }

    private void StartImport(HeaderPart[] headerListBoxItems)
    {
        importer.FetchData();
        keys = new int[HUMAN_FIELDS.Length + 1];
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i] = 0;
        }
        foreach (HeaderPart item in headerListBoxItems)
        {
            Log.D("ImportFileWindow", $"Header is {item.HeaderLabel.Text}");
            if (item.HeaderBox.SelectedIndex != 0)
            {
                keys[item.HeaderBox.SelectedIndex] = item.Index;
            }
        }
        ImportData data = importer.Data!;
        string[] distancesFromFile = data.GetDistanceNames(keys[DISTANCE]);
        if (distancesFromFile.Length <= 0)
        {
            noDistance = true;
            distancesFromFile =
            [
                "",
                ];
        }
        Event? currentEvent = database.GetCurrentEvent();
        if (currentEvent == null || currentEvent.Identifier < 0)
        {
            Log.E("IO.ImportFileWindow", "No event selected.");
            Close();
            return;
        }
        List<Distance> distancesFromDatabase = database.GetDistances(currentEvent.Identifier);
        page = new ImportFilePage2Alt(distancesFromFile, distancesFromDatabase, noDistance);
        Frame.Content = page;
        SheetsBox.IsVisible = false;
        Done.IsEnabled = true;
        Cancel.IsEnabled = true;
    }

    private async void ImportWork(List<ImportDistance> fileDistances)
    {
        try
        {
            // Make sure Age Groups are set properly.
            Dictionary<(int, int), AgeGroup> ageGroups = [];
            Dictionary<int, AgeGroup> lastAgeGroup = [];
            foreach (AgeGroup g in database.GetAgeGroups(theEvent!.Identifier))
            {
                for (int i = g.StartAge; i <= g.EndAge; i++)
                {
                    ageGroups[(g.DistanceId, i)] = g;
                }
                if (lastAgeGroup.TryGetValue(g.DistanceId, out AgeGroup? group) &&
                    group.StartAge >= g.StartAge) continue;
                group = g;
                lastAgeGroup[g.DistanceId] = group;
            }

            await Task.Run(() =>
            {
                ImportData data = importer.Data!;
                int thisYear = DateTime.Parse(theEvent.Date).Year;
                Dictionary<string, Distance> divHashName = [];
                Dictionary<int, Distance> divHashId = [];
                List<Distance> distances = database.GetDistances(theEvent.Identifier);
                foreach (Distance d in distances)
                {
                    divHashName[d.Name.ToLower()] = d;
                    divHashId[d.Identifier] = d;
                }
                bool backYardUltra = Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA == theEvent.EventType;
                Distance? backyardDistance = null;
                // Ensure we don't add more distances for backyard ultra events.
                if (!backYardUltra)
                {
                    bool newDistances = false;
                    foreach (ImportDistance id in fileDistances)
                    {
                        string nameFromFile = id.NameFromFile.ToLower();
                        if (id.DistanceId == -1)
                        {
                            if (divHashName.TryGetValue(nameFromFile, out Distance? dist)) continue;
                            dist = new Distance(id.NameFromFile, theEvent.Identifier);
                            database.AddDistance(dist);
                            dist.Identifier = database.GetDistanceId(dist);
                            divHashName[nameFromFile] = dist;
                            newDistances = true;
                        }
                        else
                        {
                            if (divHashId.TryGetValue(id.DistanceId, out Distance? theDistance))
                            {
                                divHashName[nameFromFile] = theDistance;
                            }
                            else
                            {
                                Log.E("IO.ImportFileWindow", "Distance doesn't exist in the database...");
                            }
                        }
                    }
                    if (newDistances)
                    {
                        window?.UpdateRegistrationDistances();
                    }
                }
                else
                {
                    if (distances.Count > 0)
                    {
                        backyardDistance = distances[0];
                    }
                    else
                    {
                        backyardDistance = new Distance("Backyard", theEvent.Identifier);
                        database.AddDistance(backyardDistance);
                        backyardDistance.Identifier = database.GetDistanceId(backyardDistance);
                        window?.UpdateRegistrationDistances();
                    }
                }
                int numEntries = data.Data.Count;
                importParticipants = [];
                // new distances might have been added
                distances = database.GetDistances(theEvent.Identifier);
                for (int counter = 0; counter < numEntries; counter++)
                {
                    Distance thisDiv = distances[0];
                    if (data.Data[counter][keys[DISTANCE]].Length > 0)
                    {
                        string distName = data.Data[counter][keys[DISTANCE]].ToLower();
                        // Always set distance to our backyard distance if we're importing for a backyard ultra event, otherwise figure out the proper distance.
                        thisDiv = backYardUltra ? backyardDistance! : divHashName[distName];
                    }
                    string birthday = "";
                    int age;
                    if (keys[BIRTHDAY] == 0 && keys[AGE] != 0) // birthday not set but age is
                    {
                        if (int.TryParse(data.Data[counter][keys[AGE]], out age))
                        {
                            birthday = $"{thisYear - age,4}/01/01";
                        }
                    }
                    else if (keys[BIRTHDAY] != 0)
                    {
                        birthday = data.Data[counter][keys[BIRTHDAY]]; // birthday
                    }
                    Participant output = new(
                        data.Data[counter][keys[FIRST]] ?? "", // First Name
                        data.Data[counter][keys[LAST]] ?? "", // Last Name
                        data.Data[counter][keys[STREET]] ?? "", // Street
                        data.Data[counter][keys[CITY]] ?? "", // City
                        data.Data[counter][keys[STATE]] ?? ""   , // State
                        data.Data[counter][keys[ZIP]] ?? "", // Zip
                        birthday, // Birthday
                        new EventSpecific(
                            theEvent.Identifier,
                            thisDiv.Identifier,
                            thisDiv.Name,
                            data.Data[counter][keys[BIB]] ?? "", // Bib number
                            0,                            // checked in
                            data.Data[counter][keys[COMMENTS]] ?? "", // comments
                            data.Data[counter][keys[OWES]] ?? "", // owes
                            data.Data[counter][keys[OTHER]] ?? "", // other
                            (data.Data[counter][keys[ANONYMOUS]] ?? "").Length > 0, // Set Anonymous if anything is in the field
                            false, // always false, this field is no longer used
                            data.Data[counter][keys[APPAREL_ITEM]] ?? "",
                            data.Data[counter][keys[DIVISION]] ?? ""
                        ),
                        data.Data[counter][keys[EMAIL]] ?? "", // email
                        data.Data[counter][keys[PHONE]] ?? "", // phone
                        data.Data[counter][keys[MOBILE]] ?? "", // mobile
                        data.Data[counter][keys[PARENT]] ?? "", // parent
                        data.Data[counter][keys[COUNTRY]] ?? "", // country
                        data.Data[counter][keys[STREET2]] ?? "",  // street2
                        data.Data[counter][keys[GENDER]] ?? "",  // gender
                        data.Data[counter][keys[EMERGENCY_NAME]] ?? "", // Emergency Name
                        data.Data[counter][keys[EMERGENCY_PHONE]] ?? ""  // Emergency Phone
                    );
                    int agDivId = theEvent.CommonAgeGroups ? Constants.Timing.COMMON_AGEGROUPS_DISTANCEID : output.EventSpecific.DistanceIdentifier;
                    age = output.GetAge(theEvent.Date);
                    if (age < 0)
                    {
                        output.EventSpecific.AgeGroupId = Constants.Timing.TIMERESULT_DUMMYAGEGROUP;
                        output.EventSpecific.AgeGroupName = "";
                    }
                    else if (ageGroups.TryGetValue((agDivId, age), out AgeGroup? group))
                    {
                        output.EventSpecific.AgeGroupId = group.GroupId;
                        output.EventSpecific.AgeGroupName = group.PrettyName();
                    }
                    else if (lastAgeGroup.TryGetValue(agDivId, out AgeGroup? lGroup))
                    {
                        output.EventSpecific.AgeGroupId = lGroup.GroupId;
                        output.EventSpecific.AgeGroupName = lGroup.PrettyName();
                    }
                    else
                    {
                        output.EventSpecific.AgeGroupId = Constants.Timing.TIMERESULT_DUMMYAGEGROUP;
                        output.EventSpecific.AgeGroupName = "";
                    }
                    importParticipants.Add(output);
                }
                // Check import participants for multiples.
                existingParticipants = database.GetParticipants(theEvent.Identifier);
                HashSet<Participant> duplicates = [];
                for (int inner = 0; inner < importParticipants.Count; inner++)
                {
                    // Check against everyone currently in the database.
                    foreach (Participant part in existingParticipants.Where(part => importParticipants[inner].Is(part)))
                    {
                        // check if its someone who's already in the database thus we don't need to add to multiples so
                        // we can remove them from the import
                        if ((importParticipants[inner].Bib == part.Bib || importParticipants[inner].Bib.Length < 1 && part.Bib.Length > 0)
                            && importParticipants[inner].Distance.Equals(part.Distance, StringComparison.OrdinalIgnoreCase))
                        {
                            // bib remains the same or isn't set in new import
                            duplicates.Add(importParticipants[inner]);
                        }
                        else if (importParticipants[inner].Bib.Length > 0 && part.Bib.Length < 1
                            && importParticipants[inner].Distance.Equals(part.Distance, StringComparison.OrdinalIgnoreCase))
                        {
                            // bib is an update, add to duplicates so we don't add it again,
                            // then add to list of participants to update
                            duplicates.Add(importParticipants[inner]);
                            updatedParticipants.Add(importParticipants[inner]);
                        }
                    }
                }
                // remove all duplicates from the import
                importParticipants.RemoveAll(duplicates.Contains);
            });
            try
            {
                await Task.Run(() =>
                {
                    Log.D("ImportFileWindow", "Updating participants.");
                    foreach (Participant p in updatedParticipants)
                    {
                        p.Trim();
                        p.FormatData();
                    }
                    database.UpdateParticipants(updatedParticipants);
                    Log.D("ImportFileWindow", "Adding new participants.");
                    foreach (Participant p in importParticipants)
                    {
                        p.Trim();
                        p.FormatData();
                    }
                    database.AddParticipants(importParticipants);
                });
                Log.D("ImportFileWindow", "All done with the import.");
                database.ResetTimingResultsEvent(theEvent!.Identifier);
                window?.NetworkClearResults();
                window?.NotifyTimingWorker();
                Close();
            }
            catch (Exception e)
            {
                Log.E("IO.ImportFileWindow", $"Error processing bib conflicts. ${e}");
            }
        }
        catch (Exception e)
        {
            Log.E("IO.ImportFileWindow", $"Error importing. ${e}");
        }
    }

    internal static int GetHeaderBoxIndex(string s)
    {
        Log.D("ImportFileWindow", $"Looking for a value for: {s}");
        if (s.Contains("First", StringComparison.OrdinalIgnoreCase))
        {
            return FIRST;
        }

        if (s.Contains("Last", StringComparison.OrdinalIgnoreCase))
        {
            return LAST;
        }

        if (s.Contains("Gender", StringComparison.OrdinalIgnoreCase)
            && !s.Contains("Race Group", StringComparison.OrdinalIgnoreCase))
        {
            return GENDER;
        }

        if (string.Equals(s, "Birthday", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "Birthdate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "DOB", StringComparison.OrdinalIgnoreCase)
            || (s.Contains("Date", StringComparison.OrdinalIgnoreCase) && s.Contains("Birth", StringComparison.OrdinalIgnoreCase)))
        {
            return BIRTHDAY;
        }

        if (string.Equals(s, "Street", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "Address", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "Street Address", StringComparison.OrdinalIgnoreCase))
        {
            return STREET;
        }

        if (string.Equals(s, "Street 2", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "Address 2", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "Apartment", StringComparison.OrdinalIgnoreCase))
        {
            return STREET2;
        }

        if (string.Equals(s, "City", StringComparison.OrdinalIgnoreCase))
        {
            return CITY;
        }

        if ((s.Contains("State", StringComparison.OrdinalIgnoreCase)
             || s.Contains("Province", StringComparison.OrdinalIgnoreCase))
            && !s.Contains("Statement", StringComparison.OrdinalIgnoreCase))
        {
            return STATE;
        }

        if (s.Contains("Zip", StringComparison.OrdinalIgnoreCase)
            || s.Contains("Postal Code", StringComparison.OrdinalIgnoreCase))
        {
            return ZIP;
        }

        if (string.Equals(s, "Country", StringComparison.OrdinalIgnoreCase))
        {
            return COUNTRY;
        }

        if (string.Equals(s, "Phone", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "Phone Number", StringComparison.OrdinalIgnoreCase))
        {
            return PHONE;
        }

        if (s.Contains("Mobile", StringComparison.OrdinalIgnoreCase))
        {
            return MOBILE;
        }

        if (s.Contains("Email", StringComparison.OrdinalIgnoreCase))
        {
            return EMAIL;
        }

        if (string.Equals(s, "Parent", StringComparison.OrdinalIgnoreCase))
        {
            return PARENT;
        }

        if ((s.Contains("Bib", StringComparison.OrdinalIgnoreCase)
             || s.Contains("pinney", StringComparison.OrdinalIgnoreCase))
            && !s.Contains("Race Group", StringComparison.OrdinalIgnoreCase))
        {
            return BIB;
        }

        if (
            (s.Contains("Shirt", StringComparison.OrdinalIgnoreCase)
             || s.Contains("Hat", StringComparison.OrdinalIgnoreCase)
             || s.Contains("Fleece", StringComparison.OrdinalIgnoreCase)
             || s.Contains("Apparel", StringComparison.OrdinalIgnoreCase)
             || s.Contains("Hoodie", StringComparison.OrdinalIgnoreCase)
            )
            && !(s.Contains("Quantity", StringComparison.OrdinalIgnoreCase)
                 || s.Contains("Options", StringComparison.OrdinalIgnoreCase)
                 || s.Contains("Details", StringComparison.OrdinalIgnoreCase)
                )
        )
        {
            return APPAREL_ITEM;
        }

        if (string.Equals(s, "Owes", StringComparison.OrdinalIgnoreCase))
        {
            return OWES;
        }

        if (string.Equals(s, "Comments", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "Notes", StringComparison.OrdinalIgnoreCase))
        {
            return COMMENTS;
        }

        if (string.Equals(s, "Other", StringComparison.OrdinalIgnoreCase))
        {
            return OTHER;
        }

        if (s.Contains("Division", StringComparison.OrdinalIgnoreCase))
        {
            return DIVISION;
        }

        if (s.Contains("Distance", StringComparison.OrdinalIgnoreCase)
            || s.Equals("Event", StringComparison.OrdinalIgnoreCase))
        {
            return DISTANCE;
        }

        if ((s.Contains("emergency", StringComparison.OrdinalIgnoreCase) && s.Contains("name", StringComparison.OrdinalIgnoreCase))
            || string.Equals(s, "Emergency", StringComparison.OrdinalIgnoreCase))
        {
            return EMERGENCY_NAME;
        }

        if (s.Contains("emergency", StringComparison.OrdinalIgnoreCase)
            && (s.Contains("phone", StringComparison.OrdinalIgnoreCase) || s.Contains("cell", StringComparison.OrdinalIgnoreCase)))
        {
            return EMERGENCY_PHONE;
        }

        if (string.Equals(s, "Age", StringComparison.OrdinalIgnoreCase))
        {
            return AGE;
        }

        if (string.Equals(s, "Registration Date", StringComparison.OrdinalIgnoreCase))
        {
            return REGISTRATION_DATE;
        }

        if (s.Contains("Anonymous", StringComparison.OrdinalIgnoreCase)
            || s.Contains("Private", StringComparison.OrdinalIgnoreCase))
        {
            return ANONYMOUS;
        }
        return 0;
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        window?.WindowFinalize();
        importer.Finish();
    }

    private void SheetsBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (init) { return; }
        int selection = ((ComboBox)sender!).SelectedIndex;
        Log.D("ImportFileWindow", $"You've selected number {selection}");
        if (page is ImportFilePage1 page1)
        {
            page1.UpdateSheetNo(selection + 1);
        }
    }

    private void Done_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("ImportFileWindow", "Import - Done button clicked.");
        Done.IsEnabled = false;
        Cancel.IsEnabled = false;
        switch (page)
        {
            case ImportFilePage1 page1:
                List<string> repeats = page1.RepeatHeaders();
                List<string> requiredNotFound = page1.RequiredNotFound();
                if (repeats.Count > 0)
                {
                    StringBuilder sb = new("Repeats for the following headers were found:");
                    foreach (string s in repeats)
                    {
                        sb.Append('\n');
                        sb.Append(s);
                    }
                    DialogBox.AsyncShow(sb.ToString());
                }
                else if (requiredNotFound.Count > 0)
                {
                    StringBuilder sb = new("Required fields not found:");
                    foreach (string s in requiredNotFound)
                    {
                        sb.Append('\n');
                        sb.Append(s);
                    }
                    DialogBox.AsyncShow(sb.ToString());
                }
                else
                {
                    Log.D("ImportFileWindow", "No repeat headers found.");
                    try
                    {
                        StartImport(page1.GetListBoxItems());
                    }
                    catch
                    {
                        DialogBox.AsyncShow("Error importing participant data. Please check the file.");
                        Close();
                    }
                }
                break;
            case ImportFilePage2Alt page2:
                Log.D("ImportFileWindow", "Importing participants.");
                ImportWork(page2.GetDistances());
                break;
            default:
                Log.D("ImportFileWindow", "Abort! Abort! Something went terribly wrong.");
                break;
        }
        Done.IsEnabled = true;
        Cancel.IsEnabled = true;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("ImportFileWindow", "Import - Cancel button clicked.");
        Close();
    }

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}