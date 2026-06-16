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

    public ApiPage1(ApiWindow window, IDBInterface database)
    {
        InitializeComponent();
        this.window = window;

        AppSetting lastApi = database.GetAppSetting(Constants.Settings.LAST_USED_API_ID)!;
        List<ApiObject> apis = database.GetAllAPI();
        apis.RemoveAll(x => !Constants.APIConstants.API_RESULTS[x.Type]);
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