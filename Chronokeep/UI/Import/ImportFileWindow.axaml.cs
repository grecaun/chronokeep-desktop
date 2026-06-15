using Avalonia;
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
using System.Text;
using System.Threading.Tasks;
using static Chronokeep.UI.Import.ImportFilePage2Alt;

namespace Chronokeep.UI.Import;

public partial class ImportFileWindow : Window
{
    private readonly IDataImporter importer;
    private readonly IMainWindow? window = null;
    private readonly IDBInterface database;
    private readonly bool init = true;
    internal static readonly string[] human_fields = [
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
    internal static readonly int AGE = 1;
    internal static readonly int ANONYMOUS = 2;
    internal static readonly int APPARELITEM = 3;
    internal static readonly int BIB = 4;
    internal static readonly int BIRTHDAY = 5;
    internal static readonly int CITY = 6;
    internal static readonly int COMMENTS = 7;
    internal static readonly int COUNTRY = 8;
    internal static readonly int DISTANCE = 9;
    internal static readonly int DIVISION = 10;
    internal static readonly int EMAIL = 11;
    internal static readonly int EMERGENCYNAME = 12;
    internal static readonly int EMERGENCYPHONE = 13;
    internal static readonly int FIRST = 14;
    internal static readonly int GENDER = 15;
    internal static readonly int LAST = 16;
    internal static readonly int MOBILE = 17;
    internal static readonly int OTHER = 18;
    internal static readonly int OWES = 19;
    internal static readonly int PARENT = 20;
    internal static readonly int PHONE = 21;
    internal static readonly int REGDATE = 22;
    internal static readonly int STATE = 23;
    internal static readonly int STREET = 24;
    internal static readonly int STREET2 = 25;
    internal static readonly int ZIP = 26;
    private UserControl? page = null;
    private int[] keys = [];

    private bool no_distance = false;

    private readonly Event? theEvent;

    /**
     * VERIFICATION VARIABLES
     */
    private List<Participant> existingParticipants = [];
    private List<Participant> importParticipants = [];
    private readonly List<Participant> updatedParticipants = [];
    private readonly List<Participant> existingToRemoveParticipants = [];

    public ImportFileWindow(IMainWindow? window, IDataImporter importer, IDBInterface database)
    {
        InitializeComponent();
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
    }

    public static ImportFileWindow NewWindow(IMainWindow window, IDataImporter importer, IDBInterface database)
    {
        return new ImportFileWindow(window, importer, database);
    }

    private void StartImport(HeaderPart[] headerListBoxItems)
    {
        importer.FetchData();
        keys = new int[human_fields.Length + 1];
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i] = 0;
        }
        foreach (HeaderPart item in headerListBoxItems)
        {
            Log.D("ImportFileWindow", "Header is " + item.HeaderLabel.Text);
            if (item.HeaderBox.SelectedIndex != 0)
            {
                keys[item.HeaderBox.SelectedIndex] = item.Index;
            }
        }
        ImportData data = importer.Data!;
        string[] distancesFromFile = data.GetDistanceNames(keys[DISTANCE]);
        if (distancesFromFile.Length <= 0)
        {
            no_distance = true;
            distancesFromFile =
            [
                "",
                ];
        }
        Event theEvent = database.GetCurrentEvent()!;
        if (theEvent == null || theEvent.Identifier < 0)
        {
            Log.E("IO.ImportFileWindow", "No event selected.");
            this.Close();
        }
        List<Distance> distancesFromDatabase = database.GetDistances(theEvent!.Identifier);
        page = new ImportFilePage2Alt(distancesFromFile, distancesFromDatabase, no_distance);
        Frame.Content = page;
        SheetsBox.IsVisible = false;
        Done.IsEnabled = true;
        Cancel.IsEnabled = true;
    }

    private async void ImportWork(List<ImportDistance> fileDistances)
    {
        // Make sure Age Groups are set properly.
        Dictionary<(int, int), AgeGroup> AgeGroups = [];
        Dictionary<int, AgeGroup> LastAgeGroup = [];
        foreach (AgeGroup g in database.GetAgeGroups(theEvent!.Identifier))
        {
            for (int i = g.StartAge; i <= g.EndAge; i++)
            {
                AgeGroups[(g.DistanceId, i)] = g;
            }
            if (!LastAgeGroup.TryGetValue(g.DistanceId, out AgeGroup? group) || group.StartAge < g.StartAge)
            {
                group = g;
                LastAgeGroup[g.DistanceId] = group;
            }
        }

        HashSet<Participant> multiples = [];
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
            bool BackYardUltra = Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA == theEvent.EventType;
            Distance? theDistance;
            Distance? backyardDistance = null;
            // Ensure we don't add more distances for backyard ultra events.
            if (!BackYardUltra)
            {
                bool newDistances = false;
                foreach (ImportDistance id in fileDistances)
                {
                    string nameFromFile = id.NameFromFile.ToLower();
                    if (id.DistanceId == -1)
                    {
                        if (divHashName.TryGetValue(nameFromFile, out Distance? dist))
                        {
                            theDistance = dist;
                        }
                        else
                        {
                            dist = new(id.NameFromFile, theEvent.Identifier);
                            database.AddDistance(dist);
                            dist.Identifier = database.GetDistanceID(dist);
                            divHashName[nameFromFile] = dist;
                            newDistances = true;
                        }
                    }
                    else
                    {
                        if (divHashId.TryGetValue(id.DistanceId, out theDistance))
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
                    backyardDistance = new("Backyard", theEvent.Identifier);
                    database.AddDistance(backyardDistance);
                    backyardDistance.Identifier = database.GetDistanceID(backyardDistance);
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
                if (data.Data[counter][keys[DISTANCE]] != null && data.Data[counter][keys[DISTANCE]].Length > 0)
                {
                    string distName = data.Data[counter][keys[DISTANCE]].ToLower();
                    // Always set distance to our backyard distance if we're importing for a backyard ultra event. Otherwise figure out the proper distance.
                    thisDiv = BackYardUltra ? backyardDistance! : divHashName[distName];
                }
                string birthday = "";
                int age = -1;
                if (keys[BIRTHDAY] == 0 && keys[AGE] != 0) // birthday not set but age is
                {
                    try
                    {
                        if (int.TryParse(data.Data[counter][keys[AGE]], out age))
                        {
                            birthday = string.Format("{0,4}/01/01", thisYear - age);
                        }
                    }
                    catch { }
                }
                else if (keys[BIRTHDAY] != 0)
                {
                    birthday = data.Data[counter][keys[BIRTHDAY]]; // birthday
                }
                Participant output = new(
                    data.Data[counter][keys[FIRST]], // First Name
                    data.Data[counter][keys[LAST]], // Last Name
                    data.Data[counter][keys[STREET]], // Street
                    data.Data[counter][keys[CITY]], // City
                    data.Data[counter][keys[STATE]], // State
                    data.Data[counter][keys[ZIP]], // Zip
                    birthday, // Birthday
                    new(
                        theEvent.Identifier,
                        thisDiv.Identifier,
                        thisDiv.Name,
                        data.Data[counter][keys[BIB]], // Bib number
                        0,                            // checked in
                        data.Data[counter][keys[COMMENTS]], // comments
                        data.Data[counter][keys[OWES]], // owes
                        data.Data[counter][keys[OTHER]], // other
                        data.Data[counter][keys[ANONYMOUS]] != null && data.Data[counter][keys[ANONYMOUS]].Trim().Length > 0, // Set Anonymous if anything is in the field
                        false, // always false, this field is no longer used
                        data.Data[counter][keys[APPARELITEM]],
                        data.Data[counter][keys[DIVISION]]
                        ),
                    data.Data[counter][keys[EMAIL]], // email
                    data.Data[counter][keys[PHONE]], // phone
                    data.Data[counter][keys[MOBILE]], // mobile
                    data.Data[counter][keys[PARENT]], // parent
                    data.Data[counter][keys[COUNTRY]], // country
                    data.Data[counter][keys[STREET2]],  // street2
                    data.Data[counter][keys[GENDER]] ?? "",  // gender
                    data.Data[counter][keys[EMERGENCYNAME]], // Emergency Name
                    data.Data[counter][keys[EMERGENCYPHONE]]  // Emergency Phone
                    );
                int agDivId = theEvent.CommonAgeGroups ? Constants.Timing.COMMON_AGEGROUPS_DISTANCEID : output.EventSpecific.DistanceIdentifier;
                age = output.GetAge(theEvent.Date);
                if (age < 0)
                {
                    output.EventSpecific.AgeGroupId = Constants.Timing.TIMERESULT_DUMMYAGEGROUP;
                    output.EventSpecific.AgeGroupName = "";
                }
                else if (AgeGroups.TryGetValue((agDivId, age), out AgeGroup? group))
                {
                    output.EventSpecific.AgeGroupId = group.GroupId;
                    output.EventSpecific.AgeGroupName = group.PrettyName();
                }
                else if (LastAgeGroup.TryGetValue(agDivId, out AgeGroup? lGroup))
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
            /**
             * 
             * VERIFICATION CODE
             * 
             */
            // Check import participants for multiples.
            existingParticipants = database.GetParticipants(theEvent.Identifier);
            HashSet<Participant> duplicatesImport = [];
            for (int inner = 0; inner < importParticipants.Count; inner++)
            {
                // Check against others imported
                for (int outer = inner + 1; outer < importParticipants.Count; outer++)
                {
                    //Log.D("ImportFileWindow", string.Format("inner {1} outer {0}", outer, inner));
                    if (importParticipants[inner].Is(importParticipants[outer]))
                    {
                        // if they're a duplicate and not just a multiple
                        if (importParticipants[inner].Bib == importParticipants[outer].Bib
                            && importParticipants[inner].Distance.Equals(importParticipants[outer].Distance, StringComparison.OrdinalIgnoreCase))
                        {
                            duplicatesImport.Add(importParticipants[inner]);
                        }
                        else
                        {
                            multiples.Add(importParticipants[inner]);
                            multiples.Add(importParticipants[outer]);
                        }
                    }
                }
                // Check against everyone currently in the database.
                foreach (Participant part in existingParticipants)
                {
                    if (importParticipants[inner].Is(part))
                    {
                        // check if its someone who's already in the database thus we don't need to add to multiples and
                        // we can remove them from the import
                        if ((importParticipants[inner].Bib == part.Bib || importParticipants[inner].Bib.Length < 1 && part.Bib.Length > 0)
                            && importParticipants[inner].Distance.Equals(part.Distance, StringComparison.OrdinalIgnoreCase))
                        {
                            // bib remains the same or isn't set in new import
                            duplicatesImport.Add(importParticipants[inner]);
                        }
                        else if (importParticipants[inner].Bib.Length > 0 && part.Bib.Length < 1
                            && importParticipants[inner].Distance.Equals(part.Distance, StringComparison.OrdinalIgnoreCase))
                        {
                            // bib is an update, add to duplicates so we don't add it again,
                            // then add to list of participants to update
                            duplicatesImport.Add(importParticipants[inner]);
                            updatedParticipants.Add(importParticipants[inner]);
                        }
                        else
                        {
                            multiples.Add(importParticipants[inner]);
                            multiples.Add(part);
                        }
                    }
                }
            }
            // Remove anyone that was deemed a duplicate
            // This can happen if there was an X, Y, and Z in the import where X and Y are duplicates
            // but Z is the same person with diff bib/distance.
            // This can also happen if there are X and Z in the import but Y in the database,
            // where the situation is as above
            foreach (Participant dup in duplicatesImport)
            {
                multiples.Remove(dup);
            }
            // remove all duplicates from the import
            importParticipants.RemoveAll(x => duplicatesImport.Contains(x));
        });
        // if we have multiples to mess around with display the page
        if (multiples.Count > 0)
        {
            page = new ImportFilePageConflicts([.. multiples], theEvent);
            Frame.Content = page;
            Done.IsEnabled = true;
            Cancel.IsEnabled = true;
        }
        // otherwise process the multiples (none)
        else
        {
            ProcessMultiplestoRemove([]);
        }
    }

    private async void ProcessMultiplestoRemove(List<Participant> toRemove)
    {
        List<Participant> conflicts = [];
        await Task.Run(() =>
        {
            Dictionary<string, HashSet<Participant>> BibConflictsDict = [];
            Dictionary<string, Participant> ExistingParticipantsDict = [];
            // keep track of who we need to tell the database to remove
            existingToRemoveParticipants.AddRange(toRemove);
            existingToRemoveParticipants.RemoveAll(x => importParticipants.Contains(x));
            // Remove those we didn't select to keep from our lists.
            existingParticipants.RemoveAll(x => toRemove.Contains(x));
            importParticipants.RemoveAll(x => toRemove.Contains(x));
            foreach (Participant existing in existingParticipants)
            {
                ExistingParticipantsDict[existing.Bib] = existing;
            }
            foreach (Participant import in importParticipants)
            {
                import.FormatData();
                // this is checking for bib repeats, so check if we're actually checking a specified bib
                if (import.Bib.Length > 0 && ExistingParticipantsDict.TryGetValue(import.Bib, out Participant? part))
                {
                    part.FormatData();
                    if (!part.Is(import))
                    {
                        Log.D("ImportFileWindow",
                            string.Format("We've found \n'{0}' '{1}' '{5}' '{7}' '{9}'\n'{2}' '{3}' '{6}' '{8}' '{10}'\nfor bib '{4}'",
                            import.FirstName,
                            import.LastName,
                            ExistingParticipantsDict[import.Bib].FirstName,
                            ExistingParticipantsDict[import.Bib].LastName,
                            import.Bib,
                            import.Street,
                            ExistingParticipantsDict[import.Bib].Street,
                            import.Zip,
                            ExistingParticipantsDict[import.Bib].Zip,
                            import.Birthdate,
                            ExistingParticipantsDict[import.Bib].Birthdate
                            ));
                        if (!BibConflictsDict.TryGetValue(import.Bib, out HashSet<Participant>? bibConflictSet))
                        {
                            bibConflictSet = [];
                            BibConflictsDict[import.Bib] = bibConflictSet;
                        }
                        bibConflictSet.Add(import);
                        bibConflictSet.Add(ExistingParticipantsDict[import.Bib]);
                    }
                }
            }
            foreach (string bib in BibConflictsDict.Keys)
            {
                conflicts.AddRange(BibConflictsDict[bib]);
            }
        });
        // if we have multiples to mess around with display the page
        if (conflicts.Count > 0)
        {
            page = new ImportFilePageConflicts(conflicts, theEvent!);
            Frame.Content = page;
            Done.IsEnabled = true;
            Cancel.IsEnabled = true;
        }
        // otherwise process the multiples (none)
        else
        {
            ProcessBibConflicts([]);
        }
    }

    private async void ProcessBibConflicts(List<Participant> toRemove)
    {
        await Task.Run(() =>
        {
            // keep track of who we need to tell the database to get rid of
            existingToRemoveParticipants.AddRange(toRemove);
            existingToRemoveParticipants.RemoveAll(x => importParticipants.Contains(x));
            // Remove those we didn't select to keep from our import list
            // no need to remove from the existing because we're not re-adding those
            importParticipants.RemoveAll(x => toRemove.Contains(x));
            Log.D("ImportFileWindow", "Removing old participants we were told to.");
            database.RemoveParticipantEntries(existingToRemoveParticipants);
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
        this.Close();
    }

    internal static int GetHeaderBoxIndex(string s)
    {
        Log.D("ImportFileWindow", "Looking for a value for: " + s);
        if (s.Contains("First", StringComparison.OrdinalIgnoreCase))
        {
            return FIRST;
        }
        else if (s.Contains("Last", StringComparison.OrdinalIgnoreCase))
        {
            return LAST;
        }
        else if (s.Contains("Gender", StringComparison.OrdinalIgnoreCase)
            && !s.Contains("Race Group", StringComparison.OrdinalIgnoreCase))
        {
            return GENDER;
        }
        else if (string.Equals(s, "Birthday", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "Birthdate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "DOB", StringComparison.OrdinalIgnoreCase)
            || (s.Contains("Date", StringComparison.OrdinalIgnoreCase) && s.Contains("Birth", StringComparison.OrdinalIgnoreCase)))
        {
            return BIRTHDAY;
        }
        else if (string.Equals(s, "Street", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "Address", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "Street Address", StringComparison.OrdinalIgnoreCase))
        {
            return STREET;
        }
        else if (string.Equals(s, "Street 2", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "Address 2", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "Apartment", StringComparison.OrdinalIgnoreCase))
        {
            return STREET2;
        }
        else if (string.Equals(s, "City", StringComparison.OrdinalIgnoreCase))
        {
            return CITY;
        }
        else if ((s.Contains("State", StringComparison.OrdinalIgnoreCase)
            || s.Contains("Province", StringComparison.OrdinalIgnoreCase))
            && !s.Contains("Statement", StringComparison.OrdinalIgnoreCase))
        {
            return STATE;
        }
        else if (s.Contains("Zip", StringComparison.OrdinalIgnoreCase)
            || s.Contains("Postal Code", StringComparison.OrdinalIgnoreCase))
        {
            return ZIP;
        }
        else if (string.Equals(s, "Country", StringComparison.OrdinalIgnoreCase))
        {
            return COUNTRY;
        }
        else if (string.Equals(s, "Phone", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "Phone Number", StringComparison.OrdinalIgnoreCase))
        {
            return PHONE;
        }
        else if (s.Contains("Mobile", StringComparison.OrdinalIgnoreCase))
        {
            return MOBILE;
        }
        else if (s.Contains("Email", StringComparison.OrdinalIgnoreCase))
        {
            return EMAIL;
        }
        else if (string.Equals(s, "Parent", StringComparison.OrdinalIgnoreCase))
        {
            return PARENT;
        }
        else if ((s.Contains("Bib", StringComparison.OrdinalIgnoreCase)
            || s.Contains("pinney", StringComparison.OrdinalIgnoreCase))
            && !s.Contains("Race Group", StringComparison.OrdinalIgnoreCase))
        {
            return BIB;
        }
        else if (
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
            return APPARELITEM;
        }
        else if (string.Equals(s, "Owes", StringComparison.OrdinalIgnoreCase))
        {
            return OWES;
        }
        else if (string.Equals(s, "Comments", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "Notes", StringComparison.OrdinalIgnoreCase))
        {
            return COMMENTS;
        }
        else if (string.Equals(s, "Other", StringComparison.OrdinalIgnoreCase))
        {
            return OTHER;
        }
        else if (s.Contains("Division", StringComparison.OrdinalIgnoreCase))
        {
            return DIVISION;
        }
        else if (s.Contains("Distance", StringComparison.OrdinalIgnoreCase)
            || s.Equals("Event", StringComparison.OrdinalIgnoreCase))
        {
            return DISTANCE;
        }
        else if ((s.Contains("emergency", StringComparison.OrdinalIgnoreCase) && s.Contains("name", StringComparison.OrdinalIgnoreCase))
            || string.Equals(s, "Emergency", StringComparison.OrdinalIgnoreCase))
        {
            return EMERGENCYNAME;
        }
        else if (s.Contains("emergency", StringComparison.OrdinalIgnoreCase)
            && (s.Contains("phone", StringComparison.OrdinalIgnoreCase) || s.Contains("cell", StringComparison.OrdinalIgnoreCase)))
        {
            return EMERGENCYPHONE;
        }
        else if (string.Equals(s, "Age", StringComparison.OrdinalIgnoreCase))
        {
            return AGE;
        }
        else if (string.Equals(s, "Registration Date", StringComparison.OrdinalIgnoreCase))
        {
            return REGDATE;
        }
        else if (s.Contains("Anonymous", StringComparison.OrdinalIgnoreCase)
            || s.Contains("Private", StringComparison.OrdinalIgnoreCase))
        {
            return ANONYMOUS;
        }
        return 0;
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        window?.WindowFinalize(this);
        importer.Finish();
    }

    private void SheetsBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (init) { return; }
        int selection = ((ComboBox)sender!).SelectedIndex;
        Log.D("ImportFileWindow", "You've selected number " + selection);
        if (page != null && page is ImportFilePage1 page1)
        {
            page1.UpdateSheetNo(selection + 1);
        }
    }

    private void Done_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("ImportFileWindow", "Import - Done button clicked.");
        Done.IsEnabled = false;
        Cancel.IsEnabled = false;
        if (page != null && page is ImportFilePage1 page1)
        {
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
                DialogBox.Show(sb.ToString());
            }
            else if (requiredNotFound.Count > 0)
            {
                StringBuilder sb = new("Required fields not found:");
                foreach (string s in requiredNotFound)
                {
                    sb.Append('\n');
                    sb.Append(s);
                }
                DialogBox.Show(sb.ToString());
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
                    DialogBox.Show("Error importing participant data. Please check the file.");
                    Close();
                }
            }
        }
        else if (page != null && page is ImportFilePage2Alt page2)
        {
            Log.D("ImportFileWindow", "Importing participants.");
            ImportWork(page2.GetDistances());
        }
        else if (page != null && page is ImportFilePageConflicts multiplesPage)
        {
            Log.D("ImportFileWindow", "Processing multiples to keep/remove.");
            ProcessMultiplestoRemove(multiplesPage.GetParticipantsToRemove());
        }
        else if (page != null && page is ImportFilePageConflicts bibConflictsPage)
        {
            Log.D("ImportFileWindow", "Processing bib conflicts to remove.");
            ProcessBibConflicts(bibConflictsPage.GetParticipantsToRemove());
        }
        else
        {
            Log.D("ImportFileWindow", "Abort! Abort! Something went terribly wrong.");
        }
        Done.IsEnabled = true;
        Cancel.IsEnabled = true;
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("ImportFileWindow", "Import - Cancel button clicked.");
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