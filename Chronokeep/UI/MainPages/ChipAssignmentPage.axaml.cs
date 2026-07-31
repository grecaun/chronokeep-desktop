using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.IO;
using Chronokeep.Interfaces.UI;
using Chronokeep.IO;
using Chronokeep.Objects;
using Chronokeep.UI.ChipAssignment;
using Chronokeep.UI.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Chronokeep.UI.MainPages;

public partial class ChipAssignmentPage : UserControl, IMainPage
{

    private readonly IMainWindow mWindow;
    private readonly IdbInterface database;
    private readonly Event? theEvent;
    private AppSetting chipType;

    private bool bibsChanged;

    [GeneratedRegex("[^0-9]")]
    private static partial Regex AllowedChars();
    [GeneratedRegex("[^0-9a-fA-F]")]
    private static partial Regex AllowedHexChars();

    public ChipAssignmentPage(IMainWindow mWindow, IdbInterface database)
    {
        InitializeComponent();
        this.mWindow = mWindow;
        this.database = database;
        chipType = database.GetAppSetting(Constants.Settings.DEFAULT_CHIP_TYPE)!;
        ChipTypeBox.SelectedIndex = chipType.Value switch
        {
            Constants.Settings.CHIP_TYPE_DEC => 0,
            Constants.Settings.CHIP_TYPE_HEX => 1,
            _ => ChipTypeBox.SelectedIndex
        };
        ChipTypeBox.SelectionChanged += ChipTypeBox_SelectionChanged;
        theEvent = database.GetCurrentEvent();
        UpdateView();
    }

    public async void UpdateView()
    {
        try
        {
            if (theEvent == null)
            {
                return;
            }
            List<BibChipAssociation> list = [];
            List<BibChipAssociation> ignored = [];
            await Task.Run(() =>
            {
                list = database.GetBibChips(theEvent.Identifier);
                list.Sort();
                ignored = database.GetBibChips(-1);
                ignored.Sort();
            });
            BibChipList.ItemsSource = list;
            IgnoredChipList.ItemsSource = ignored;
            long maxChip = 0;
            long chip;
            switch (chipType.Value)
            {
                // check if hex before using a convert
                case Constants.Settings.CHIP_TYPE_DEC:
                {
                    foreach (BibChipAssociation b in list)
                    {
                        _ = long.TryParse(b.Chip, out chip);
                        maxChip = chip > maxChip ? chip : maxChip;
                    }

                    break;
                }
                case Constants.Settings.CHIP_TYPE_HEX:
                {
                    foreach (BibChipAssociation b in list)
                    {
                        long.TryParse(b.Chip, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out chip);
                        maxChip = chip > maxChip ? chip : maxChip;
                    }

                    break;
                }
            }
            maxChip += 1;
            switch (chipType.Value)
            {
                case Constants.Settings.CHIP_TYPE_DEC:
                    SingleChipBox.Text = maxChip.ToString();
                    RangeStartChipBox.Text = maxChip.ToString();
                    break;
                case Constants.Settings.CHIP_TYPE_HEX:
                    SingleChipBox.Text = maxChip.ToString("X");
                    RangeStartChipBox.Text = maxChip.ToString("X");
                    break;
            }
            List<Event> events = [];
            await Task.Run(() =>
            {
                events = database.GetEvents();
                events.Sort();
            });
            PreviousEvents.Items.Clear();
            ComboBoxItem boxItem = new()
            {
                Content = "None",
                Tag = "-1"
            };
            PreviousEvents.Items.Add(boxItem);
            foreach (Event e in events)
            {
                if (e.Equals(theEvent)) continue;
                string name = $"{e.YearCode} {e.Name}";
                name = name.Trim();
                boxItem = new ComboBoxItem
                {
                    Content = name,
                    Tag = e.Identifier.ToString()
                };
                PreviousEvents.Items.Add(boxItem);
            }
            PreviousEvents.SelectedIndex = 0;
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.ChipAssignmentPage", "Error updating views.");
        }
    }

    private static void UpdateDatabase() { }

    public void KeyboardCtrlA()
    {
        UseTool_Click(null, null);
    }

    public void KeyboardCtrlS()
    {
        Export_Click(null, null);
    }

    public void KeyboardCtrlZ() { }

    public void Closing()
    {
        if (database.GetAppSetting(Constants.Settings.UPDATE_ON_PAGE_CHANGE)!.Value == Constants.Settings.SETTING_TRUE)
        {
            UpdateDatabase();
        }
        if (!bibsChanged) return;
        database.ResetTimingResultsEvent(theEvent!.Identifier);
        mWindow.NetworkClearResults();
        mWindow.NotifyTimingWorker();
    }

    private void Delete_Click(object? sender, RoutedEventArgs? e)
    {
        Log.D("UI.MainPages.ChipAssignmentPage", "Delete clicked.");
        List<BibChipAssociation> items = [];
        items.AddRange(BibChipList.SelectedItems.Cast<BibChipAssociation>());
        database.RemoveBibChipAssociations(items);
        bibsChanged = true;
        UpdateView();
    }

    private void Clear_Click(object? sender, RoutedEventArgs? e)
    {
        DialogBox.AsyncShow(
            "Are you sure you want to delete everything? This cannot be undone.",
            "Yes",
            "No",
            () =>
            {
                database.RemoveBibChipAssociations((List<BibChipAssociation>)BibChipList.ItemsSource);
                bibsChanged = true;
                UpdateView();
            }
            );
    }

    private void DeleteIgnored_Click(object? sender, RoutedEventArgs? e)
    {
        Log.D("UI.MainPages.ChipAssignmentPage", "Delete ignored clicked.");
        List<BibChipAssociation> items = [];
        items.AddRange(IgnoredChipList.SelectedItems.Cast<BibChipAssociation>());
        database.RemoveBibChipAssociations(items);
        bibsChanged = true;
        UpdateView();
    }

    private void ClearIgnored_Click(object? sender, RoutedEventArgs? e)
    {
        DialogBox.AsyncShow(
            "Are you sure you want to delete everything? This cannot be undone.",
            "Yes",
            "No",
            () =>
            {
                database.RemoveBibChipAssociations((List<BibChipAssociation>)IgnoredChipList.ItemsSource);
                bibsChanged = true;
                UpdateView();
            }
            );
    }

    private void KeyPressHandlerSingle(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SaveSingleButton_Click(null, null);
        }
    }

    private void SelectAll(object? sender, FocusChangedEventArgs e)
    {
        TextBox? src = e.Source as TextBox;
        src?.SelectAll();
    }

    private void ChipValidation(object? sender, TextInputEventArgs e)
    {
        e.Handled = chipType.Value switch
        {
            Constants.Settings.CHIP_TYPE_DEC => e.Text != null && AllowedChars().IsMatch(e.Text),
            Constants.Settings.CHIP_TYPE_HEX => e.Text != null && AllowedHexChars().IsMatch(e.Text),
            _ => e.Handled
        };
    }

    private void SaveSingleButton_Click(object? sender, RoutedEventArgs? e)
    {
        Log.D("UI.MainPages.ChipAssignmentPage", "Save Single clicked.");
        long chip = -1;
        if (!long.TryParse(SingleBibBox.Text, out long bib))
        {
            bib = -1;
        }
        switch (chipType.Value)
        {
            case Constants.Settings.CHIP_TYPE_DEC:
                _ = long.TryParse(SingleChipBox.Text, out chip);
                break;
            case Constants.Settings.CHIP_TYPE_HEX:
                long.TryParse(SingleChipBox.Text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out chip);
                break;
        }
        Log.D("UI.MainPages.ChipAssignmentPage", $"Bib {bib} Chip {chip}");
        if (chip == -1)
        {
            DialogBox.AsyncShow("The chip is not valid.");
            return;
        }
        List<BibChipAssociation> bibChips =
        [
            new()
                {
                    EventId = theEvent!.Identifier,
                    Bib = SingleBibBox.Text!,
                    Chip = Constants.Settings.CHIP_TYPE_DEC == chipType.Value ? chip.ToString() : chip.ToString("X")
                }
        ];
        database.AddBibChipAssociation(theEvent!.Identifier, bibChips);
        bibsChanged = true;
        UpdateView();
        SingleBibBox.Text = bib > -1 ? (bib + 1).ToString() : "";
        SingleBibBox.Focus();
    }

    private async void FileImport_Click(object? sender, RoutedEventArgs? e)
    {
        try
        {
            Log.D("UI.MainPages.ChipAssignmentPage", "Import from file clicked.");

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
            IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                FileTypeFilter = [Utils.ExcelType, FilePickerFileTypes.All],
                AllowMultiple = false,
                SuggestedStartLocation = startingFolder,
            });
            if (files.Count <= 0) return;
            string ext = Path.GetExtension(files[0].Name);
            try
            {
                string? filePath = files[0].TryGetLocalPath();
                IDataImporter importer;
                if (ext is ".xlsx" or ".xls")
                {
                    importer = new ExcelImporter(filePath!);
                }
                else
                {
                    importer = new CsvImporter(filePath!);
                }
                await Task.Run(() =>
                {
                    importer.FetchHeaders();
                });
                BibChipAssociationWindow bcWindow = BibChipAssociationWindow.NewWindow(mWindow, importer, database);
                mWindow.AddWindow(bcWindow);
                await bcWindow.ShowDialog((Window)mWindow);
                if (bcWindow.ImportComplete)
                {
                    bibsChanged = true;
                }
            }
            catch (Exception ex)
            {
                Log.E("UI.MainPages.ChipAssignmentPage", $"Something went wrong when trying to read the CSV file. {ex.StackTrace}");
                DialogBox.AsyncShow("Unable to open file.");
            }
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.ChipAssignmentPage", "Error importing.");
        }
    }

    private void UseTool_Click(object? sender, RoutedEventArgs? e)
    {
        Log.D("UI.MainPages.ChipAssignmentPage", "Use Tool clicked.");
        ChipTool chipTool = ChipTool.NewWindow(mWindow, database);
        mWindow.AddWindow(chipTool);
        chipTool.ShowDialog((Window)mWindow);
        if (chipTool.ImportComplete)
        {
            bibsChanged = true;
        }
    }

    private async void Export_Click(object? sender, RoutedEventArgs? e)
    {
        try
        {
            Log.D("UI.MainPages.ChipAssignmentPage", "Export clicked.");
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
                FileTypeChoices = [Utils.ExcelType],
                SuggestedFileName = $"{theEvent!.YearCode} {theEvent.Name} Chips.xlsx",
                SuggestedStartLocation = startingFolder,
            });
            if (file is null) return;
            List<object[]> data = [];
            List<BibChipAssociation> associations = database.GetBibChips(theEvent.Identifier);
            associations.Sort();
            foreach (BibChipAssociation association in associations)
            {
                Log.D("UI.MainPages.ChipAssignmentPage", $"Checking associations ... Bib {association.Bib} Chip {association.Chip}");
            }
            string[] headers = ["Bib", "Chip"];
            data.AddRange(associations.Select(bca => (object[])[bca.Bib, bca.Chip]));
            IDataExporter exporter;
            string extension = Path.GetExtension(file.Name);
            Log.D("UI.MainPages.ChipAssignmentPage", $"Extension is '{extension}'");
            if (extension.Contains("xls", StringComparison.CurrentCulture))
            {
                exporter = new ExcelExporter();
            }
            else
            {
                StringBuilder format = new();
                for (int i = 0; i < headers.Length; i++)
                {
                    format.Append("\"{");
                    format.Append(i);
                    format.Append("}\",");
                }
                format.Remove(format.Length - 1, 1);
                Log.D("UI.MainPages.ChipAssignmentPage", $"The format is '{format}'");
                exporter = new CsvExporter(format.ToString());
            }
            exporter.SetData(headers, data);
            exporter.ExportData(file.TryGetLocalPath()!);
            DialogBox.AsyncShow("File saved.");
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.ChipAssignmentPage", "Error exporting.");
        }
    }

    private void KeyPressHandlerRange(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SaveRangeButton_Click(null, null);
        }
    }

    private void UpdateEndChip(object? sender, TextChangedEventArgs e)
    {
        long startChip = -1;
        _ = long.TryParse(RangeStartBibBox.Text, out long startBib);
        _ = long.TryParse(RangeEndBibBox.Text, out long endBib);
        switch (chipType.Value)
        {
            case Constants.Settings.CHIP_TYPE_DEC when !long.TryParse(RangeStartChipBox.Text, out startChip):
            case Constants.Settings.CHIP_TYPE_HEX when !long.TryParse(RangeStartChipBox.Text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out startChip):
                return;
        }
        long endChip = Math.Abs(endBib - startBib) + startChip;
        if (startBib <= -1 || endBib <= -1 || startChip <= -1) return;
        RangeEndChipLabel.Text = chipType.Value switch
        {
            Constants.Settings.CHIP_TYPE_DEC => endChip.ToString(),
            Constants.Settings.CHIP_TYPE_HEX => endChip.ToString("X"),
            _ => RangeEndChipLabel.Text
        };
    }

    private void SaveRangeButton_Click(object? sender, RoutedEventArgs? e)
    {
        Log.D("UI.MainPages.ChipAssignmentPage", "Save Range clicked.");
        long startChip = -1, endChip = -1;
        if (!long.TryParse(RangeStartBibBox.Text, out long startBib) || !long.TryParse(RangeEndBibBox.Text, out long endBib))
        {
            DialogBox.AsyncShow("Invalid bibs for range based assignment.");
            return;
        }
        switch (chipType.Value)
        {
            case Constants.Settings.CHIP_TYPE_DEC when !long.TryParse(RangeStartChipBox.Text, out startChip) ||
                                                       !long.TryParse(RangeEndChipLabel.Text!, out endChip):
            case Constants.Settings.CHIP_TYPE_HEX when !long.TryParse(RangeStartChipBox.Text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out startChip) ||
                                                       !long.TryParse(RangeEndChipLabel.Text!, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out endChip):
                DialogBox.AsyncShow("Invalid chip values.");
                return;
        }
        Log.D("UI.MainPages.ChipAssignmentPage", $"StartBib {startBib} EndBib {endBib} StartChip {startChip} EndChip {endChip}");
        if (startChip == -1 || endChip == -1 || startBib == -1 || endBib == -1)
        {
            DialogBox.AsyncShow("One or more values is not valid.");
            return;
        }
        List<BibChipAssociation> bibChips = [];
        if (startBib > endBib)
        {
            // Normal save range -- both increase
            for (long bib = startBib, tag = startChip; bib <= endBib && tag <= endChip; bib++, tag++)
            {
                bibChips.Add(new BibChipAssociation
                {
                    EventId = theEvent!.Identifier,
                    Bib = bib.ToString(),
                    Chip = Constants.Settings.CHIP_TYPE_HEX == chipType.Value ? tag.ToString("X") : tag.ToString()
                });
            }
        }
        else
        {
            // Reverse save range -- bib decreases but chip increases
            for (long bib = startBib, tag=startChip; bib >= endBib && tag <= endChip; bib--, tag++)
            {
                bibChips.Add(new BibChipAssociation
                {
                    EventId = theEvent!.Identifier,
                    Bib = bib.ToString(),
                    Chip = Constants.Settings.CHIP_TYPE_HEX == chipType.Value ? tag.ToString("X") : tag.ToString()
                });
            }
        }
        database.AddBibChipAssociation(theEvent!.Identifier, bibChips);
        bibsChanged = true;
        UpdateView();
        long newBib = endBib > startBib ? endBib + 1 : startBib + 1;
        RangeStartBibBox.Text = $"{newBib}";
        RangeEndBibBox.Text = $"{newBib}";
        RangeStartBibBox.Focus();
    }

    private void Copy_Click(object? sender, RoutedEventArgs? e)
    {
        Log.D("UI.MainPages.ChipAssignmentPage", "Copy clicked.");
        int oldEventId = Convert.ToInt32((string)((ComboBoxItem)PreviousEvents.SelectedItem!).Tag!);
        Log.D("UI.MainPages.ChipAssignmentPage", $"Old event Id is {oldEventId}");
        if (oldEventId > 0)
        {
            List<BibChipAssociation> assocs = database.GetBibChips(oldEventId);
            database.AddBibChipAssociation(theEvent!.Identifier, assocs);
            bibsChanged = true;
            UpdateView();
        }
    }

    private void KeyPressHandlerIgnored(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SaveIgnored_Click(null, null);
        }
    }

    private void SaveIgnored_Click(object? sender, RoutedEventArgs? e)
    {
        Log.D("UI.MainPages.ChipAssignmentPage", "Save Ignored clicked.");
        long chip = -1;
        if (Constants.Settings.CHIP_TYPE_DEC != chipType.Value)
        {
            if (Constants.Settings.CHIP_TYPE_HEX == chipType.Value)
            {
                _ = long.TryParse(IgnoredChipBox.Text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out chip);
            }
        }
        else
        {
            _ = long.TryParse(IgnoredChipBox.Text, out chip);
        }
        Log.D("UI.MainPages.ChipAssignmentPage", $" Chip {chip}");
        if (chip == -1)
        {
            DialogBox.AsyncShow("The chip is not valid.");
            return;
        }
        List<BibChipAssociation> bibChips =
        [
            new()
                {
                    EventId = -1,
                    Bib = IgnoredChipBox.Text!,
                    Chip = Constants.Settings.CHIP_TYPE_DEC == chipType.Value ? chip.ToString() : chip.ToString("X")
                }
        ];
        database.AddBibChipAssociation(-1, bibChips);
        Globals.UpdateIgnoredChips(database);
        bibsChanged = true;
        UpdateView();
        IgnoredChipBox.Text = "";
        IgnoredChipBox.Focus();
    }

    private void ChipTypeBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        switch (ChipTypeBox.SelectedIndex)
        {
            case 0:
                database.SetAppSetting(Constants.Settings.DEFAULT_CHIP_TYPE, Constants.Settings.CHIP_TYPE_DEC);
                SingleChipBox.Text = "0";
                RangeStartChipBox.Text = "0";
                break;
            case 1:
                database.SetAppSetting(Constants.Settings.DEFAULT_CHIP_TYPE, Constants.Settings.CHIP_TYPE_HEX);
                break;
        }
        chipType = database.GetAppSetting(Constants.Settings.DEFAULT_CHIP_TYPE)!;
    }
}