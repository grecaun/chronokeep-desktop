using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.UI.MainPages;
using Chronokeep.UI.Util;
using System;
using System.Collections.Generic;

namespace Chronokeep.UI.Participants;

public partial class ModifyParticipantWindow : Window
{
    private readonly IMainWindow? window;
    private readonly IDBInterface database;
    private readonly TimingPage? tPage;
    private readonly Event? theEvent;
    private readonly Participant? person;

    private bool ParticipantChanged = false;

    public ModifyParticipantWindow(IMainWindow window, IDBInterface database, Participant? person)
    {
        InitializeComponent();
        this.window = window;
        this.tPage = null;
        this.database = database;
        this.person = person;
        theEvent = database.GetCurrentEvent();
        if (theEvent == null)
        {
            DialogBox.Show("Unable to get event.");
            this.Close();
        }
        if (person == null)
        {
            Add.Click += Add_Click;
            UpdateDistances();
        }
        else
        {
            Add.Click += Modify_Click;
            UpdateAllFields();
        }
        BibBox.Focus();
    }

    public ModifyParticipantWindow(TimingPage tPage, IDBInterface database, int EventSpecificId, string Bib)
    {
        InitializeComponent();
        this.window = null;
        this.tPage = tPage;
        this.database = database;
        theEvent = database.GetCurrentEvent();
        if (theEvent == null)
        {
            DialogBox.Show("Unable to get event.");
            this.Close();
        }
        person = database.GetParticipantEventSpecific(theEvent!.Identifier, EventSpecificId);
        if (person == null)
        {
            BibBox.Text = Bib;
            Add.Click += Add_Click;
            UpdateDistances();
        }
        else
        {
            Add.Click += Modify_Click;
            UpdateAllFields();
        }
        BibBox.IsEnabled = false;
    }

    public static ModifyParticipantWindow NewWindow(IMainWindow window, IDBInterface database, Participant? person = null)
    {
        return new(window, database, person);
    }

    private void UpdateDistances()
    {
        if (theEvent == null || theEvent.Identifier < 0)
            return;
        List<Distance> divs = database.GetDistances(theEvent.Identifier);
        DistanceBox.Items.Clear();
        divs.Sort();
        foreach (Distance d in divs)
        {
            DistanceBox.Items.Add(new ComboBoxItem()
            {
                Content = d.Name,
                Tag = d.Identifier.ToString()
            });
        }
        DistanceBox.SelectedIndex = 0;
        List<string> divisions = database.GetDivisions(theEvent.Identifier);
        divisions.RemoveAll(x => x.Trim().Length < 1);
        divisions.Sort((x, y) => x.CompareTo(y));
        DivisionBox.ItemsSource = divisions;
    }

    private void UpdateAllFields()
    {
        if (person == null || theEvent == null || theEvent.Identifier < 0)
        {
            return;
        }
        List<Distance> divs = database.GetDistances(theEvent.Identifier);
        DistanceBox.Items.Clear();
        divs.Sort();
        ComboBoxItem? selected = null;
        foreach (Distance d in divs)
        {
            ComboBoxItem item = new()
            {
                Content = d.Name,
                Tag = d.Identifier.ToString()
            };
            if (d.Identifier == person.EventSpecific.DistanceIdentifier)
            {
                selected = item;
            }
            DistanceBox.Items.Add(item);
        }
        DistanceBox.SelectedItem = selected;
        BibBox.Text = person.Bib.ToString();
        FirstBox.Text = person.FirstName;
        LastBox.Text = person.LastName;
        BirthdayBox.Text = DateTime.Parse(person.Birthdate).ToString("MM/dd/yyyy");
        AgeBox.Text = person.Age(theEvent.Date);
        bool genderFound = false;
        ComboBoxItem? otherBoxItem = null, notSpecifiedBoxItem = null;
        foreach (object? item in GenderBox.Items)
        {
            if (item is ComboBoxItem cbi)
            {
                if (person.Gender.Equals(cbi!.Content!.ToString()))
                {
                    GenderBox.SelectedItem = cbi;
                    genderFound = true;
                }
                if (cbi.Content.ToString() == "Not Specified")
                {
                    notSpecifiedBoxItem = cbi;
                }
                else if (cbi.Content.ToString() == "Other")
                {
                    otherBoxItem = cbi;
                }
            }
        }
        if (person.Gender.Equals("NS", StringComparison.OrdinalIgnoreCase))
        {
            GenderBox.SelectedItem = notSpecifiedBoxItem;
            genderFound = true;
        }
        if (!genderFound)
        {
            GenderBox.SelectedItem = otherBoxItem;
            otherGenderBox.Text = person.Gender;
            ShowOtherGender();
        }
        else
        {
            DismissOtherGender();
        }
        StreetBox.Text = person.Street;
        Street2Box.Text = person.Street2;
        CityBox.Text = person.City;
        StateBox.Text = person.State;
        ZipBox.Text = person.Zip;
        CountryBox.Text = person.Country;
        EmailBox.Text = person.Email;
        PhoneBox.Text = person.Phone;
        MobileBox.Text = person.Mobile;
        ParentBox.Text = person.Parent;
        CommentsBox.Text = person.Comments;
        ECNameBox.Text = person.ECName;
        ECPhoneBox.Text = person.ECPhone;
        AnonymousBox.IsChecked = person.Anonymous;
        ApparelBox.Text = person.EventSpecific.Apparel;
        DivisionBox.Text = person.EventSpecific.Division;
        Add.Content = "Update";
        Done.Content = "Cancel";
        List<string> divisions = database.GetDivisions(theEvent.Identifier);
        divisions.RemoveAll(x => x.Trim().Length < 1);
        divisions.Sort((x, y) => x.CompareTo(y));
        DivisionBox.ItemsSource = divisions;
    }

    private void Clear()
    {
        DistanceBox.SelectedItem = 0;
        BibBox.Text = "";
        FirstBox.Text = "";
        LastBox.Text = "";
        AgeBox.Text = "";
        GenderBox.SelectedIndex = 0;
        otherGenderBox.Text = "";
        StreetBox.Text = "";
        Street2Box.Text = "";
        CityBox.Text = "";
        StateBox.Text = "";
        ZipBox.Text = "";
        CountryBox.Text = "";
        EmailBox.Text = "";
        PhoneBox.Text = "";
        MobileBox.Text = "";
        ParentBox.Text = "";
        CommentsBox.Text = "";
        ECNameBox.Text = "";
        ECPhoneBox.Text = "";
        AnonymousBox.IsChecked = false;
        ApparelBox.Text = "";
        DivisionBox.Text = "";
        List<string> divisions = database.GetDivisions(theEvent!.Identifier);
        divisions.RemoveAll(x => x.Trim().Length < 1);
        divisions.Sort((x, y) => x.CompareTo(y));
        DivisionBox.ItemsSource = divisions;
    }

    private void ShowOtherGender()
    {
        otherGenderBox?.IsVisible = true;
    }

    private void DismissOtherGender()
    {
        otherGenderBox?.IsVisible = false;
    }

    private Participant? FromFields()
    {
        if (theEvent == null || theEvent.Identifier < 0)
        {
            return null;
        }
        int eventSpecificId = -1, participantId = -1;
        if (person != null)
        {
            eventSpecificId = person.EventSpecific.Identifier;
            participantId = person.Identifier;
        }
        string gender = "Not Specified";
        if (GenderBox.SelectedItem != null)
        {
            gender = ((ComboBoxItem)GenderBox.SelectedItem).Content!.ToString()!;
        }
        if (gender.Equals("Other", StringComparison.OrdinalIgnoreCase))
        {
            gender = otherGenderBox.Text!;
            if (gender.Length < 1)
            {
                gender = "Not Specified";
            }
        }
        int checkedin = 0;
        if (!int.TryParse(AgeBox.Text, out int age))
        {
            age = 0;
        }
        string birthdate = BirthdayBox.Text!.Replace('_', '0');
        if (age != 0 && birthdate.Length < 1)
        {
            if (!int.TryParse(theEvent.Date.Split('/')[2], out int year))
            {
                year = 0;
            }
            year = year < 1969 ? DateTime.Now.Year : year;
            birthdate = "1/1/" + (year - age);
        }
        Log.D("UI.Participants.ModifyParticipantWindow", "----- Birthdate -----" + birthdate);
        Participant output = new(
            participantId,
            FirstBox.Text!,
            LastBox.Text!,
            StreetBox.Text!,
            CityBox.Text!,
            StateBox.Text!,
            ZipBox.Text!,
            birthdate,
            new EventSpecific(
                eventSpecificId,
                theEvent.Identifier,
                Convert.ToInt32(((ComboBoxItem)DistanceBox.SelectedItem!).Tag),
                "",
                BibBox.Text!,
                checkedin,
                CommentsBox.Text!,
                "",
                "",
                Constants.Timing.EVENTSPECIFIC_UNKNOWN,
                "",
                Constants.Timing.TIMERESULT_DUMMYAGEGROUP,
                AnonymousBox.IsChecked == true,
                false,
                ApparelBox.Text!,
                DivisionBox.Text!,
                Constants.Timing.EVENTSPECIFIC_DEFAULT_VERSION,
                Constants.Timing.EVENTSPECIFIC_DEFAULT_UPLOADED_VERSION
                ),
            EmailBox.Text!,
            PhoneBox.Text!,
            MobileBox.Text!,
            ParentBox.Text!,
            CountryBox.Text!,
            Street2Box.Text!,
            gender,
            ECNameBox.Text!,
            ECPhoneBox.Text!
            );
        age = output.GetAge(theEvent.Date);
        Dictionary<(int, int), AgeGroup> AgeGroups = [];
        Dictionary<int, AgeGroup> LastAgeGroup = [];
        foreach (AgeGroup g in database.GetAgeGroups(theEvent.Identifier))
        {
            for (int i = g.StartAge; i <= g.EndAge; i++)
            {
                AgeGroups[(g.DistanceId, i)] = g;
            }
            if (!LastAgeGroup.TryGetValue(g.DistanceId, out AgeGroup? oAgeGrp) || oAgeGrp.StartAge < g.StartAge)
            {
                LastAgeGroup[g.DistanceId] = g;
            }
        }
        int agDivId = theEvent.CommonAgeGroups ? Constants.Timing.COMMON_AGEGROUPS_DISTANCEID : output.EventSpecific.DistanceIdentifier;
        if (AgeGroups == null || age < 0)
        {
            Log.D("UI.Participants.ModifyParticipantWindow", "Age Groups not found or Age is less than 0.");
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
            Log.D("UI.Participants.ModifyParticipantWindow", "Age Group not found.");
            output.EventSpecific.AgeGroupId = Constants.Timing.TIMERESULT_DUMMYAGEGROUP;
            output.EventSpecific.AgeGroupName = "";
        }
        return output;
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (ParticipantChanged)
        {
            database.ResetTimingResultsEvent(theEvent!.Identifier);
            window?.NotifyTimingWorker();
            if (tPage != null)
            {
                tPage.DatasetChanged();
                tPage.UpdateView();
                tPage.NotifyTimingWorker();
            }
        }
        window?.WindowFinalize(this);
    }

    private void Box_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            Add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
    }

    private void GenderBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        string selectedGender = ((ComboBoxItem)GenderBox.SelectedItem!).Content!.ToString()!;
        if (selectedGender.Equals("Other", StringComparison.OrdinalIgnoreCase))
        {
            ShowOtherGender();
        }
        else
        {
            DismissOtherGender();
        }
    }

    private void Done_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Participants.ModifyParticipantWindow", "Done clicked.");
        this.Close();
    }

    private void Modify_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Participants.ModifyParticipantWindow", "Modify clicked.");
        Participant newPart = FromFields()!;
        // Copy old Version values when modifying.
        newPart!.EventSpecific.Version = person!.EventSpecific.Version;
        newPart.EventSpecific.UploadedVersion = person.EventSpecific.UploadedVersion;
        Participant? offendingBib = null;
        // If bib isn't empty and isn't the dummybib, offer to swap bibs.
        if (newPart.Bib.Length > 0 && newPart.Bib != Constants.Timing.CHIPREAD_DUMMYBIB)
        {
            offendingBib = database.GetParticipantBib(theEvent!.Identifier, newPart.Bib);
        }
        if (offendingBib != null && newPart.Identifier != offendingBib.Identifier)
        {
            // bib is taken - person object holds old bib #
            bool ModifyBibs = false;
            DialogBox.Show(
                "This bib is already taken. Swap bibs?",
                "Yes",
                "No",
                () =>
                {
                    ModifyBibs = true;
                    offendingBib.EventSpecific.Bib = person.EventSpecific.Bib;
                    string newBib = newPart.EventSpecific.Bib;
                    newPart.EventSpecific.Bib = Constants.Timing.CHIPREAD_DUMMYBIB;
                    // Both participants are being updated, so increment their version numbers.
                    newPart.EventSpecific.Version += 1;
                    offendingBib.EventSpecific.Version += 1;
                    //database.UpdateParticipant(newPart);
                    database.UpdateParticipant(offendingBib);
                    newPart.EventSpecific.Bib = newBib;
                    database.UpdateParticipant(newPart);
                    ParticipantChanged = true;
                    this.Close();
                });
            if (!ModifyBibs)
            {
                BibBox.Text = person.EventSpecific.Bib.ToString();
            }
        }
        else
        {
            if (newPart != null)
            {
                Log.D("UI.Participants.ModifyParticipantWindow", "NewPart not null ---- Should update --- NewPart birthdate ----" + newPart.Birthdate);
                // New Part has information that doesn't match the old participant.
                // so increment the version
                if (!newPart.Matches(person))
                {
                    newPart.EventSpecific.Version += 1;
                }
                database.UpdateParticipant(newPart);
                if (newPart.Bib != Constants.Timing.CHIPREAD_DUMMYBIB)
                {
                    ParticipantChanged = true;
                }
                this.Close();
            }
        }
    }

    private void Add_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Participants.ModifyParticipantWindow", "Add clicked.");
        if (person != null && person.Bib != Constants.Timing.CHIPREAD_DUMMYBIB)
        {
            ParticipantChanged = true;
        }
        Participant newPart = FromFields()!;
        Participant? offendingBib = null;
        // If bib isn't empty and isn't the dummybib, offer to remove bib from old participant.
        if (newPart.Bib.Length > 0 && newPart.Bib != Constants.Timing.CHIPREAD_DUMMYBIB)
        {
            offendingBib = database.GetParticipantBib(theEvent!.Identifier, newPart.Bib);
        }
        if (offendingBib != null)
        {
            // bib is taken
            DialogBox.Show(
                "This bib is already taken. Assign no bib to the previous bib owner?",
                "Yes",
                "No",
                () =>
                {
                    if (newPart != null)
                    {
                        if (newPart.FirstName.Trim().Length < 1 && newPart.LastName.Trim().Length < 1)
                        {
                            DialogBox.Show("Invalid name given.");
                            return;
                        }
                        // only update the participant with the old bib if we're actually adding the person
                        // but also make sure to increment their version because they were in fact updated
                        offendingBib.EventSpecific.Bib = Constants.Timing.CHIPREAD_DUMMYBIB;
                        offendingBib.EventSpecific.Version += 1;
                        database.UpdateParticipant(offendingBib);
                        database.AddParticipant(newPart);
                        if (newPart.Bib != Constants.Timing.CHIPREAD_DUMMYBIB)
                        {
                            ParticipantChanged = true;
                        }
                    }
                    Clear();
                    BibBox.Focus();
                });
        }
        else
        {
            if (newPart != null)
            {
                if (newPart.FirstName.Trim().Length < 1 && newPart.LastName.Trim().Length < 1)
                {
                    DialogBox.Show("Invalid name given.");
                    return;
                }
                database.AddParticipant(newPart);
                if (newPart.Bib != Constants.Timing.CHIPREAD_DUMMYBIB)
                {
                    ParticipantChanged = true;
                }
            }
            Clear();
            BibBox.Focus();
        }
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