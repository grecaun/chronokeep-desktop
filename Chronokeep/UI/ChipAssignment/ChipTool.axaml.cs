using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.UI.ChipAssignment.Parts;
using Chronokeep.UI.Util;
using System.Collections.Generic;
using System.Linq;

namespace Chronokeep.UI.ChipAssignment;

public partial class ChipTool : Window
{
    private readonly IWindowCallback? window;
    private readonly IDBInterface? database;

    public bool ImportComplete = false;

    public ChipTool()
    {
        InitializeComponent();
        correlationBox.Items.Add(new TagRangePart(correlationBox));
    }

    private ChipTool(IWindowCallback window, IDBInterface database)
    {
        InitializeComponent();
        correlationBox.Items.Add(new TagRangePart(correlationBox));
        this.window = window;
        this.database = database;
        MinHeight = 100;
        MinWidth = 550;
        Width = 600;
        CanResize = false;
    }

    public static ChipTool NewWindow(IWindowCallback window, IDBInterface database)
    {
        return new(window, database);
    }

    private void AddRange_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        correlationBox.Items.Add(new TagRangePart(correlationBox));
    }

    private void Save_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        List<Range> ranges = [];
        foreach (TagRangePart? tag in correlationBox.Items.Cast<TagRangePart?>())
        {
            _ = int.TryParse(tag!.StartBib.Text, out int startBib);
            _ = int.TryParse(tag!.EndBib.Text, out int endBib);
            _ = int.TryParse(tag!.StartChip.Text, out int startChip);
            _ = int.TryParse(tag!.EndChip.Text!.ToString(), out int endChip);
            Log.D("UI.ChipAssignment.ChipTool", "StartBib " + startBib + " EndBib " + endBib + " StartChip " + startChip + " EndChip " + endChip);
            Range curRange = new()
            {
                StartBib = startBib,
                EndBib = endBib,
                StartChip = startChip,
                EndChip = endChip
            };
            bool conflicts = !curRange.IsValid();
            foreach (Range r in ranges)
            {
                if (r.Violates(curRange))
                {
                    conflicts = true;
                }
            }
            if (conflicts)
            {
                DialogBox.Show("One or more values is in conflict. Please fix the error and try again.");
                return;
            }
            ranges.Add(curRange);
        }
        ranges.Sort();
        List<BibChipAssociation> list = [];
        foreach (Range r in ranges)
        {
            for (int bib = r.StartBib, tag = r.StartChip; bib <= r.EndBib && tag <= r.EndChip; bib++, tag++)
            {
                list.Add(new()
                {
                    Bib = bib.ToString(),
                    Chip = tag.ToString()
                });
            }
        }
        Event theEvent = database!.GetCurrentEvent()!;
        database.AddBibChipAssociation(theEvent.Identifier, list);
        ImportComplete = true;
        this.Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        ImportComplete = false;
        Close();
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        window?.WindowFinalize(this);
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}