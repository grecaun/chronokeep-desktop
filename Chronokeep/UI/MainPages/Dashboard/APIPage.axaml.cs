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
    private readonly IDBInterface database;
    private List<ApiObject>? resultsApi;

    public ApiPage(IMainWindow mWindow, IDBInterface database)
    {
        InitializeComponent();
        this.mWindow = mWindow;
        this.database = database;
        UpdateView();
    }

    public void Keyboard_Ctrl_A()
    {
        Log.D("UI.MainPages.APIPage", "Ctrl + A Passed to this page.");
        Add_Click(null, null);
    }

    public void Keyboard_Ctrl_S()
    {
        Log.D("UI.MainPages.APIPage", "Ctrl + S Passed to this page.");
        UpdateResultsApi();
        UpdateView();
    }

    public void Keyboard_Ctrl_Z()
    {
        UpdateView();
    }

    public void UpdateView()
    {
        ApiBox.Items.Clear();
        resultsApi = database.GetAllAPI();
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
            database.UpdateAPI(part.TheApi);
        }
    }

    public void RemoveApi(ApiObject api)
    {
        if (database.GetAppSetting(Constants.Settings.UPDATE_ON_PAGE_CHANGE)!.Value == Constants.Settings.SETTING_TRUE)
        {
            UpdateResultsApi();
        }
        database.RemoveAPI(api.Identifier);
        UpdateView();
    }

    private void Add_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs? e)
    {
        Log.D("UI.MainPages.APIPage", "Add api clicked.");
        if (database.GetAppSetting(Constants.Settings.UPDATE_ON_PAGE_CHANGE)!.Value == Constants.Settings.SETTING_TRUE)
        {
            UpdateResultsApi();
        }
        database.AddAPI(new ApiObject());
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