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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chronokeep.UI.ChipAssignment;

public partial class BibChipAssociationWindow : Window
{
    private readonly IDataImporter importer;
    private readonly IWindowCallback window;
    private readonly IDBInterface database;
    private readonly bool init = true;
    private int[]? keys;

    public bool ImportComplete = false;

    private BibChipAssociationWindow(IWindowCallback window, IDataImporter importer, IDBInterface database)
    {
        InitializeComponent();
        this.importer = importer;
        this.window = window;
        this.database = database;
        this.MinHeight = 300;
        this.MinWidth = 300;
        this.Height = 300;
        this.Width = 300;
        this.Topmost = true;
        if (importer.Data!.Type == ImportData.FileType.EXCEL)
        {
            SheetsContainer.IsVisible = true;
            SheetsBox.ItemsSource = ((ExcelImporter)importer).SheetNames;
            SheetsBox.SelectedIndex = 0;
            init = false;
        }
        else
        {
            SheetsContainer.IsVisible = false;
        }
        for (int i = 1; i < importer.Data.GetNumHeaders(); i++)
        {
            headerListBox.Items.Add(new BibChipHeaderPart(importer.Data.Headers[i], i));
        }
    }

    public static BibChipAssociationWindow NewWindow(IWindowCallback window, IDataImporter importer, IDBInterface database)
    {
        return new(window, importer, database);
    }

    internal List<string> RepeatHeaders()
    {
        Log.D("UI.BibChipAssociationWindow", "Checking for repeat headers in user selection.");
        int[] check = new int[BibChipHeaderPart.human_fields.Length];
        bool repeat = false;
        List<string> output = [];
        foreach (object? item in headerListBox.Items)
        {
            if (item != null && item is BibChipHeaderPart part)
            {
                int val = part.HeaderBox.SelectedIndex;
                if (val > 0)
                {
                    if (check[val] > 0)
                    {
                        output.Add(part.HeaderBox.SelectedItem!.ToString()!);
                        repeat = true;
                    }
                    else
                    {
                        check[val] = 1;
                    }
                }
            }
        }
        return repeat ? output : [];
    }

    internal BibChipHeaderPart[] GetListBoxItems()
    {
        BibChipHeaderPart[] output = new BibChipHeaderPart[headerListBox.Items.Count];
        for (int i = 0; i < headerListBox.Items.Count; i++)
        {
            output[i] = (BibChipHeaderPart)headerListBox.Items[i]!;
        }
        return output;
    }

    private void SheetsBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (init) { return; }
        int selection = ((ComboBox)sender!).SelectedIndex;
        Log.D("UI.BibChipAssociationWindow", "You've selected number " + selection);
        ExcelImporter excelImporter = (ExcelImporter)importer;
        excelImporter.ChangeSheet(selection);
        excelImporter.FetchHeaders();
        headerListBox.Items.Clear();
        for (int i = 1; i < importer.Data!.GetNumHeaders(); i++)
        {
            headerListBox.Items.Add(new BibChipHeaderPart(importer.Data.Headers[i], i));
        }
    }

    private async void Done_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.BibChipAssociationWindow", "Bib Chip Association = Done Clicked.");
        List<string> headers = RepeatHeaders();
        int eventId = -1;
        eventId = database.GetCurrentEvent()!.Identifier;
        if (headers.Count == 0)
        {
            importer.FetchData();
            keys = new int[3];
            foreach (BibChipHeaderPart? item in headerListBox.Items.Cast<BibChipHeaderPart?>())
            {
                if (item!.HeaderBox.SelectedIndex != 0)
                {
                    keys[item.HeaderBox.SelectedIndex] = item.Index;
                }
            }
            bool extraAssoc = Headers.IsChecked == false;
            await Task.Run(() =>
            {
                Dictionary<string, string> currentAssociations = database.GetBibChips(eventId).ToDictionary(x => x.Chip, x => x.Bib);
                List<BibChipAssociation> items = [];
                ImportData data = importer.Data!;
                int numEntries = data!.Data.Count;
                if (extraAssoc)
                {
                    items.Add(new()
                    {
                        Bib = data.Headers[keys[1]],
                        Chip = data.Headers[keys[2]]
                    });
                }
                for (int counter = 0; counter < numEntries; counter++)
                {
                    try
                    {
                        items.Add(new()
                        {
                            Bib = data.Data[counter][keys[1]],
                            Chip = data.Data[counter][keys[2]]
                        });
                    }
                    catch
                    {
                        Log.E("UI.BibChipAssociationWindow", "One or more values not an integer.");
                    }
                }
                // Check new associations against old ones.
                List<BibChipAssociation> conflicts = [];
                foreach (BibChipAssociation assoc in items)
                {
                    // Check to ensure we aren't trying to associate this chip with a different bib
                    // when it already has one associated with it.
                    if (currentAssociations.TryGetValue(assoc.Chip, out string? oBib) && !oBib.Equals(assoc.Bib, StringComparison.OrdinalIgnoreCase))
                    {
                        conflicts.Add(assoc);
                    }
                }
                // if there are conflicts, alter the user to them and verify clobbering
                if (conflicts.Count > 0)
                {
                    StringBuilder error = new("There were conflicts found in the import file. Please confirm you want to clobber current values.");
                    foreach (BibChipAssociation assoc in conflicts)
                    {
                        error.Append(string.Format("\nChip {0} - Bib {1}", assoc.Chip, assoc.Bib));
                    }
                    DialogBox.Show(
                        error.ToString(),
                        "Yes",
                        "No",
                        () =>
                        {
                            items.RemoveAll(x => conflicts.Contains(x));
                        }
                        );
                }
                database.AddBibChipAssociation(eventId, items);
            });
            Log.D("UI.BibChipAssociationWindow", "All done with bib chip associations.");
            ImportComplete = true;
            Close();
        }
        else
        {
            string val = "";
            foreach (string str in headers)
            {
                val = " " + str;
            }
            DialogBox.Show("Multiple values given for: " + val);
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        importer.Finish();
        Log.D("UI.BibChipAssociationWindow", "Bib Chip Association - Cancel Button clicked.");
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