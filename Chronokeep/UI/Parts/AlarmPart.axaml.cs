using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Helpers;
using Chronokeep.Objects;
using Chronokeep.UI.MainPages.Timing;

namespace Chronokeep.UI.Parts;

public partial class AlarmPart : UserControl
{
    private readonly AlarmsPage page;
    private readonly Alarm theAlarm;

    public AlarmPart(AlarmsPage page, Alarm alarm)
    {
        InitializeComponent();
        this.page = page;
        theAlarm = alarm;
        BibBox.Text = theAlarm.Bib;
        ChipBox.Text = theAlarm.Chip;
        EnabledBox.IsChecked = theAlarm.Enabled;
        AlarmSoundBox.SelectedIndex = theAlarm.AlarmSound;
    }

    public Alarm GetUpdatedAlarm()
    {
        theAlarm.Bib = BibBox.Text!.Trim();
        theAlarm.Chip = theAlarm.Bib.Length > 0 ? "" : ChipBox.Text!;
        theAlarm.Enabled = EnabledBox.IsChecked == true;
        theAlarm.AlarmSound = AlarmSoundBox.SelectedIndex;
        return theAlarm;
    }

    private void Remove_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.AlarmsPage", "Removing alarm.");
        page.RemoveAlarm(this);
    }
}