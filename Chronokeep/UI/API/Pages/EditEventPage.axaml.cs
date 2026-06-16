using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Constants;
using Chronokeep.Helpers;
using Chronokeep.Network.API;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronoKeepAPI;
using Chronokeep.UI.API.Windows;
using Chronokeep.UI.Util;

namespace Chronokeep.UI.API.Pages;

public partial class EditEventPage : UserControl
{
    private readonly EditApiWindow window;

    private readonly ApiObject api;
    private readonly string slug;

    private GetEventResponse? apiEvent;

    public EditEventPage(EditApiWindow window, ApiObject api, string slug)
    {
        InitializeComponent();
        this.window = window;
        this.api = api;
        this.slug = slug;
        GetEvent();
    }

    private async void GetEvent()
    {
        try
        {
            try
            {
                apiEvent = await ApiHandlers.GetEvent(api, slug);
            }
            catch (ApiException ex)
            {
                DialogBox.Show(ex.Message);
                window.Close();
                return;
            }
            NameBox.Text = apiEvent.Event.Name;
            SlugBox.Text = apiEvent.Event.Slug;
            ContactBox.Text = apiEvent.Event.ContactEmail;
            WebsiteBox.Text = apiEvent.Event.Website;
            ImageBox.Text = apiEvent.Event.Image;
            RestrictBox.IsChecked = apiEvent.Event.AccessRestricted;
            ComboBoxItem? type = null;
            foreach (object? item in TypeBox.Items)
            {
                if (item is not ComboBoxItem cbi) continue;
                if (cbi.Content!.ToString()!.Equals("Distance", StringComparison.OrdinalIgnoreCase)
                    && apiEvent.Event.Type.Equals(ApiConstants.CHRONOKEEP_EVENT_TYPE_DISTANCE, StringComparison.OrdinalIgnoreCase) || cbi.Content.ToString()!.Equals("Time", StringComparison.OrdinalIgnoreCase)
                    && apiEvent.Event.Type.Equals(ApiConstants.CHRONOKEEP_EVENT_TYPE_TIME, StringComparison.OrdinalIgnoreCase) || cbi.Content.ToString()!.Equals("Backyard Ultra", StringComparison.OrdinalIgnoreCase)
                    && apiEvent.Event.Type.Equals(ApiConstants.CHRONOKEEP_EVENT_TYPE_BACKYARD_ULTRA, StringComparison.OrdinalIgnoreCase))
                {
                    type = cbi;
                }
            }
            if (type != null)
            {
                TypeBox.SelectedItem = type;
            }
            else
            {
                TypeBox.SelectedIndex = 0;
            }
            EventPanel.IsVisible = true;
            HoldingLabel.IsVisible = false;
            SaveButton.IsEnabled = true;
        }
        catch (Exception)
        {
            Log.D("UI.API.Pages.EditEventPage", "Error getting event.");
        }
    }

    private async void Done_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            string type = ApiConstants.CHRONOKEEP_EVENT_TYPE_UNKNOWN;
            if (((ComboBoxItem)TypeBox.SelectedItem!).Content!.ToString()!.Equals("Distance", StringComparison.OrdinalIgnoreCase))
            {
                type = ApiConstants.CHRONOKEEP_EVENT_TYPE_DISTANCE;
            }
            else if (((ComboBoxItem)TypeBox.SelectedItem).Content!.ToString()!.Equals("Time", StringComparison.OrdinalIgnoreCase))
            {
                type = ApiConstants.CHRONOKEEP_EVENT_TYPE_TIME;
            }
            else if (((ComboBoxItem)TypeBox.SelectedItem).Content!.ToString()!.Equals("Backyard Ultra", StringComparison.OrdinalIgnoreCase))
            {
                type = ApiConstants.CHRONOKEEP_EVENT_TYPE_BACKYARD_ULTRA;
            }
            await ApiHandlers.UpdateEvent(api, new ApiEvent
            {
                Name = NameBox.Text!,
                CertificateName = CertNameBox.Text!,
                Slug = SlugBox.Text!,
                Website = WebsiteBox.Text!,
                Image = ImageBox.Text!,
                ContactEmail = ContactBox.Text!,
                AccessRestricted = RestrictBox.IsChecked == true,
                Type = type
            });
            window.Close();
        }
        catch (ApiException ex)
        {
            DialogBox.Show(ex.Message);
        }
        catch (Exception)
        {
            Log.D("UI.API.Pages.EditEventPage", "Error finishing.");
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        window.Close();
    }
}