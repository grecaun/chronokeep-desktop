using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Network.API;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronoKeepAPI;
using Chronokeep.Objects.Notifications;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Twilio;

namespace Chronokeep.Constants
{
    public partial class GlobalVars
    {
        // keep track of TWILIO credentials
        public static readonly TwilioCredentials TwilioCredentials = new();
        // keep track of banned phones
        public static readonly HashSet<string> BannedPhones = [];
        // keep track of local list of banned phone numbers that we need to add to the api's list
        public static readonly HashSet<string> NewBannedPhones = [];
        // keep track of banned emails
        public static readonly HashSet<string> BannedEmails = [];

        [GeneratedRegex("^(?:\\+?1)?\\s*\\-?\\s*(?:\\d{3}|\\(\\d{3}\\))\\s*\\-?\\s*\\d{3}\\s*\\-?\\s*\\d{4}$")]
        private static partial Regex Phone();
        [GeneratedRegex("\\s+")]
        private static partial Regex WhiteSpace();

        public static async void UpdateBannedPhones()
        {
            try
            {
                GetBannedPhonesResponse phonesResponse = await ApiHandlers.GetBannedPhones();
                BannedPhones.Clear();
                if (phonesResponse.Phones != null)
                {
                    foreach (string phone in phonesResponse.Phones)
                    {
                        string p = GetValidPhone(phone);
                        if (p.Length > 0)
                        {
                            BannedPhones.Add(p);
                        }
                    }
                    // make sure we've got all our new banned phone numbers in there too
                    foreach (string phone in NewBannedPhones)
                    {
                        string p = GetValidPhone(phone);
                        if (p.Length > 0)
                        {
                            BannedPhones.Add(p);
                        }
                    }
                }

            }
            catch
            {
                Log.E("Constants.Globals", "Exception getting banned phones.");
            }
            // attempt to upload all the new phone numbers
            foreach (string phone in NewBannedPhones)
            {
                string p = GetValidPhone(phone);
                if (p.Length > 0)
                {
                    try
                    {
                        await ApiHandlers.AddBannedPhone(p);
                        NewBannedPhones.Remove(phone);
                    }
                    catch
                    {
                        Log.E("Constants.Globals", "Exception uploading banned phone number.");
                    }
                }
            }
        }

        public static async void AddBannedPhone(string phone)
        {
            string p = GetValidPhone(phone);
            BannedPhones.Add(p);
            NewBannedPhones.Add(phone);
            try
            {
                await ApiHandlers.AddBannedPhone(p);
                NewBannedPhones.Remove(phone);
            }
            catch
            {
                Log.E("Constants.Globals", "Exception uploading banned phone number.");
            }
        }

        public static async void UpdateBannedEmails()
        {
            try
            {
                GetBannedEmailsResponse emailsResponse = await ApiHandlers.GetBannedEmails();
                BannedEmails.Clear();
                if (emailsResponse.Emails != null)
                {
                    foreach (string email in emailsResponse.Emails)
                    {
                        BannedEmails.Add(email);
                    }
                }
            }
            catch
            {
                Log.E("Constants.Globals", "Exception getting banned emails.");
            }
        }

        public static string GetValidPhone(string phone)
        {
            string output = "";
            if (Phone().Match(phone).Success)
            {
                string tmp = WhiteSpace().Replace(phone.Replace("-", "").Replace(")", "").Replace("(", ""), "");
                if (tmp.Length == 10)
                {
                    output = string.Format("+1{0}", tmp);
                }
                else if (tmp.Length == 11)
                {
                    output = string.Format("+{0}", tmp);
                }
                else if (tmp.Length == 12 && tmp.StartsWith('+'))
                {
                    output = tmp;
                }
            }
            return output;
        }

        public static void SetTwilioCredentials(IDBInterface database)
        {
            AppSetting sid = database.GetAppSetting(Settings.TWILIO_ACCOUNT_SID)!;
            AppSetting auth = database.GetAppSetting(Settings.TWILIO_AUTH_TOKEN)!;
            AppSetting phone = database.GetAppSetting(Settings.TWILIO_PHONE_NUMBER)!;
            if (sid != null)
            {
                TwilioCredentials.AccountSid = sid.Value;
            }
            if (auth != null)
            {
                TwilioCredentials.AuthToken = auth.Value;
            }
            if (phone != null)
            {
                TwilioCredentials.PhoneNumber = phone.Value;
            }
            if (TwilioCredentials.AccountSid.Length > 0 && TwilioCredentials.AuthToken.Length > 0)
            {
                TwilioClient.Init(TwilioCredentials.AccountSid, TwilioCredentials.AuthToken);
            }
        }

        public static void SetTwilioCredentials(string accountSID, string authToken, string phoneNumber)
        {
            TwilioCredentials.AccountSid = accountSID;
            TwilioCredentials.AuthToken = authToken;
            TwilioCredentials.PhoneNumber = GetValidPhone(phoneNumber);
            if (TwilioCredentials.AccountSid.Length > 0 && TwilioCredentials.AuthToken.Length > 0)
            {
                TwilioClient.Init(TwilioCredentials.AccountSid, TwilioCredentials.AuthToken);
            }
        }
    }
}
