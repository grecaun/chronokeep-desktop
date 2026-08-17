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

