using Chronokeep.Database;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Chronokeep.Constants
{
    partial class Settings
    {
        public const string PROGRAM_DIR           = "Chronokeep";
        public const string HELP_DIR              = "help";

        // Settings 1
        public const string SERVER_NAME                 = "SETTING_SERVER_NAME";
        public const string DATABASE_VERSION            = "DATABASE_VERSION";           // default is database managed
        public const string HARDWARE_IDENTIFIER         = "HARDWARE_IDENTIFIER";        // used to detect if hardware changes so we can update SETTING_UNIQUE_MODIFIER if the user desires
        public const string PROGRAM_VERSION             = "PROGRAM_VERSION";
        public const string AUTO_SHOW_CHANGELOG         = "AUTO_SHOW_CHANGELOG";
        // Settings 2
        public const string DEFAULT_EXPORT_DIR          = "SETTING_DEFAULT_EXPORT_DIR";
        public const string DEFAULT_TIMING_SYSTEM       = "SETTING_DEFAULT_TIMING_SYSTEM";
        public const string CURRENT_EVENT               = "SETTING_CURRENT_EVENT";
        public const string COMPANY_NAME                = "SETTING_COMPANY_NAME";
        public const string CONTACT_EMAIL               = "SETTING_CONTACT_EMAIL";
        // Settings 3
        public const string UPDATE_ON_PAGE_CHANGE       = "SETTING_UPDATE_PAGE_CHANGE";
        public const string EXIT_NO_PROMPT              = "EXIT_NO_PROMPT";
        public const string DEFAULT_CHIP_TYPE           = "DEFAULT_CHIP_TYPE";
        public const string LAST_USED_API_ID            = "SETTING_LAST_USED_API_ID";   // default is not set
        public const string CHECK_UPDATES               = "SETTING_CHECK_UPDATES";
        public const string CURRENT_THEME               = "SETTING_THEME";
        // Settings 4
        public const string UPLOAD_INTERVAL             = "SETTING_UPLOAD_INTERVAL";
        public const string DOWNLOAD_INTERVAL           = "SETTINGS_DOWNLOAD_INTERVAL";
        public const string ANNOUNCER_WINDOW            = "SETTING_ANNOUNCER_WINDOW";
        public const string ALARM_SOUND                 = "SETTING_ALARM_SOUND";
        public const string MINIMUM_COMPATIBLE_DATABASE = "SETTING_MINIMUM_COMPATIBLE_DATABASE";
        // Settings 5
        public const string PROGRAM_UNIQUE_MODIFIER     = "SETTING_UNIQUE_MODIFIER";
        // Twilio
        public const string TWILIO_ACCOUNT_SID    = "TWILIO_ACCOUNT_SID";
        public const string TWILIO_AUTH_TOKEN     = "TWILIO_AUTH_TOKEN";
        public const string TWILIO_PHONE_NUMBER   = "TWILIO_PHONE_NUMBER";
        // Mailgun
        public const string MAILGUN_API_KEY       = "MAILGUN_API_KEY";
        public const string MAILGUN_API_URL       = "MAILGUN_API_URL";
        public const string MAILGUN_FROM_EMAIL    = "MAILGUN_FROM_EMAIL";
        public const string MAILGUN_FROM_NAME     = "MAILGUN_FROM_NAME";

        public const string NULL_EVENT_ID     = "-1";

        public const string SETTING_TRUE      = "TRUE";
        public const string SETTING_FALSE     = "FALSE";

        public const string THEME_SYSTEM      = "THEME_SYSTEM";
        public const string THEME_DARK        = "THEME_DARK";
        public const string THEME_LIGHT       = "THEME_LIGHT";

        public const string CHIP_TYPE_DEC     = "DEC";
        public const string CHIP_TYPE_HEX     = "HEX";

        public const string WAIVER_YEAR       = "[YEAR]";
        public const string WAIVER_EVENT      = "[EVENT]";
        public const string WAIVER_COMPANY    = "[COMPANY]";

        public const string DEFAULT_INTERVAL  = "30";
        public const string DEFAULT_ANNOUNCER = "45";
        public const string DEFAULT_ALARM     = "1";

        [GeneratedRegex("[^a-zA-Z0-9]")]
        public static partial Regex AlphaNum();

        public static void SetupSettings(IDBInterface database)
        {
            // Settings 1
            if (database.GetAppSetting(SERVER_NAME) == null)
            {
                database.SetAppSetting(SERVER_NAME, "Chronokeep Registration");
            }
            if (database.GetAppSetting(AUTO_SHOW_CHANGELOG) == null)
            {
                database.SetAppSetting(AUTO_SHOW_CHANGELOG, SETTING_TRUE);
            }
            // Settings 2
            if (database.GetAppSetting(DEFAULT_EXPORT_DIR) == null)
            {
                string dirPath = App.IsWindows ?
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), PROGRAM_DIR, "exports")
                    : Path.Combine(Directory.GetCurrentDirectory(), "exports");
                database.SetAppSetting(DEFAULT_EXPORT_DIR, dirPath);
            }
            if (database.GetAppSetting(DEFAULT_TIMING_SYSTEM) == null)
            {
                database.SetAppSetting(DEFAULT_TIMING_SYSTEM, Readers.DEFAULT_TIMING_SYSTEM);
            }
            if (database.GetAppSetting(CURRENT_EVENT) == null)
            {
                database.SetAppSetting(CURRENT_EVENT, NULL_EVENT_ID);
            }
            if (database.GetAppSetting(COMPANY_NAME) == null)
            {
                database.SetAppSetting(COMPANY_NAME, "");
            }
            if (database.GetAppSetting(CONTACT_EMAIL) == null)
            {
                database.SetAppSetting(CONTACT_EMAIL, "");
            }
            // Settings 3
            if (database.GetAppSetting(UPDATE_ON_PAGE_CHANGE) == null)
            {
                database.SetAppSetting(UPDATE_ON_PAGE_CHANGE, SETTING_TRUE);
            }
            if (database.GetAppSetting(EXIT_NO_PROMPT) == null)
            {
                database.SetAppSetting(EXIT_NO_PROMPT, SETTING_FALSE);
            }
            if (database.GetAppSetting(DEFAULT_CHIP_TYPE) == null)
            {
                database.SetAppSetting(DEFAULT_CHIP_TYPE, CHIP_TYPE_DEC);
            }
            if (database.GetAppSetting(CHECK_UPDATES) == null)
            {
                database.SetAppSetting(CHECK_UPDATES, SETTING_FALSE);
            }
            if (database.GetAppSetting(CURRENT_THEME) == null)
            {
                database.SetAppSetting(CURRENT_THEME, THEME_LIGHT);
            }
            // Settings 4
            if (database.GetAppSetting(UPLOAD_INTERVAL) == null)
            {
                database.SetAppSetting(UPLOAD_INTERVAL, DEFAULT_INTERVAL);
            }
            if (database.GetAppSetting(DOWNLOAD_INTERVAL) == null)
            {
                database.SetAppSetting(DOWNLOAD_INTERVAL, DEFAULT_INTERVAL);
            }
            if (database.GetAppSetting(ANNOUNCER_WINDOW) == null)
            {
                database.SetAppSetting(ANNOUNCER_WINDOW, DEFAULT_ANNOUNCER);
            }
            if (database.GetAppSetting(ALARM_SOUND) == null)
            {
                database.SetAppSetting(ALARM_SOUND, DEFAULT_ALARM);
            }
            if (database.GetAppSetting(MINIMUM_COMPATIBLE_DATABASE) == null)
            {
                database.SetAppSetting(MINIMUM_COMPATIBLE_DATABASE, SQLiteInterface.minimum_compatible_version.ToString());
            }
            // Settings 5
            if (database.GetAppSetting(PROGRAM_UNIQUE_MODIFIER) == null)
            {
                string randomMod = AlphaNum().Replace(Guid.NewGuid().ToString("N"), "").ToUpper()[0..3];
                database.SetAppSetting(PROGRAM_UNIQUE_MODIFIER, randomMod);
            }
            // Twilio
            if (database.GetAppSetting(TWILIO_ACCOUNT_SID) == null)
            {
                database.SetAppSetting(TWILIO_ACCOUNT_SID, "");
            }
            if (database.GetAppSetting(TWILIO_AUTH_TOKEN) == null)
            {
                database.SetAppSetting(TWILIO_AUTH_TOKEN, "");
            }
            if (database.GetAppSetting(TWILIO_PHONE_NUMBER) == null)
            {
                database.SetAppSetting(TWILIO_PHONE_NUMBER, "");
            }
            // Mailgun
            if (database.GetAppSetting(MAILGUN_API_KEY) == null)
            {
                database.SetAppSetting(MAILGUN_API_KEY, "");
            }
            if (database.GetAppSetting(MAILGUN_API_URL) == null)
            {
                database.SetAppSetting(MAILGUN_API_URL, "");
            }
            if (database.GetAppSetting(MAILGUN_FROM_EMAIL) == null)
            {
                database.SetAppSetting(MAILGUN_FROM_EMAIL, "");
            }
            if (database.GetAppSetting(MAILGUN_FROM_NAME) == null)
            {
                database.SetAppSetting(MAILGUN_FROM_NAME, "");
            }
        }
    }
}
