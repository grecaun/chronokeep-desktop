using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Chronokeep.Database;
using Chronokeep.Helpers;
using Chronokeep.Network.API;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronoKeepAPI;
using Chronokeep.UI.API.Windows;
using Chronokeep.UI.Util;

namespace Chronokeep.UI.API.Pages;

public partial class ApiPage2 : UserControl
{
    private readonly ApiWindow window;
    private readonly IdbInterface database;
    private readonly ApiObject api;
    private readonly Event theEvent;

    private GetEventsResponse? events;

    public ApiPage2(ApiWindow window, IdbInterface database, ApiObject api, Event theEvent)
    {
        InitializeComponent();
        this.window = window;
        this.database = database;
        this.api = api;
        this.theEvent = theEvent;
        GetEvents();
    }

    private async void GetEvents()
    {
        try
        {
            try
            {
                events = await ApiHandlers.GetEvents(api);
            }
            catch (ApiException ex)
            {
                DialogBox.AsyncShow(ex.Message);
                window.Close();
                return;
            }
            Log.D("UI.API.APIPage2", "Adding events to combo box.");
            events.Events.Sort((a, b) => b.CompareTo(a));
            events.Events.Insert(0, new ApiEvent
            {
                Name = "New Event"
            });
            List<ApiEvent> ev = [.. events.Events];
            EventList.ItemsSource = ev;
            ApiEvent maybeEvent = ev.Find(x => x.Name.Equals(theEvent.Name, StringComparison.OrdinalIgnoreCase))!;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                EventList.SelectedItem = maybeEvent;
                EventList.ScrollIntoView(maybeEvent);
            });
            if (EventList.SelectedItem == null)
            {
                EventList.SelectedIndex = 0;
            }
            NameBox.Text = theEvent.Name;
            SlugBox.Text = theEvent.Name.Replace(' ', '-').Replace("'", "").Replace("/", "").Replace("\\", "").ToLower();
            ContactBox.Text = database.GetAppSetting(Constants.Settings.CONTACT_EMAIL)!.Value;
            EventPanel.IsVisible = true;
            HoldingLabel.IsVisible = false;
        }
        catch (Exception)
        {
            Log.D("UI.API.Pages.ApiPage2", "Error getting events.");
        }
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        List<ApiEvent> ev = [.. events!.Events];
        if (SearchBox.Text!.Trim().Length > 0)
        {
            Log.D("UI.API.APIPage2", $"searchBox.Text {SearchBox.Text}");
            ev.RemoveAll(x =>
                !x.Name.Contains(SearchBox.Text, StringComparison.OrdinalIgnoreCase)
                && !x.Name.Contains("New Event", StringComparison.OrdinalIgnoreCase)
            );
        }
        EventList.ItemsSource = ev;
        ApiEvent maybeEvent = ev.Find(x => x.Name.Equals(theEvent.Name, StringComparison.OrdinalIgnoreCase))!;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            EventList.SelectedItem = maybeEvent;
            EventList.ScrollIntoView(maybeEvent);
        });
    }

    private void EventBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        NewPanel.IsVisible = EventList.SelectedIndex < 1;
    }

    private void EventList_MouseDoubleClick(object? sender, TappedEventArgs e)
    {
        Log.D("UI.ChangeEventWindow", "Double Click detected.");
        Next_Click(sender, null);
    }

    private async void Next_Click(object? sender, RoutedEventArgs? e)
    {
        try
        {
            if (EventList == null)
            {
                window.Close();
                return;
            }
            string slug;
            if (EventList.SelectedItem == null)
            {
                DialogBox.AsyncShow("Please select an event.");
                return;
            }
            if (((ApiEvent?)EventList.SelectedItem)?.Slug == null || ((ApiEvent)EventList.SelectedItem).Slug.Length < 1)
            {
                try
                {
                    string type = theEvent.EventType switch
                    {
                        Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA => Constants.ApiConstants
                            .CHRONOKEEP_EVENT_TYPE_BACKYARD_ULTRA,
                        Constants.Timing.EVENT_TYPE_TIME => Constants.ApiConstants.CHRONOKEEP_EVENT_TYPE_TIME,
                        Constants.Timing.EVENT_TYPE_DISTANCE => Constants.ApiConstants.CHRONOKEEP_EVENT_TYPE_DISTANCE,
                        _ => Constants.ApiConstants.CHRONOKEEP_EVENT_TYPE_UNKNOWN
                    };
                    ModifyEventResponse addResponse = await ApiHandlers.AddEvent(api, new ApiEvent
                    {
                        Name = NameBox.Text!,
                        CertificateName = CertNameBox.Text!,
                        Slug = SlugBox.Text!,
                        Website = WebsiteBox.Text!,
                        Image = ImageBox.Text!,
                        ContactEmail = ContactBox.Text!,
                        AccessRestricted = (bool)RestrictBox.IsChecked!,
                        Type = type
                    });
                    slug = addResponse.Event.Slug;
                }
                catch (ApiException ex)
                {
                    DialogBox.AsyncShow(ex.Message);
                    return;
                }
            }
            else
            {
                slug = ((ApiEvent)EventList.SelectedItem).Slug;
            }
            window.GotoPage3(slug);
        }
        catch (Exception)
        {
            Log.D("UI.API.Pages.ApiPage2", "Error proceeding.");
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        window.Close();
    }
}