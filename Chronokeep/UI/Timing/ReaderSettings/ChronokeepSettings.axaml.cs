using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Helpers;
using Chronokeep.Objects.ChronokeepPortal;
using Chronokeep.Timing.Interfaces;
using Chronokeep.UI.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Chronokeep.UI.Timing.ReaderSettings;

public partial class ChronokeepSettings : Window
{
    private readonly ChronokeepInterface? reader = null;

    private bool saving = false;

    private Dictionary<long, Parts.ReaderSubPart> readerDict = [];
    private Dictionary<long, Parts.APIPart> apiDict = [];

    internal ChronokeepSettings(ChronokeepInterface reader)
    {
        InitializeComponent();
        this.MinWidth = 100;
        this.MinHeight = 100;
        this.reader = reader;
        reader?.SendGetSettings();
    }

    internal void UpdateView(PortalSettingsHolder allSettings)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "UpdateView.");
        Application.Current!.Dispatcher.Invoke(new Action(delegate ()
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
                switch (allSettings.Voice)
                {
                    case PortalSettingsHolder.VoiceType.EMILY:
                        VoiceBox.SelectedIndex = 0;
                        break;
                    case PortalSettingsHolder.VoiceType.MICHAEL:
                        VoiceBox.SelectedIndex = 1;
                        break;
                    case PortalSettingsHolder.VoiceType.CUSTOM:
                        VoiceBox.SelectedIndex = 2;
                        break;
                }
                NtfyUrlBox.Text = allSettings.NtfyURL;
                NtfyTopicBox.Text = allSettings.NtfyTopic;
                NtfyUserBox.Text = allSettings.NtfyUser;
                NtfyPassBox.Text = allSettings.NtfyPass;
                EnableNTFYSwitch.IsChecked = allSettings.EnableNTFY;
                if (allSettings.ScreenType == Constants.Readers.CHRONOKEEP_SCREEN_ADAFRUIT)
                {
                    ScreenPanel.IsVisible = true;
                    ScreenBox.SelectedIndex = 0;
                }
                else if (allSettings.ScreenType == Constants.Readers.CHRONOKEEP_SCREEN_PCF8574T)
                {
                    ScreenPanel.IsVisible = true;
                    ScreenBox.SelectedIndex = 1;
                }
                else
                {
                    ScreenPanel.IsVisible = false;
                    ScreenBox.SelectedIndex = -1;
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
                    if (readerDict.TryGetValue(read.Id, out Parts.ReaderSubPart? oReaderItem))
                    {
                        oReaderItem.UpdateReader(read);
                    }
                    // otherwise add new
                    else
                    {
                        readerDict[read.Id] = new(read, reader!);
                    }
                }
                var newDictionary = readerDict.Where(pair => found.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
                readerDict = newDictionary;
                ReaderListView.Items.Clear();
                foreach (Parts.ReaderSubPart item in readerDict.Values)
                {
                    ReaderListView.Items.Add(item);
                }
            }
            if (allSettings.Changes.Contains(PortalSettingsHolder.ChangeType.APIS))
            {
                // keep track of which apis we are already displaying
                HashSet<long> found = [];
                foreach (PortalAPI api in allSettings.APIs)
                {
                    found.Add(api.Id);
                    // update if we know about them
                    if (apiDict.TryGetValue(api.Id, out Parts.APIPart? oAPIItem))
                    {
                        oAPIItem.UpdateAPI(api);
                    }
                    else
                    {
                        apiDict[api.Id] = new(api, reader!);
                    }
                }
                var newDictionary = apiDict.Where(pair => found.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
                apiDict = newDictionary;
                ApiListView.Items.Clear();
                foreach (Parts.APIPart item in apiDict.Values)
                {
                    ApiListView.Items.Add(item);
                }
            }
            if (allSettings.Changes.Contains(PortalSettingsHolder.ChangeType.ANTENNAS))
            {
                Dictionary<string, Parts.ReaderSubPart> readerNameDict = [];
                foreach (Parts.ReaderSubPart reader in readerDict.Values)
                {
                    if (reader.GetReaderName().Equals(allSettings.Antennas.ReaderName, StringComparison.OrdinalIgnoreCase))
                    {
                        reader?.UpdateAntennas(allSettings.Antennas.Antennas);
                        break;
                    }
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
            }
        }));
    }

    public void CloseWindow()
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "CloseWindow.");
        Application.Current!.Dispatcher.Invoke(new Action(Close));
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        reader?.SettingsWindowFinalize();
    }

    private void VolumeSlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (VolumeSlider != null && VolumeBlock != null)
        {
            VolumeBlock.Text = VolumeSlider.Value.ToString();
        }
    }

    private void ReaderExpander_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Reader expander expanding/contracting.");
        if (ReaderExpander.IsExpanded)
        {
            AddReaderButton.IsVisible = true;
        }
        else
        {
            AddReaderButton.IsVisible = false;
        }
    }

    private void AddReaderButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Adding new reader.");
        reader?.SendSaveReader(new()
        {
            Id = -1,
            Name = "New Reader",
            Kind = PortalReader.READER_KIND_ZEBRA,
            IPAddress = "192.168.1.0",
            Port = uint.Parse(PortalReader.READER_DEFAULT_PORT_ZEBRA),
            AutoConnect = true,
        });
    }

    private void APIExpander_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "API expander expanding/contracting.");
        if (ApiExpander.IsExpanded)
        {
            AddAPIButton.IsVisible = true;
        }
        else
        {
            AddAPIButton.IsVisible = false;
        }
    }

    private void AddAPIButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Add API button clicked.");
        reader?.SendSaveApi(new()
        {
            Id = -1,
            Nickname = "New API",
            Kind = PortalAPI.API_TYPE_CHRONOKEEP_REMOTE,
            Token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
            Uri = PortalAPI.API_URI_CHRONOKEEP_REMOTE,
        });
    }

    private void UploadSlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (UploadSlider != null && UploadBlock != null)
        {
            UploadBlock.Text = UploadSlider.Value.ToString();
        }
    }

    private void BeepSlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (BeepSlider != null && BeepBlock != null)
        {
            BeepBlock.Text = BeepSlider.Value.ToString();
        }
    }

    private void ManualResultsButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Manually uploading results.");
        reader?.SendManualResultsUpload();
    }

    private void DeleteReadsButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "User requests deletion of reader chip reads.");
        DialogBox.Show("This will delete all of the chip reads from the reader.  This action is not reversible. Continue?", "Yes", "No", () =>
        {
            Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Clearing chip reads from reader.");
            reader?.SendDeleteAllReads();
        });
    }

    private void UpdateServerButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Update button clicked.");
        DialogBox.Show(
            "This will update the portal software. Do you want to proceed?",
            "Yes",
            "No",
            () =>
            {
                // send update command
                reader?.SendUpdate();
                this.Close();
            }
            );
    }

    private void RestartServerButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Restart button clicked.");
        DialogBox.Show(
            "This will restart the portal software. Do you want to proceed?",
            "Yes",
            "No",
            () =>
            {
                // send restart command
                reader?.SendRestart();
                this.Close();
            }
            );
    }

    private void StopServerButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Stop button clicked.");
        DialogBox.Show(
            "This will stop the portal software. Do you want to proceed?",
            "Yes",
            "No",
            () =>
            {
                // send stop command
                reader?.SendQuit();
                this.Close();
            }
            );
    }

    private void ShutdownServerButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Shutdown button clicked.");
        DialogBox.Show(
            "This will shutdown the entire computer the portal software is running on. Do you want to proceed?",
            "Yes",
            "No",
            () =>
            {
                // send shutdown command
                reader?.SendShutdown();
                this.Close();
            }
            );
    }

    private void SaveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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
                NtfyURL = NtfyUrlBox.Text!.Trim(),
                NtfyTopic = NtfyTopicBox.Text!.Trim(),
                NtfyUser = NtfyUserBox.Text!.Trim(),
                NtfyPass = NtfyPassBox.Text!.Trim(),
                EnableNTFY = EnableNTFYSwitch.IsChecked == true,
                ScreenType = ScreenBox.SelectedItem != null ? (string)((ComboBoxItem)ScreenBox.SelectedItem).Tag! : ""
            };
            reader?.SendSetSettings(sett);
        }
        catch (Exception ex)
        {
            Log.E("UI.Timing.ReaderSettings.ChronokeepSettings", "Error saving settings: " + ex.Message);
            DialogBox.Show("Error saving settings.");
        }
    }

    private void CloseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Close button clicked.");
        this.Close();
    }

    private void AutoResultsSwitch_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.Timing.ReaderSettings.ChronokeepSettings", "Auto upload switched.");
        if (AutoResultsSwitch.IsEnabled == false)
        {
            return;
        }
        if (AutoResultsSwitch.IsChecked == false)
        {
            reader?.SendAutoUploadResults(Objects.ChronokeepPortal.Requests.AutoUploadQuery.STOP);
        }
        else
        {
            reader?.SendAutoUploadResults(Objects.ChronokeepPortal.Requests.AutoUploadQuery.START);
        }
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}