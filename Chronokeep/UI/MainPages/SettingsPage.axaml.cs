using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.UI.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace Chronokeep.UI.MainPages;

public partial class SettingsPage : UserControl, IMainPage
{
    private readonly IMainWindow mWindow;
    private readonly IDBInterface database;

    private readonly int themeOffset = -1;

    public SettingsPage(IMainWindow mainWindow, IDBInterface database)
    {
        InitializeComponent();
        this.mWindow = mainWindow;
        this.database = database;
        DefaultTimingBox.Items.Clear();
        DefaultTimingBox.Items.Add(new ComboBoxItem()
        {
            Content = Constants.Readers.SYSTEM_NAMES[Constants.Readers.SYSTEM_RFID],
            Tag = Constants.Readers.SYSTEM_RFID
        });
        DefaultTimingBox.Items.Add(new ComboBoxItem()
        {
            Content = Constants.Readers.SYSTEM_NAMES[Constants.Readers.SYSTEM_CHRONOKEEP_PORTAL],
            Tag = Constants.Readers.SYSTEM_CHRONOKEEP_PORTAL
        });
        DefaultTimingBox.Items.Add(new ComboBoxItem()
        {
            Content = Constants.Readers.SYSTEM_NAMES[Constants.Readers.SYSTEM_IPICO],
            Tag = Constants.Readers.SYSTEM_IPICO
        });
        DefaultTimingBox.Items.Add(new ComboBoxItem()
        {
            Content = Constants.Readers.SYSTEM_NAMES[Constants.Readers.SYSTEM_IPICO_LITE],
            Tag = Constants.Readers.SYSTEM_IPICO_LITE
        });
        int systemTheme = Utils.GetSystemTheme();
        if (systemTheme != -1)
        {
            themeOffset = 0;
            ThemeColorBox.Items.Add(new ComboBoxItem()
            {
                Content = "System",
                Tag = Constants.Settings.THEME_SYSTEM
            });
        }
        ThemeColorBox.Items.Add(new ComboBoxItem()
        {
            Content = "Light",
            Tag = Constants.Settings.THEME_LIGHT
        });
        ThemeColorBox.Items.Add(new ComboBoxItem()
        {
            Content = "Dark",
            Tag = Constants.Settings.THEME_DARK
        });
        UpdateView();
    }

    public void UpdateView()
    {
        AppSetting setting = database.GetAppSetting(Constants.Settings.DEFAULT_TIMING_SYSTEM)!;
        DefaultTimingBox.SelectedIndex = setting.Value switch
        {
            Constants.Readers.SYSTEM_CHRONOKEEP_PORTAL => 1,
            Constants.Readers.SYSTEM_IPICO => 2,
            Constants.Readers.SYSTEM_IPICO_LITE => 3,
            _ => 0,
        };
        CompanyNameBox.Text = database.GetAppSetting(Constants.Settings.COMPANY_NAME)!.Value;
        ContactEmailBox.Text = database.GetAppSetting(Constants.Settings.CONTACT_EMAIL)!.Value;
        DefaultExportDirBox.Text = database.GetAppSetting(Constants.Settings.DEFAULT_EXPORT_DIR)!.Value;
        UpdatePage.IsChecked = database.GetAppSetting(Constants.Settings.UPDATE_ON_PAGE_CHANGE)!.Value == Constants.Settings.SETTING_TRUE;
        ExitNoPrompt.IsChecked = database.GetAppSetting(Constants.Settings.EXIT_NO_PROMPT)!.Value == Constants.Settings.SETTING_TRUE;
        CheckUpdates.IsChecked = database.GetAppSetting(Constants.Settings.CHECK_UPDATES)!.Value == Constants.Settings.SETTING_TRUE;
        AutoChangelog.IsChecked = database.GetAppSetting(Constants.Settings.AUTO_SHOW_CHANGELOG)!.Value == Constants.Settings.SETTING_TRUE;
        AppSetting themeSetting = database.GetAppSetting(Constants.Settings.CURRENT_THEME)!;
        Log.D("UI.MainPages.SettingsPage", "Current theme set to " + themeSetting.Value + " Theme Offset is " + themeOffset);
        switch (themeSetting.Value)
        {
            case Constants.Settings.THEME_SYSTEM:
                Log.D("UI.MainPages.SettingsPage", "Setting selected theme to System.");
                ThemeColorBox.SelectedIndex = 0;
                break;
            case Constants.Settings.THEME_LIGHT:
                Log.D("UI.MainPages.SettingsPage", "Setting selected theme to Light. " + (themeOffset + 1));
                ThemeColorBox.SelectedIndex = themeOffset + 1;
                break;
            default:
                Log.D("UI.MainPages.SettingsPage", "Setting selected theme to Dark. " + (themeOffset + 2));
                ThemeColorBox.SelectedIndex = themeOffset + 2;
                break;
        }
        if (int.TryParse(database.GetAppSetting(Constants.Settings.UPLOAD_INTERVAL)!.Value, out int uploadInt) && uploadInt is > 0 and < 60)
        {
            UploadSlider.Value = uploadInt;
            UploadBlock.Text = uploadInt.ToString();
        }
        if (int.TryParse(database.GetAppSetting(Constants.Settings.DOWNLOAD_INTERVAL)!.Value, out int downloadInt) && downloadInt is > 0 and < 60)
        {
            DownloadSlider.Value = downloadInt;
            DownloadBlock.Text = downloadInt.ToString();
        }
        if (int.TryParse(database.GetAppSetting(Constants.Settings.ANNOUNCER_WINDOW)!.Value, out int announcerWindow) && announcerWindow is >= 15 and <= 180)
        {
            AnnouncerSlider.Value = announcerWindow;
            AnnouncerBlock.Text = announcerWindow.ToString();
        }
        if (int.TryParse(database.GetAppSetting(Constants.Settings.ALARM_SOUND)!.Value, out int alarm))
        {
            AlarmSoundBox.SelectedIndex = alarm;
        }
        RegistrationServerNameBox.Text = database.GetAppSetting(Constants.Settings.SERVER_NAME)!.Value;
        TwilioAccountSidBox.Text = database.GetAppSetting(Constants.Settings.TWILIO_ACCOUNT_SID)!.Value;
        TwilioAuthTokenBox.Text = database.GetAppSetting(Constants.Settings.TWILIO_AUTH_TOKEN)!.Value;
        TwilioPhoneNumberBox.Text = database.GetAppSetting(Constants.Settings.TWILIO_PHONE_NUMBER)!.Value;
        MailgunFromNameBox.Text = database.GetAppSetting(Constants.Settings.MAILGUN_FROM_NAME)!.Value;
        MailgunFromEmailBox.Text = database.GetAppSetting(Constants.Settings.MAILGUN_FROM_EMAIL)!.Value;
        MailgunApiKeyBox.Text = database.GetAppSetting(Constants.Settings.MAILGUN_API_KEY)!.Value;
        MailgunApiUrlBox.Text = database.GetAppSetting(Constants.Settings.MAILGUN_API_URL)!.Value;
        UniqueProgramId.Text = database.GetAppSetting(Constants.Settings.PROGRAM_UNIQUE_MODIFIER)!.Value;
    }

    private void SaveSettings()
    {
        Log.D("UI.MainPages.SettingsPage", "Saving.");
        database.SetAppSetting(Constants.Settings.COMPANY_NAME, CompanyNameBox.Text!.Trim());
        database.SetAppSetting(Constants.Settings.CONTACT_EMAIL, ContactEmailBox.Text!.Trim());
        database.SetAppSetting(Constants.Settings.DEFAULT_TIMING_SYSTEM, (string)((ComboBoxItem)DefaultTimingBox.SelectedItem!).Tag!);
        database.SetAppSetting(Constants.Settings.CURRENT_THEME, (string)((ComboBoxItem)ThemeColorBox.SelectedItem!).Tag!);
        database.SetAppSetting(Constants.Settings.DEFAULT_EXPORT_DIR, DefaultExportDirBox.Text!.Trim());
        database.SetAppSetting(Constants.Settings.UPDATE_ON_PAGE_CHANGE, UpdatePage.IsChecked == true ? Constants.Settings.SETTING_TRUE : Constants.Settings.SETTING_FALSE);
        database.SetAppSetting(Constants.Settings.EXIT_NO_PROMPT, ExitNoPrompt.IsChecked == true ? Constants.Settings.SETTING_TRUE : Constants.Settings.SETTING_FALSE);
        database.SetAppSetting(Constants.Settings.CHECK_UPDATES, CheckUpdates.IsChecked == true ? Constants.Settings.SETTING_TRUE : Constants.Settings.SETTING_FALSE);
        database.SetAppSetting(Constants.Settings.AUTO_SHOW_CHANGELOG, AutoChangelog.IsChecked == true ? Constants.Settings.SETTING_TRUE : Constants.Settings.SETTING_FALSE);
        database.SetAppSetting(Constants.Settings.UPLOAD_INTERVAL, Convert.ToInt32(UploadSlider.Value).ToString());
        Globals.UploadInterval = Convert.ToInt32(UploadSlider.Value);
        database.SetAppSetting(Constants.Settings.DOWNLOAD_INTERVAL, Convert.ToInt32(DownloadSlider.Value).ToString());
        Globals.DownloadInterval = Convert.ToInt32(DownloadSlider.Value);
        database.SetAppSetting(Constants.Settings.ANNOUNCER_WINDOW, Convert.ToInt32(AnnouncerSlider.Value).ToString());
        Globals.AnnouncerWindow = Convert.ToInt32(AnnouncerSlider.Value);
        database.SetAppSetting(Constants.Settings.ALARM_SOUND, AlarmSoundBox.SelectedIndex.ToString());
        database.SetAppSetting(Constants.Settings.SERVER_NAME, RegistrationServerNameBox.Text!.Trim());

        Constants.GlobalVars.SetTwilioCredentials(TwilioAccountSidBox.Text!.Trim(), TwilioAuthTokenBox.Text!.Trim(), TwilioPhoneNumberBox.Text!.Trim());
        database.SetAppSetting(Constants.Settings.TWILIO_ACCOUNT_SID, Constants.GlobalVars.TwilioCredentials.AccountSid);
        database.SetAppSetting(Constants.Settings.TWILIO_AUTH_TOKEN, Constants.GlobalVars.TwilioCredentials.AuthToken);
        database.SetAppSetting(Constants.Settings.TWILIO_PHONE_NUMBER, Constants.GlobalVars.TwilioCredentials.PhoneNumber);

        database.SetAppSetting(Constants.Settings.MAILGUN_FROM_NAME, MailgunFromNameBox.Text!.Trim());
        database.SetAppSetting(Constants.Settings.MAILGUN_FROM_EMAIL, MailgunFromEmailBox.Text!.Trim());
        database.SetAppSetting(Constants.Settings.MAILGUN_API_KEY, MailgunApiKeyBox.Text!.Trim());
        database.SetAppSetting(Constants.Settings.MAILGUN_API_URL, MailgunApiUrlBox.Text!.Trim());
    }

    public static void UpdateDatabase() { }

    public void Keyboard_Ctrl_A() { }

    public void Keyboard_Ctrl_S()
    {
        Save_Click(null, null);
    }

    public void Keyboard_Ctrl_Z()
    {
        UpdateView();
    }

    public void Closing()
    {
        Log.D("UI.MainPages.SettingsPage", "Closing page.");
        if (UpdatePage.IsChecked == true)
        {
            SaveSettings();
        }
    }

    private async void ResetDB_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.MainPages.SettingsPage", "Reset button clicked.");
            bool yesClicked = false;
            DialogBox.Show(
                "This deletes all of the data stored in the database.  You cannot recover any of the data in the database after this step.\n\nAre you sure you wish to continue?",
                "Yes",
                "No",
                () =>
                {
                    yesClicked = true;
                });
            if (!yesClicked) return;
            ResetDb.IsEnabled = false;
            await Task.Run(() =>
            {
                database.ResetDatabase();
                Constants.Settings.SetupSettings(database);
            });
            UpdateView();
            ResetDb.IsEnabled = true;
            mWindow.UpdateStatus();
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.SettingsPage", "Error resetting database.");
        }
    }

    private async void RebuildDB_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.MainPages.SettingsPage", "Rebuild button clicked.");
            bool yesClicked = false;
            DialogBox.Show(
                "This deletes all of the tables and values in the database, then rebuilds all of the tables.  You cannot recover any of the data in the database after this step.\n\nAre you sure you wish to continue?",
                "Yes",
                "No",
                () =>
                {
                    yesClicked = true;
                });
            if (!yesClicked) return;
            RebuildDb.IsEnabled = false;
            await Task.Run(() =>
            {
                database.HardResetDatabase();
                Constants.Settings.SetupSettings(database);
            });
            UpdateView();
            RebuildDb.IsEnabled = true;
            mWindow.UpdateStatus();
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.SettingsPage", "Error rebuilding database.");
        }
    }

    private void Save_Click(object? sender, RoutedEventArgs? e)
    {
        Log.D("UI.MainPages.SettingsPage", "Save button clicked.");
        SaveSettings();
        UpdateView();
    }

    private void ThemeColorBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ThemeColorBox.SelectedItem is not ComboBoxItem selectedItem) return;
        database.SetAppSetting(Constants.Settings.CURRENT_THEME, (string)((ComboBoxItem)ThemeColorBox.SelectedItem!).Tag!);
        string theme = selectedItem.Tag != null ? (string)selectedItem.Tag : "light";
        mWindow.UpdateTheme(theme);
    }

    private async void ChangeExport_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.MainPages.SettingsPage", "Change export directory button clicked.");
            TopLevel? topLevel = TopLevel.GetTopLevel((Window)mWindow);
            if (topLevel == null) return;
            IStorageFolder? oldFold;
            try
            {
                oldFold = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(DefaultExportDirBox.Text ?? ""));
            }
            catch
            {
                oldFold = null;
            }
            IReadOnlyList<IStorageFolder> folder = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Default Export Directory",
                SuggestedStartLocation = oldFold,
            });
            if (folder.Count > 0)
            {
                DefaultExportDirBox.Text = folder[0].Path.ToString();
            }
        }
        catch (Exception)
        {
            Log.D("UI.MainPages.SettingsPage", "Error changing export directory.");
        }
    }

    private void UploadSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (UploadSlider != null && UploadBlock != null)
        {
            UploadBlock.Text = UploadSlider.Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private void DownloadSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (DownloadSlider != null && DownloadBlock != null)
        {
            DownloadBlock.Text = DownloadSlider.Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private void AnnouncerSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (AnnouncerSlider != null && AnnouncerBlock != null)
        {
            AnnouncerBlock.Text = AnnouncerSlider.Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private void PlayBtn_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.SettingsPage", "Play alarm sound clicked.");
        try
        {
            AudioPlaybackEngine.PlaySound(AlarmSoundBox.SelectedIndex);
        }
        catch (ArgumentException) { }
        catch (Exception ex)
        {
            DialogBox.Show("Error trying to play sound. " + ex.Message + ex.GetType());
        }
    }

    private void RegenerateUniqueProgramIDButton_Click(object? sender, RoutedEventArgs e)
    {
        string randomMod = Constants.Settings.AlphaNum().Replace(Guid.NewGuid().ToString("N"), "").ToUpper()[0..3];
        database.SetAppSetting(Constants.Settings.PROGRAM_UNIQUE_MODIFIER, randomMod);
        UpdateView();
    }
}