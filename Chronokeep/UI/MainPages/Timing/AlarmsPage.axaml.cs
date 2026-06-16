using Avalonia.Controls;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.UI.Parts;
using Chronokeep.UI.Util;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Chronokeep.UI.MainPages.Timing;

public partial class AlarmsPage : UserControl, ISubPage
{
    private readonly IdbInterface database;
    private readonly TimingPage parent;
    private readonly Event? theEvent;

    public AlarmsPage(TimingPage parent, IdbInterface database)
    {
        InitializeComponent();
        this.parent = parent;
        this.database = database;
        theEvent = database.GetCurrentEvent();
        if (theEvent == null || theEvent.Identifier < 0)
        {
            Log.E("UI.Timing.AlarmsPage", "Something went wrong and no proper event was returned.");
            return;
        }
        parent.SetReaders([], false);
        UpdateAlarms();
    }

    public void CancelableUpdateView(CancellationToken token) { }

    public void Search(CancellationToken token) { }

    public void Show(PeopleType type) { }

    public void SortBy(SortType type) { }

    public void Location(string location) { }

    public void EditSelected() { }

    public void UpdateView() { }

    public void UpdateAlarms()
    {
        Log.D("UI.Timing.AlarmsPage", "Updating View.");
        if (theEvent == null || theEvent.Identifier < 0)
        {
            return;
        }
        AlarmsBox.Items.Clear();
        List<Alarm> alarms = Alarm.GetAlarms();
        alarms.Sort();
        foreach (Alarm alarm in alarms)
        {
            AlarmsBox.Items.Add(new AlarmPart(this, alarm));
        }
    }

    public void Closing()
    {
        Log.D("UI.Timing.AlarmsPage", "Closing Page.");
        if (database.GetAppSetting(Constants.Settings.UPDATE_ON_PAGE_CHANGE)!.Value !=
            Constants.Settings.SETTING_TRUE) return;
        if (AlarmErrors(true))
        {
            return;
        }
        SaveAlarms();
    }

    public void KeyboardCtrlA() { }

    public void KeyboardCtrlS()
    {
        Log.D("UI.Timing.AlarmsPage", "Ctrl+S pressed.");
        if (AlarmErrors())
        {
            return;
        }
        SaveAlarms();
    }

    internal void RemoveAlarm(AlarmPart alarm)
    {
        Alarm newAlarm = alarm.GetUpdatedAlarm();
        Log.D("UI.Timing.AlarmsPage", "Alarm has ID of " + newAlarm.Identifier);
        if (newAlarm.Identifier >= 0)
        {
            database.DeleteAlarm(newAlarm);
        }
        Alarm.RemoveAlarm(newAlarm);
        AlarmsBox.Items.Remove(alarm);
    }

    public void KeyboardCtrlZ()
    {
        Log.D("UI.Timing.AlarmsPage", "Ctrl+Z pressed.");
        UpdateAlarms();
    }

    private void SaveAlarms()
    {
        Log.D("UI.Timing.AlarmsPage", "Saving Alarms.");
        Alarm.ClearAlarms();
        foreach (AlarmPart? alarm in AlarmsBox.Items.Cast<AlarmPart?>())
        {
            Alarm.AddAlarm(alarm!.GetUpdatedAlarm());
        }
        Alarm.SaveAlarms(theEvent!.Identifier, database);
    }

    private bool AlarmErrors(bool silent = false)
    {
        // Verify there are no repeating bibs/chips.
        HashSet<string> bibs = [];
        HashSet<string> chips = [];
        bool notSetExists = false;
        foreach (AlarmPart? alarm in AlarmsBox.Items.Cast<AlarmPart?>())
        {
            Alarm al = alarm!.GetUpdatedAlarm();
            if (al.Bib.Length > 0 && bibs.Contains(al.Bib))
            {
                if (!silent)
                {
                    DialogBox.Show("Unable to continue, multiples of the same bib found.");
                }
                return true;
            }
            bibs.Add(al.Bib);
            if (al.Chip.Length > 0 && chips.Contains(al.Chip))
            {
                if (!silent)
                {
                    DialogBox.Show("Unable to continue, multiples of the same chip found.");
                }
                return true;
            }
            chips.Add(al.Chip);
            if (al.Bib.Length != 0 || al.Chip.Length != 0) continue;
            if (notSetExists)
            {
                if (!silent)
                {
                    DialogBox.Show("Only one alarm without a bib & chip allowed at a time.");
                }
                return true;
            }
            notSetExists = true;
        }
        return false;
    }

    public void Reader() { }

    private void DoneButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.AlarmsPage", "Done clicked.");
        parent.LoadMainDisplay();
    }

    private void SaveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.AlarmsPage", "Save button clicked.");
        if (AlarmErrors())
        {
            return;
        }
        SaveAlarms();
        UpdateAlarms();
    }

    private void AddButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.AlarmsPage", "Add button clicked.");
        AlarmsBox.Items.Add(new AlarmPart(this, new Alarm(-1, "", "", true, 0)));
    }
}