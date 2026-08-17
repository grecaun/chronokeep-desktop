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
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Database;
using Chronokeep.Objects;
using Chronokeep.UI.API.Windows;

namespace Chronokeep.UI.API.Pages;

public partial class ApiPage1 : UserControl
{
    private readonly ApiWindow window;
    private readonly Dictionary<string, ApiObject> apiDict;

    public ApiPage1(ApiWindow window, IdbInterface database)
    {
        InitializeComponent();
        this.window = window;

        AppSetting lastApi = database.GetAppSetting(Constants.Settings.LAST_USED_API_ID)!;
        List<ApiObject> apis = database.GetAllApi();
        apis.RemoveAll(x => !Constants.ApiConstants.API_RESULTS[x.Type]);
        apiDict = [];
        int apiId;
        try
        {
            apiId = Convert.ToInt32(lastApi.Value);
        }
        catch
        {
            apiId = -1;
        }
        int ix = 0;
        int count = 0;
        foreach (ApiObject api in apis)
        {
            apiDict[api.Identifier.ToString()] = api;
            ApiBox.Items.Add(new ComboBoxItem
            {
                Content = api.Nickname,
                Tag = api.Identifier.ToString()
            });
            if (apiId > 0 && apiId == api.Identifier)
            {
                ix = count;
            }
            count++;
        }
        ApiBox.SelectedIndex = ix;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        window.Close();
    }

    private void Next_Click(object? sender, RoutedEventArgs e)
    {
        window.GotoPage2(apiDict[(string)((ComboBoxItem)ApiBox.SelectedItem!).Tag!]);
    }
}
