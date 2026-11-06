using Chronokeep.Helpers;
using Chronokeep.Test.Database;
using TimingConstants = Chronokeep.Constants.Timing;

namespace Chronokeep.Test.Logic
{
    public class TimingConstantsTests
    {
        [Fact]
        public void RfidEpoch_StartsAt1980()
        {
            Assert.Equal(0, TimingConstants.RfidDateToEpoch(new DateTime(1980, 1, 1)));
            Assert.Equal(new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc), TimingConstants.RfidEpochToDate(0));
        }

        [Fact]
        public void RfidEpoch_RoundTrips()
        {
            DateTime when = new(2026, 6, 15, 8, 30, 15);
            long seconds = TimingConstants.RfidDateToEpoch(when);
            Assert.Equal(when, TimingConstants.RfidEpochToDate(seconds));
        }

        [Fact]
        public void UnixEpoch_StartsAt1970()
        {
            Assert.Equal(0, TimingConstants.UnixDateToEpoch(new DateTime(1970, 1, 1)));
            Assert.Equal(86400, TimingConstants.UnixDateToEpoch(new DateTime(1970, 1, 2)));
        }

        [Fact]
        public void UtcToLocalDate_AddsMilliseconds()
        {
            DateTime local = TimingConstants.UtcToLocalDate(0, 250);
            Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToLocalTime().AddMilliseconds(250), local);
        }

        [Fact]
        public void UtcSecondsToRfidSeconds_ConvertsThroughLocalTime()
        {
            long unix = TimingConstants.UnixDateToEpoch(new DateTime(2026, 6, 15, 12, 0, 0));
            DateTime local = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc).ToLocalTime();
            Assert.Equal(TimingConstants.RfidDateToEpoch(local), TimingConstants.UtcSecondsToRfidSeconds(unix));
        }

        [Theory]
        [InlineData(0, 0, "0:00:00.000")]
        [InlineData(3725, 5, "1:02:05.005")]
        [InlineData(36000, 999, "10:00:00.999")]
        [InlineData(100 * 3600, 0, "100:00:00.000")]
        public void ToTime_FormatsHoursMinutesSecondsMilliseconds(long seconds, int ms, string expected)
        {
            Assert.Equal(expected, TimingConstants.ToTime(seconds, ms));
        }

        [Theory]
        [InlineData(3725, 5, "01:02:05.005")]
        [InlineData(45296, 120, "12:34:56.120")]
        public void ToTimeOfDay_PadsHours(long seconds, int ms, string expected)
        {
            Assert.Equal(expected, TimingConstants.ToTimeOfDay(seconds, ms));
        }

        [Theory]
        [InlineData(0, "0:00:00")]
        [InlineData(3725, "1:02:05")]
        public void SecondsToTime_Formats(long seconds, string expected)
        {
            Assert.Equal(expected, TimingConstants.SecondsToTime(seconds));
        }

        [Theory]
        [InlineData(125, "02:05")]
        [InlineData(3725, "02:05")]
        public void SecondsToMinuteTime_DropsHours(long seconds, string expected)
        {
            Assert.Equal(expected, TimingConstants.SecondsToMinuteTime(seconds));
        }

        [Fact]
        public void EventSpecificStatusNames_CoverEveryStatus()
        {
            Assert.Equal("Unknown", TimingConstants.EVENTSPECIFIC_STATUS_NAMES[TimingConstants.EVENTSPECIFIC_UNKNOWN]);
            Assert.Equal("Started", TimingConstants.EVENTSPECIFIC_STATUS_NAMES[TimingConstants.EVENTSPECIFIC_STARTED]);
            Assert.Equal("Finished", TimingConstants.EVENTSPECIFIC_STATUS_NAMES[TimingConstants.EVENTSPECIFIC_FINISHED]);
            Assert.Equal("DNF", TimingConstants.EVENTSPECIFIC_STATUS_NAMES[TimingConstants.EVENTSPECIFIC_DNF]);
            Assert.Equal("DNS", TimingConstants.EVENTSPECIFIC_STATUS_NAMES[TimingConstants.EVENTSPECIFIC_DNS]);
        }
    }

    public class DistanceConstantsTests
    {
        [Theory]
        [InlineData(Constants.Distances.MILES, "Miles")]
        [InlineData(Constants.Distances.YARDS, "Yards")]
        [InlineData(Constants.Distances.FEET, "Feet")]
        [InlineData(Constants.Distances.METERS, "Meters")]
        [InlineData(Constants.Distances.KILOMETERS, "Kilometers")]
        [InlineData(-99, "")]
        public void DistanceString_NamesEachUnit(int unit, string expected)
        {
            Assert.Equal(expected, Constants.Distances.DistanceString(unit));
        }
    }

    public class PhoneValidationTests
    {
        [Theory]
        [InlineData("5551234567", "+15551234567")]
        [InlineData("555-123-4567", "+15551234567")]
        [InlineData("(555) 123-4567", "+15551234567")]
        [InlineData("1-555-123-4567", "+15551234567")]
        [InlineData("15551234567", "+15551234567")]
        [InlineData("+1 555 123 4567", "+15551234567")]
        [InlineData("+1 (555) 123-4567", "+15551234567")]
        public void GetValidPhone_NormalizesUsNumbers(string input, string expected)
        {
            Assert.Equal(expected, Constants.GlobalVars.GetValidPhone(input));
        }

        [Theory]
        [InlineData("")]
        [InlineData("555-1234")]
        [InlineData("not a phone")]
        [InlineData("+44 20 7946 0958")]
        [InlineData("555-123-45678")]
        public void GetValidPhone_RejectsInvalidNumbers(string input)
        {
            Assert.Equal("", Constants.GlobalVars.GetValidPhone(input));
        }
    }

    public class UtilsTests
    {
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("a", "A")]
        [InlineData("abc def", "Abc def")]
        [InlineData("Already", "Already")]
        public void UppercaseFirst_CapitalizesOnlyFirstCharacter(string? input, string expected)
        {
            Assert.Equal(expected, Utils.UppercaseFirst(input!));
        }
    }

    public class SettingsSetupTests : DatabaseTestBase
    {
        [Fact]
        public void SetupSettings_FillsInDefaults()
        {
            Constants.Settings.SetupSettings(Db);

            Assert.Equal(Constants.Settings.SETTING_TRUE, Db.GetAppSetting(Constants.Settings.UPDATE_ON_PAGE_CHANGE)!.Value);
            Assert.Equal(Constants.Settings.SETTING_FALSE, Db.GetAppSetting(Constants.Settings.EXIT_NO_PROMPT)!.Value);
            Assert.Equal(Constants.Settings.DEFAULT_INTERVAL, Db.GetAppSetting(Constants.Settings.UPLOAD_INTERVAL)!.Value);
            Assert.Equal(Constants.Settings.DEFAULT_ANNOUNCER, Db.GetAppSetting(Constants.Settings.ANNOUNCER_WINDOW)!.Value);
            Assert.Equal(Constants.Settings.NULL_EVENT_ID, Db.GetAppSetting(Constants.Settings.CURRENT_EVENT)!.Value);
            Assert.Equal(Chronokeep.Database.SqLiteInterface.MINIMUM_COMPATIBLE_VERSION.ToString(),
                Db.GetAppSetting(Constants.Settings.MINIMUM_COMPATIBLE_DATABASE)!.Value);
            Assert.Equal("", Db.GetAppSetting(Constants.Settings.TWILIO_ACCOUNT_SID)!.Value);
        }

        [Fact]
        public void SetupSettings_CreatesThreeCharacterUniqueModifier()
        {
            Constants.Settings.SetupSettings(Db);
            string modifier = Db.GetAppSetting(Constants.Settings.PROGRAM_UNIQUE_MODIFIER)!.Value;
            Assert.Matches("^[A-Z0-9]{3}$", modifier);
        }

        [Fact]
        public void SetupSettings_KeepsExistingValues()
        {
            Db.SetAppSetting(Constants.Settings.COMPANY_NAME, "Acme Timing");
            Db.SetAppSetting(Constants.Settings.PROGRAM_UNIQUE_MODIFIER, "XYZ");

            Constants.Settings.SetupSettings(Db);
            Constants.Settings.SetupSettings(Db);

            Assert.Equal("Acme Timing", Db.GetAppSetting(Constants.Settings.COMPANY_NAME)!.Value);
            Assert.Equal("XYZ", Db.GetAppSetting(Constants.Settings.PROGRAM_UNIQUE_MODIFIER)!.Value);
        }
    }
}
