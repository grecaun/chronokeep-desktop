using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Helpers;
using Chronokeep.Objects.ChronokeepPortal;
using Chronokeep.Timing.Interfaces;
using Chronokeep.UI.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls.Primitives;
using Chronokeep.UI.Timing.ReaderSettings.Parts;

namespace Chronokeep.UI.Timing.ReaderSettings;

public partial class ChronokeepSettings : ChronokeepWindow
{
    private readonly ChronokeepInterface reader;

    private bool saving;

    private Dictionary<long, ReaderSubPart> readerDict = [];
    private Dictionary<long, ApiPart> apiDict = [];

    internal ChronokeepSettings(ChronokeepInterface reader)
    {
        InitializeComponent();
        ChronokeepInitialize();
        this.reader = reader;
        if (!App.IsWindows)
        {
            MainGrid.RowDefinitions =
            [
                new RowDefinition(new GridLength(15)),
                new RowDefinition(new GridLength(1, GridUnitType.Star))
            ];
        }
        reader.SendGetSettings();
    }

    internal void UpdateView(PortalSettingsHolder allSettings)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "UpdateView.");
        Application.Current!.Dispatcher.Invoke(delegate
        {
            if (saving)
            {
                Close();
            }
            if (allSettings.Changes.Contains(PortalSettingsHolder.ChangeType.SETTINGS))
            {
                VersionBlock.Text = allSettings.PortalVersion;
                NameBox.Text = allSettings.Name;
                ReadWindowBox.Text = allSettings.ReadWindow.ToString();
                ChipTypeBox.SelectedIndex = allSettings.ChipType == PortalSettingsHolder.ChipTypeEnum.DEC ? 0 : 1;
                VolumeSlider.Value = allSettings.Volume * 10;
                UploadSlider.Value = allSettings.UploadInterval;
                BeepSlider.Value = allSettings.BeepInterval;
                SoundBox.IsChecked = allSettings.PlaySound;
                VoiceBox.SelectedIndex = allSettings.Voice switch
                {
                    PortalSettingsHolder.VoiceType.EMILY => 0,
                    PortalSettingsHolder.VoiceType.MICHAEL => 1,
                    PortalSettingsHolder.VoiceType.CUSTOM => 2,
                    _ => VoiceBox.SelectedIndex
                };
                NtfyUrlBox.Text = allSettings.NtfyUrl;
                NtfyTopicBox.Text = allSettings.NtfyTopic;
                NtfyUserBox.Text = allSettings.NtfyUser;
                NtfyPassBox.Text = allSettings.NtfyPass;
                EnableNtfySwitch.IsChecked = allSettings.EnableNtfy;
                switch (allSettings.ScreenType)
                {
                    case Constants.Readers.CHRONOKEEP_SCREEN_ADAFRUIT:
                        ScreenPanel.IsVisible = true;
                        ScreenBox.SelectedIndex = 0;
                        break;
                    case Constants.Readers.CHRONOKEEP_SCREEN_PCF8574T:
                        ScreenPanel.IsVisible = true;
                        ScreenBox.SelectedIndex = 1;
                        break;
                    default:
                        ScreenPanel.IsVisible = false;
                        ScreenBox.SelectedIndex = -1;
                        break;
                }
            }
            // add readers and apis to views
            if (allSettings.Changes.Contains(PortalSettingsHolder.ChangeType.READERS))
            {
                // keep track of which readers we are already displaying
                HashSet<long> found = [];
                foreach (PortalReader read in allSettings.Readers)
                {
                    found.Add(read.Id);
                    // update if we know about them
                    if (readerDict.TryGetValue(read.Id, out ReaderSubPart? oReaderItem))
                    {
                        oReaderItem.UpdateReader(read);
                    }
                    // otherwise add new
                    else
                    {
                        readerDict[read.Id] = new ReaderSubPart(read, reader);
                    }
                }
                Dictionary<long, ReaderSubPart> newDictionary = readerDict.Where(pair => found.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
                readerDict = newDictionary;
                ReaderListView.Items.Clear();
                foreach (ReaderSubPart item in readerDict.Values)
                {
                    ReaderListView.Items.Add(item);
                }
            }
            if (allSettings.Changes.Contains(PortalSettingsHolder.ChangeType.APIS))
            {
                // keep track of which apis we are already displaying
                HashSet<long> found = [];
                foreach (PortalApi api in allSettings.ApIs)
                {
                    found.Add(api.Id);
                    // update if we know about them
                    if (apiDict.TryGetValue(api.Id, out ApiPart? oApiItem))
                    {
                        oApiItem.UpdateApi(api);
                    }
                    else
                    {
                        apiDict[api.Id] = new ApiPart(api, reader);
                    }
                }
                Dictionary<long, ApiPart> newDictionary = apiDict.Where(pair => found.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
                apiDict = newDictionary;
                ApiListView.Items.Clear();
                foreach (ApiPart item in apiDict.Values)
                {
                    ApiListView.Items.Add(item);
                }
            }
            if (allSettings.Changes.Contains(PortalSettingsHolder.ChangeType.ANTENNAS))
            {
                foreach (ReaderSubPart readerSubPart in readerDict.Values.Where(readerSubPart => readerSubPart.GetReaderName().Equals(allSettings.Antennas.ReaderName, StringComparison.OrdinalIgnoreCase)))
                {
                    readerSubPart.UpdateAntennas(allSettings.Antennas.Antennas);
                    break;
                }
            }
            switch (allSettings.AutoUpload)
            {
                case PortalStatus.RUNNING:
                    AutoResultsSwitch.IsEnabled = true;
                    AutoResultsSwitch.IsChecked = true;
                    break;
                case PortalStatus.UNKNOWN:
                case PortalStatus.STOPPED:
                    AutoResultsSwitch.IsEnabled = true;
                    AutoResultsSwitch.IsChecked = false;
                    break;
                case PortalStatus.STOPPING:
                    AutoResultsSwitch.IsEnabled = false;
                    AutoResultsSwitch.IsChecked = true;
                    break;
                case PortalStatus.NOTSET:
                default:
                    break;
            }
        });
    }

    public void CloseWindow()
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "CloseWindow.");
        Application.Current!.Dispatcher.Invoke(Close);
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        reader.SettingsWindowFinalize();
    }

    private void VolumeSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (VolumeSlider != null && VolumeBlock != null)
        {
            VolumeBlock.Text = VolumeSlider.Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private void ReaderExpander_Changed(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Reader expander expanding/contracting.");
        AddReaderButton.IsVisible = ReaderExpander.IsExpanded;
    }

    private void AddReaderButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Adding new reader.");
        reader.SendSaveReader(new PortalReader
        {
            Id = -1,
            Name = "New Reader",
            Kind = PortalReader.READER_KIND_ZEBRA,
            IpAddress = "192.168.1.0",
            Port = uint.Parse(PortalReader.READER_DEFAULT_PORT_ZEBRA),
            AutoConnect = true,
        });
    }

    private void APIExpander_Changed(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "API expander expanding/contracting.");
        AddApiButton.IsVisible = ApiExpander.IsExpanded;
    }

    private void AddAPIButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Add API button clicked.");
        reader.SendSaveApi(new PortalApi
        {
            Id = -1,
            Nickname = "New API",
            Kind = PortalApi.API_TYPE_CHRONOKEEP_REMOTE,
            Token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
            Uri = PortalApi.API_URI_CHRONOKEEP_REMOTE,
        });
    }

    private void UploadSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (UploadSlider != null && UploadBlock != null)
        {
            UploadBlock.Text = UploadSlider.Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private void BeepSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (BeepSlider != null && BeepBlock != null)
        {
            BeepBlock.Text = BeepSlider.Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private void ManualResultsButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Manually uploading results.");
        reader.SendManualResultsUpload();
    }

    private void DeleteReadsButton_Click(object? sender, RoutedEventArgs? e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "User requests deletion of reader chip reads.");
        DialogBox.AsyncShow("This will delete all of the chip reads from the reader.  This action is not reversible. Continue?", "Yes", "No", () =>
        {
            Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Clearing chip reads from reader.");
            reader.SendDeleteAllReads();
        });
    }

    private void UpdateServerButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Update button clicked.");
        DialogBox.AsyncShow(
            "This will update the portal software. Do you want to proceed?",
            "Yes",
            "No",
            () =>
            {
                // send update command
                reader.SendUpdate();
                Close();
            }
            );
    }

    private void RestartServerButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Restart button clicked.");
        DialogBox.AsyncShow(
            "This will restart the portal software. Do you want to proceed?",
            "Yes",
            "No",
            () =>
            {
                // send restart command
                reader.SendRestart();
                Close();
            }
            );
    }

    private void StopServerButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Stop button clicked.");
        DialogBox.AsyncShow(
            "This will stop the portal software. Do you want to proceed?",
            "Yes",
            "No",
            () =>
            {
                // send stop command
                reader.SendQuit();
                Close();
            }
            );
    }

    private void ShutdownServerButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Shutdown button clicked.");
        DialogBox.AsyncShow(
            "This will shutdown the entire computer the portal software is running on. Do you want to proceed?",
            "Yes",
            "No",
            () =>
            {
                // send shutdown command
                reader.SendShutdown();
                Close();
            }
            );
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Save button clicked.");
        saving = true;
        try
        {
            PortalSettingsHolder sett = new()
            {
                Name = NameBox.Text!.Trim(),
                ReadWindow = int.Parse(ReadWindowBox.Text!.Trim()),
                ChipType = ChipTypeBox.SelectedIndex == 0 ? PortalSettingsHolder.ChipTypeEnum.DEC
                    : PortalSettingsHolder.ChipTypeEnum.HEX,
                Volume = VolumeSlider.Value / 10,
                UploadInterval = (int)UploadSlider.Value,
                BeepInterval = (int)BeepSlider.Value,
                PlaySound = SoundBox.IsChecked == true,
                Voice = VoiceBox.SelectedIndex == 0 ? PortalSettingsHolder.VoiceType.EMILY
                    : VoiceBox.SelectedIndex == 1 ? PortalSettingsHolder.VoiceType.MICHAEL
                    : PortalSettingsHolder.VoiceType.CUSTOM,
                NtfyUrl = NtfyUrlBox.Text!.Trim(),
                NtfyTopic = NtfyTopicBox.Text!.Trim(),
                NtfyUser = NtfyUserBox.Text!.Trim(),
                NtfyPass = NtfyPassBox.Text!.Trim(),
                EnableNtfy = EnableNtfySwitch.IsChecked == true,
                ScreenType = ScreenBox.SelectedItem != null ? (string)((ComboBoxItem)ScreenBox.SelectedItem).Tag! : ""
            };
            reader.SendSetSettings(sett);
        }
        catch (Exception ex)
        {
            Log.E("UI.Timing.ReaderSettings.ChronokeepSettings", $"Error saving settings: {ex.Message}");
            DialogBox.AsyncShow("Error saving settings.");
        }
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Close button clicked.");
        Close();
    }

    private void AutoResultsSwitch_Checked(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Auto upload switched.");
        if (!AutoResultsSwitch.IsEnabled)
        {
            return;
        }
        reader.SendAutoUploadResults(AutoResultsSwitch.IsChecked == false
            ? AutoUploadQuery.STOP
            : AutoUploadQuery.START);
    }

    protected override Border? TitleBar()
    {
        return ChronokeepToolBar;
    }
}