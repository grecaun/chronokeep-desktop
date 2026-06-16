using Avalonia.Controls;
using Chronokeep.Helpers;
using Chronokeep.IO;
using Chronokeep.UI.Parts;
using Chronokeep.UI.Util;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Chronokeep.UI.Timing.Import;

public partial class ImportLogPage2 : UserControl
{
    private static readonly string[] HumanFields =
    [
        "",
        "Chip",
        "Time"
    ];

    private const int CHIP = 1;
    private const int TIME = 2;

    private readonly ImportLogWindow parent;

    public ImportLogPage2(ImportLogWindow parent, LogImporter importer)
    {
        InitializeComponent();
        this.parent = parent;
        for (int i = 1; i < importer.Data!.GetNumHeaders(); i++)
        {
            ItemListBox.Items.Add(new LogPart(importer.Data.Headers[i], i, HumanFields, 0));
        }
    }

    private List<string> RepeatHeaders()
    {
        Log.D("UI.Timing.ImportLog", "Checking for repeat headers in user selection.");
        int[] check = new int[HumanFields.Length];
        bool repeat = false;
        List<string> output = [];
        foreach (LogPart? item in ItemListBox.Items.Cast<LogPart?>())
        {
            int val = item!.HeaderBox.SelectedIndex;
            if (val <= 0) continue;
            if (check[val] > 0)
            {
                output.Add(item.HeaderBox.SelectedItem!.ToString()!);
                repeat = true;
            }
            else
            {
                check[val] = 1;
            }
        }
        return repeat ? output : [];
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ImportLog", "Cancel clicked.");
        parent.Cancel();
    }

    private void ImportButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ImportLog", "Import clicked.");
        List<string> repeats = RepeatHeaders();
        if (repeats.Count < 1)
        {
            StringBuilder message = new("The following are repeats:\n");
            foreach (string s in repeats)
            {
                message.Append(s);
                message.Append('\n');
            }
            DialogBox.Show(message.ToString());
            return;
        }
        int chip = 0, time = 0;
        foreach (LogPart? item in ItemListBox.Items.Cast<LogPart?>())
        {
            if (CHIP == item!.HeaderBox.SelectedIndex)
            {
                chip = item.Index;
            }
            else if (TIME == item.HeaderBox.SelectedIndex)
            {
                time = item.Index;
            }
        }
        if (chip == 0 || time == 0)
        {
            DialogBox.Show("Both Chip and Time must be chosen.");
            return;
        }
        parent.Import(LogImporter.Type.CUSTOM, Constants.Timing.LOCATION_DUMMY, chip, time);
    }
}