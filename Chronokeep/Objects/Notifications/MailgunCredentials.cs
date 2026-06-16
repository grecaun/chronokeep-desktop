using Chronokeep.Database;

namespace Chronokeep.Objects.Notifications
{
    public class MailgunCredentials
    {
        public string Username { get; set; } = "api";
        public string ApiKey { get; private init; } = "";
        private string FromName { get; init; } = "";
        private string FromEmail { get; init; } = "";
        public string Domain { get; private init; } = "";

        public bool Valid()
        {
            return ApiKey.Length > 0 && Domain.Length > 0 && FromEmail.Length > 0;
        }

        public string From()
        {
            return FromName.Length > 0 ? $"{FromName} <{FromEmail}>" : FromEmail;
        }

        public static MailgunCredentials GetCredentials(IdbInterface database)
        {
            AppSetting apiKey = database.GetAppSetting(Constants.Settings.MAILGUN_API_KEY)!;
            AppSetting domain = database.GetAppSetting(Constants.Settings.MAILGUN_API_URL)!;
            AppSetting fromEmail = database.GetAppSetting(Constants.Settings.MAILGUN_FROM_EMAIL)!;
            AppSetting fromName = database.GetAppSetting(Constants.Settings.MAILGUN_FROM_NAME)!;
            MailgunCredentials output = new()
            {
                ApiKey = apiKey.Value,
                Domain = domain.Value,
                FromEmail = fromEmail.Value,
                FromName = fromName.Value
            };
            return output;
        }
    }
}
