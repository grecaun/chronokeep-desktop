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

public partial class ChipTool : ChronokeepWindow
{
    private readonly IWindowCallback? window;
    private readonly IdbInterface? database;

    public bool ImportComplete;

    private ChipTool(IWindowCallback window, IdbInterface database)
    {
        InitializeComponent();
        ChronokeepInitialize();
        CorrelationBox.Items.Add(new TagRangePart(CorrelationBox));
        this.window = window;
        this.database = database;
        CanResize = false;
    }

    public static ChipTool NewWindow(IWindowCallback window, IdbInterface database)
    {
        return new ChipTool(window, database);
    }

    private void AddRange_Click(object? sender, RoutedEventArgs e)
    {
        CorrelationBox.Items.Add(new TagRangePart(CorrelationBox));
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        List<Range> ranges = [];
        foreach (object? tag in CorrelationBox.Items)
        {
            if (tag is not TagRangePart part) continue;
            _ = int.TryParse(part.StartBib.Text, out int startBib);
            _ = int.TryParse(part.EndBib.Text, out int endBib);
            _ = int.TryParse(part.StartChip.Text, out int startChip);
            _ = int.TryParse(part.EndChip.Text!, out int endChip);
            Log.D("UI.ChipAssignment.ChipTool", $"StartBib {startBib} EndBib {endBib} StartChip {startChip} EndChip {endChip}");
            Range curRange = new()
            {
                StartBib = startBib,
                EndBib = endBib,
                StartChip = startChip,
                EndChip = endChip
            };
            bool conflicts = !curRange.IsValid();
            foreach (Range _ in ranges.Where(r => r.Violates(curRange)))
            {
                conflicts = true;
            }
            if (conflicts)
            {
                DialogBox.AsyncShow("One or more values is in conflict. Please fix the error and try again.");
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
                list.Add(new BibChipAssociation
                {
                    Bib = bib.ToString(),
                    Chip = tag.ToString()
                });
            }
        }
        Event theEvent = database!.GetCurrentEvent()!;
        database.AddBibChipAssociation(theEvent.Identifier, list);
        ImportComplete = true;
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        ImportComplete = false;
        Close();
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        window?.WindowFinalize();
    }

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}
