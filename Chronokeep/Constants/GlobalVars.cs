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

using System;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Network.API;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronoKeepAPI;
using Chronokeep.Objects.Notifications;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Twilio;

namespace Chronokeep.Constants
{
    public static partial class GlobalVars
    {
        // keep track of TWILIO credentials
        internal static readonly TwilioCredentials TwilioCredentials = new();
        // keep track of banned phones
        internal static readonly HashSet<string> BannedPhones = [];
        // keep track of local list of banned phone numbers that we need to add to the api's list
        private static readonly HashSet<string> NewBannedPhones = [];
        // keep track of banned emails
        internal static readonly HashSet<string> BannedEmails = [];

        [GeneratedRegex("^(?:\\+?1)?\\s*\\-?\\s*(?:\\d{3}|\\(\\d{3}\\))\\s*\\-?\\s*\\d{3}\\s*\\-?\\s*\\d{4}$")]
        private static partial Regex Phone();
        [GeneratedRegex("\\s+")]
        private static partial Regex WhiteSpace();

        public static async void UpdateBannedPhones()
        {
            try
            {
                try
                {
                    GetBannedPhonesResponse phonesResponse = await ApiHandlers.GetBannedPhones();
                    BannedPhones.Clear();
                    foreach (string p in phonesResponse.Phones.Select(GetValidPhone).Where(p => p.Length > 0))
                    {
                        BannedPhones.Add(p);
                    }
                    // make sure we've got all our new banned phone numbers in there too
                    foreach (string p in NewBannedPhones.Select(GetValidPhone).Where(p => p.Length > 0))
                    {
                        BannedPhones.Add(p);
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
                    if (p.Length <= 0) continue;
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
            catch (Exception)
            {
                Log.E("Constants.Globals", "Error updating banned phones.");
            }
        }

        public static async void AddBannedPhone(string phone)
        {
            try
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
            catch (Exception)
            {
                Log.E("Constants.Globals", "Error adding banned phone.");
            }
        }

        public static async void UpdateBannedEmails()
        {
            try
            {
                GetBannedEmailsResponse emailsResponse = await ApiHandlers.GetBannedEmails();
                BannedEmails.Clear();
                foreach (string email in emailsResponse.Emails)
                {
                    BannedEmails.Add(email);
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
            if (!Phone().IsMatch(phone)) return output;
            string tmp = WhiteSpace().Replace(phone.Replace("-", "").Replace(")", "").Replace("(", ""), "");
            output = tmp.Length switch
            {
                10 => $"+1{tmp}",
                11 => $"+{tmp}",
                12 when tmp.StartsWith('+') => tmp,
                _ => output
            };
            return output;
        }

        public static void SetTwilioCredentials(IdbInterface database)
        {
            AppSetting sid = database.GetAppSetting(Settings.TWILIO_ACCOUNT_SID)!;
            AppSetting auth = database.GetAppSetting(Settings.TWILIO_AUTH_TOKEN)!;
            AppSetting phone = database.GetAppSetting(Settings.TWILIO_PHONE_NUMBER)!;
            TwilioCredentials.AccountSid = sid.Value;
            TwilioCredentials.AuthToken = auth.Value;
            TwilioCredentials.PhoneNumber = phone.Value;
            if (TwilioCredentials.AccountSid.Length > 0 && TwilioCredentials.AuthToken.Length > 0)
            {
                TwilioClient.Init(TwilioCredentials.AccountSid, TwilioCredentials.AuthToken);
            }
        }

        public static void SetTwilioCredentials(string accountSid, string authToken, string phoneNumber)
        {
            TwilioCredentials.AccountSid = accountSid;
            TwilioCredentials.AuthToken = authToken;
            TwilioCredentials.PhoneNumber = GetValidPhone(phoneNumber);
            if (TwilioCredentials.AccountSid.Length > 0 && TwilioCredentials.AuthToken.Length > 0)
            {
                TwilioClient.Init(TwilioCredentials.AccountSid, TwilioCredentials.AuthToken);
            }
        }
    }
}

