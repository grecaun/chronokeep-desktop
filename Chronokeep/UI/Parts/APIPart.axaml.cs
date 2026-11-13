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

using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Helpers;
using Chronokeep.Network.API;
using Chronokeep.Network.Remote;
using Chronokeep.Network.Runsignup;
using System;
using Chronokeep.Objects;
using Chronokeep.Objects.Runsignup;
using Chronokeep.UI.Util;
using System.Threading.Tasks;
using Chronokeep.UI.MainPages.Dashboard;

namespace Chronokeep.UI.Parts;

public partial class ApiPart : UserControl
{
    private readonly ApiPage page;
    public readonly ApiObject TheApi;
    private string shownType = "";

    public ApiPart(ApiPage page, ApiObject api)
    {
        InitializeComponent();
        TheApi = api;
        this.page = page;
        ApiNickname.Text = api.Nickname;
        ComboBoxItem? selected = null;
        foreach (string uid in Constants.ApiConstants.API_TYPE_NAMES.Keys)
        {
            ComboBoxItem newItem = new()
            {
                Content = Constants.ApiConstants.API_TYPE_NAMES[uid],
                Tag = uid,
                IsSelected = TheApi.Type.Equals(uid),
            };
            if (TheApi.Type.Equals(uid))
            {
                selected = newItem;
            }
            ApiType.Items.Add(newItem);
        }
        ApiUrl.Text = api.Url;
        ApiUrl.IsEnabled = Constants.ApiConstants.API_SELF_HOSTED[TheApi.Type] || api.IsRunsignup;
        ApiToken.Text = api.AuthToken;
        ApiWebUrl.Text = api.WebUrl;
        if (selected != null)
        {
            ApiType.SelectedItem = selected;
        }
        else
        {
            ApiType.SelectedIndex = 0;
        }
    }

    public void UpdateResultsApi()
    {
        Log.D("UI.MainPages.APIPage", "Updating api.");
        TheApi.Nickname = ApiNickname.Text!;
        TheApi.Type = (string)((ComboBoxItem)ApiType.SelectedItem!).Tag!;
        TheApi.Url = (ApiUrl.Text ?? "").Trim();
        // A Runsignup secret is stored in the URL column and must be kept exactly as typed.
        if (!TheApi.IsRunsignup && !TheApi.Url.EndsWith('/'))
        {
            TheApi.Url += "/";
        }
        TheApi.AuthToken = (ApiToken.Text ?? "").Trim();
        TheApi.WebUrl = ApiWebUrl.Text!;
        if (TheApi.WebUrl.Length > 0 && !TheApi.WebUrl.EndsWith('/'))
        {
            TheApi.WebUrl += "/";
        }
    }

    private void APIType_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Log.D("UI.MainPages.APIPage", "Changing API Type!");
        // Ensure we've got something selected, and then change the URL if they've selected Chronokeep.
        if (ApiType.SelectedItem == null) return;
        string type = (string)((ComboBoxItem)ApiType.SelectedItem).Tag!;
        bool runsignup = type == Constants.ApiConstants.RUNSIGNUP;
        bool wasRunsignup = shownType == Constants.ApiConstants.RUNSIGNUP;
        bool firstShow = shownType.Length == 0;
        shownType = type;
        ApiUrlLabel.Text = runsignup ? "API Secret" : "API URL";
        ApiTokenLabel.Text = "API Key";
        if (runsignup)
        {
            // The URL box holds the secret. Don't carry a URL over into it, or a secret into a URL.
            if (!firstShow && !wasRunsignup)
            {
                ApiUrl.Text = "";
            }
            ApiUrl.IsEnabled = true;
            ApiWebUrl.Text = "";
            ApiWebUrl.IsEnabled = false;
            return;
        }
        if (wasRunsignup)
        {
            ApiUrl.Text = "";
        }
        if (!Constants.ApiConstants.API_SELF_HOSTED[type])
        {
            TheApi.Url = Constants.ApiConstants.API_URL[type];
            ApiUrl.Text = TheApi.Url;
            ApiUrl.IsEnabled = false;
        }
        else
        {
            ApiUrl.IsEnabled = true;
        }
        if (Constants.ApiConstants.API_RESULTS[type])
        {
            ApiWebUrl.Text = TheApi.WebUrl;
            ApiWebUrl.IsEnabled = true;
        }
        else
        {
            ApiWebUrl.Text = "";
            ApiWebUrl.IsEnabled = false;
        }
    }

    private async void Test_Click(object? sender, RoutedEventArgs e)
    {
        await TestApi();
    }

    /// <summary>
    /// Checks the typed values (not the saved ones) work by asking the API for something that needs
    /// the key: Runsignup's account, the Chronokeep events, or the remote readers.
    /// </summary>
    internal async Task TestApi()
    {
        string type = (string)((ComboBoxItem)ApiType.SelectedItem!).Tag!;
        string url = (ApiUrl.Text ?? "").Trim();
        if (type != Constants.ApiConstants.RUNSIGNUP && !url.EndsWith('/'))
        {
            url += "/";
        }
        ApiObject typed = new(TheApi.Identifier, type, url, "", (ApiToken.Text ?? "").Trim(), "");
        TestButton.IsEnabled = false;
        try
        {
            string result;
            if (type == Constants.ApiConstants.RUNSIGNUP)
            {
                RsuEntityInfo info = await RunsignupHandlers.GetEntityInfo(typed);
                result = $"The Runsignup key works.\n\n{info.Describe()}";
            }
            else if (Constants.ApiConstants.API_REMOTE[type])
            {
                int readers = (await RemoteHandlers.GetReaders(typed)).Readers.Count;
                result = $"The API works.\n\n{readers} remote reader{(readers == 1 ? "" : "s")} found.";
            }
            else
            {
                int events = (await ApiHandlers.GetEvents(typed)).Events.Count;
                result = $"The API works.\n\n{events} event{(events == 1 ? "" : "s")} found.";
            }
            DialogBox.Show(result);
        }
        catch (Exception ex)
        {
            DialogBox.Show(ex.Message);
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }

    private void Remove_Click(object? sender, RoutedEventArgs e)
    {
        Log.D("UI.MainPages.APIPage", "Removing api.");
        page.RemoveApi(TheApi);
    }
}
