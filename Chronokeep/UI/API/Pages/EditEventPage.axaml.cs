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
                DialogBox.AsyncShow(ex.Message);
                window.Close();
                return;
            }
            NameBox.Text = apiEvent.Event.Name;
            SlugBox.Text = apiEvent.Event.Slug;
            CertNameBox.Text = apiEvent.Event.CertificateName;
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
                CertificateName = CertNameBox.Text ?? "",
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
            DialogBox.AsyncShow(ex.Message);
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
