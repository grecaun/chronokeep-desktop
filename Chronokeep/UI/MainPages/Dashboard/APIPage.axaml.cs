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
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Objects;
using Chronokeep.UI.Parts;
using System.Collections.Generic;

namespace Chronokeep.UI.MainPages.Dashboard;

public partial class ApiPage : UserControl, IMainPage
{
    private readonly IMainWindow mWindow;
    private readonly IdbInterface database;
    private List<ApiObject>? resultsApi;

    public ApiPage(IMainWindow mWindow, IdbInterface database)
    {
        InitializeComponent();
        this.mWindow = mWindow;
        this.database = database;
        UpdateView();
    }

    public void KeyboardCtrlA()
    {
        Log.D("UI.MainPages.APIPage", "Ctrl + A Passed to this page.");
        Add_Click(null, null);
    }

    public void KeyboardCtrlS()
    {
        Log.D("UI.MainPages.APIPage", "Ctrl + S Passed to this page.");
        UpdateResultsApi();
        UpdateView();
    }

    public void KeyboardCtrlZ()
    {
        UpdateView();
    }

    public void UpdateView()
    {
        ApiBox.Items.Clear();
        resultsApi = database.GetAllApi();
        foreach (ApiObject api in resultsApi)
        {
            ApiBox.Items.Add(new ApiPart(this, api));
        }
    }

    public void Closing()
    {
        if (database.GetAppSetting(Constants.Settings.UPDATE_ON_PAGE_CHANGE)!.Value == Constants.Settings.SETTING_TRUE)
        {
            UpdateResultsApi();
        }
    }

    private void UpdateResultsApi()
    {
        foreach (object? listDiv in ApiBox.Items)
        {
            if (listDiv is not ApiPart part) continue;
            part.UpdateResultsApi();
            database.UpdateApi(part.TheApi);
        }
    }

    public void RemoveApi(ApiObject api)
    {
        if (database.GetAppSetting(Constants.Settings.UPDATE_ON_PAGE_CHANGE)!.Value == Constants.Settings.SETTING_TRUE)
        {
            UpdateResultsApi();
        }
        database.RemoveApi(api.Identifier);
        UpdateView();
    }

    private void Add_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs? e)
    {
        Log.D("UI.MainPages.APIPage", "Add api clicked.");
        if (database.GetAppSetting(Constants.Settings.UPDATE_ON_PAGE_CHANGE)!.Value == Constants.Settings.SETTING_TRUE)
        {
            UpdateResultsApi();
        }
        database.AddApi(new ApiObject());
        UpdateView();
    }

    private void Update_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.MainPages.APIPage", "Update clicked.");
        UpdateResultsApi();
        UpdateView();
    }

    private void Revert_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.D("UI.MainPages.APIPage", "Revert clicked.");
        UpdateView();
    }

    private void DoneBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        mWindow.SwitchPage(new DashboardPage(mWindow, database));
    }
}
