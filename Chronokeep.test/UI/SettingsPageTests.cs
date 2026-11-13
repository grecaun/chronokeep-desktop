using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chronokeep.Helpers;
using Chronokeep.Objects;
using Chronokeep.Test.UI.Infrastructure;
using Chronokeep.UI.MainPages;
using static Chronokeep.Constants.Settings;

namespace Chronokeep.Test.UI
{
    /// <summary>The Settings page: loading, saving, the theme picker, the program id, and database resets.</summary>
    public class SettingsPageTests : UiTestBase
    {
        private readonly (int Upload, int Download, int Announcer, int Seen) savedGlobals =
            (Globals.UploadInterval, Globals.DownloadInterval, Globals.AnnouncerWindow, Globals.SeenWindow);

        public override void Dispose()
        {
            // Saving settings also sets these process-wide values; put them back for other tests.
            (Globals.UploadInterval, Globals.DownloadInterval, Globals.AnnouncerWindow, Globals.SeenWindow) = savedGlobals;
            base.Dispose();
        }

        private SettingsPage Open()
        {
            SettingsPage page = new(MainWindow, Db);
            Host(page);
            return page;
        }

        private string Setting(string name) => Db.GetAppSetting(name)!.Value;

        private static void SelectByTag(ComboBox box, string tag)
        {
            box.SelectedItem = box.Items.OfType<ComboBoxItem>().Single(i => (string)i.Tag! == tag);
            Pump();
        }

        [AvaloniaFact]
        public void Opening_shows_the_stored_settings()
        {
            Db.SetAppSetting(COMPANY_NAME, "Northwest Timing");
            Db.SetAppSetting(CONTACT_EMAIL, "results@example.com");
            Db.SetAppSetting(DEFAULT_TIMING_SYSTEM, Constants.Readers.SYSTEM_IPICO);
            Db.SetAppSetting(ANNOUNCER_WINDOW, "120");
            Db.SetAppSetting(EXIT_NO_PROMPT, SETTING_TRUE);

            SettingsPage page = Open();

            Assert.Equal("Northwest Timing", Find<TextBox>(page, "CompanyNameBox").Text);
            Assert.Equal("results@example.com", Find<TextBox>(page, "ContactEmailBox").Text);
            Assert.Equal(Constants.Readers.SYSTEM_IPICO, ((ComboBoxItem)Find<ComboBox>(page, "DefaultTimingBox").SelectedItem!).Tag);
            Assert.Equal(120, Find<Slider>(page, "AnnouncerSlider").Value);
            Assert.True(Find<ToggleSwitch>(page, "ExitNoPrompt").IsChecked);
        }

        [AvaloniaFact]
        public void Save_stores_edited_settings_and_trims_text()
        {
            SettingsPage page = Open();
            Find<TextBox>(page, "CompanyNameBox").Text = "  Northwest Timing  ";
            Find<TextBox>(page, "ContactEmailBox").Text = "results@example.com";
            Find<TextBox>(page, "RegistrationServerNameBox").Text = "Front Desk";
            Find<TextBox>(page, "MailgunFromNameBox").Text = "Race Results";
            SelectByTag(Find<ComboBox>(page, "DefaultTimingBox"), Constants.Readers.SYSTEM_CHRONOKEEP_PORTAL);
            Find<ToggleSwitch>(page, "CheckUpdates").IsChecked = false;
            Find<Slider>(page, "AnnouncerSlider").Value = 300;
            Find<Slider>(page, "AnnRepSlider").Value = 12;

            Click(Find<Button>(page, "Save"));

            Assert.Equal("Northwest Timing", Setting(COMPANY_NAME));
            Assert.Equal("results@example.com", Setting(CONTACT_EMAIL));
            Assert.Equal("Front Desk", Setting(SERVER_NAME));
            Assert.Equal("Race Results", Setting(MAILGUN_FROM_NAME));
            Assert.Equal(Constants.Readers.SYSTEM_CHRONOKEEP_PORTAL, Setting(DEFAULT_TIMING_SYSTEM));
            Assert.Equal(SETTING_FALSE, Setting(CHECK_UPDATES));
            Assert.Equal("300", Setting(ANNOUNCER_WINDOW));
            Assert.Equal("12", Setting(SEEN_WINDOW));
            Assert.Equal(300, Globals.AnnouncerWindow);
            Assert.Equal(12, Globals.SeenWindow);
            Assert.Equal("Northwest Timing", Find<TextBox>(page, "CompanyNameBox").Text);
        }

        [AvaloniaFact]
        public void Leaving_the_page_saves_only_when_save_on_page_change_is_on()
        {
            Db.SetAppSetting(UPDATE_ON_PAGE_CHANGE, SETTING_TRUE);
            SettingsPage saving = Open();
            Find<TextBox>(saving, "CompanyNameBox").Text = "Saved on leave";
            saving.Closing();
            Assert.Equal("Saved on leave", Setting(COMPANY_NAME));

            SettingsPage notSaving = Open();
            Find<ToggleSwitch>(notSaving, "UpdatePage").IsChecked = false;
            Find<TextBox>(notSaving, "CompanyNameBox").Text = "Not saved";
            notSaving.Closing();
            Assert.Equal("Saved on leave", Setting(COMPANY_NAME));
        }

        [AvaloniaFact]
        public void Picking_a_theme_applies_and_stores_it_right_away()
        {
            SettingsPage page = Open();
            MainWindow.Themes.Clear();

            SelectByTag(Find<ComboBox>(page, "ThemeColorBox"), THEME_DARK);

            Assert.Equal(THEME_DARK, Setting(CURRENT_THEME));
            Assert.Equal(THEME_DARK, MainWindow.Themes.Last());
        }

        [AvaloniaFact]
        public void Regenerate_replaces_the_program_id_with_three_new_characters()
        {
            Db.SetAppSetting(PROGRAM_UNIQUE_MODIFIER, "AAA");
            SettingsPage page = Open();

            Click(Find<Button>(page, "RegenerateUniqueProgramIdButton"));

            string id = Setting(PROGRAM_UNIQUE_MODIFIER);
            Assert.Matches("^[A-Z0-9]{3}$", id);
            Assert.NotEqual("AAA", id);
            Assert.Equal(id, Find<TextBox>(page, "UniqueProgramId").Text);
        }

        [AvaloniaFact]
        public void Reset_and_rebuild_buttons_do_nothing_until_confirmed()
        {
            SeedEvent();
            SettingsPage page = Open();

            // The confirmation needs the real main window, so it is never answered here.
            Click(Find<Button>(page, "ResetDb"));
            Click(Find<Button>(page, "RebuildDb"));

            Assert.Single(Db.GetEvents());
        }

        [AvaloniaTheory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Confirmed_reset_clears_events_and_restores_default_settings(bool hard)
        {
            SeedEvent();
            Db.SetAppSetting(COMPANY_NAME, "Northwest Timing");
            SettingsPage page = Open();
            int statusUpdates = MainWindow.UpdateStatusCalls;

            await page.ResetDatabase(hard);

            Assert.Empty(Db.GetEvents());
            Assert.NotNull(Db.GetAppSetting(UPDATE_ON_PAGE_CHANGE));
            Assert.Equal("", Find<TextBox>(page, "CompanyNameBox").Text);
            Assert.True(Find<Button>(page, hard ? "RebuildDb" : "ResetDb").IsEnabled);
            Assert.Equal(statusUpdates + 1, MainWindow.UpdateStatusCalls);
        }
    }
}
