using System;
using System.Globalization;
using Avalonia.Controls;
using Chronokeep.Helpers;
using Chronokeep.Network.API;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronoKeepAPI;
using Chronokeep.UI.API.Windows;
using Chronokeep.UI.Util;

namespace Chronokeep.UI.API.Pages;

public partial class EditYearPage : UserControl
{
    private readonly EditApiWindow window;

    private readonly ApiObject api;
    private readonly string slug;
    private readonly string year;

    private EventYearResponse? response;

    public EditYearPage(EditApiWindow window, ApiObject api, string slug, string year)
    {
        InitializeComponent();
        this.window = window;

        this.api = api;
        this.slug = slug;
        this.year = year;

        GetEventYears();
    }

    private async void GetEventYears()
    {
        try
        {
            try
            {
                response = await ApiHandlers.GetEventYear(api, slug, year);
            }
            catch (ApiException ex)
            {
                DialogBox.Show(ex.Message);
                window.Close();
                return;
            }
            YearBox.Text = response.EventYear.Year;
            DateBox.SelectedDate = DateTime.Parse(response.EventYear.DateTime);
            if (response.Event.Type == Constants.ApiConstants.CHRONOKEEP_EVENT_TYPE_BACKYARD_ULTRA)
            {
                RankBox.Items.Add(new ComboBoxItem
                {
                    Content = "Elapsed",
                    Tag = "Clock"
                });
                RankBox.Items.Add(new ComboBoxItem
                {
                    Content = "Cumulative",
                    Tag = "Chip"
                });
            }
            else
            {
                RankBox.Items.Add(new ComboBoxItem
                {
                    Content = "Clock",
                    Tag = "Clock"
                });
                RankBox.Items.Add(new ComboBoxItem
                {
                    Content = "Chip",
                    Tag = "Chip"
                });
            }
            RankBox.SelectedIndex = response.EventYear.RankingType.Equals("chip", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            LiveBox.IsChecked = response.EventYear.Live;
            DaysAllowedText.Text = response.EventYear.DaysAllowed.ToString();
            DaysAllowedSlider.Value = response.EventYear.DaysAllowed;
            YearPanel.IsVisible = true;
            HoldingLabel.IsVisible = false;
            SaveButton.IsEnabled = true;
        }
        catch (Exception)
        {
            Log.D("UI.API.Pages.EventYearPage","Error getting years.");
        }
    }

    private void DaysAllowed_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (DaysAllowedSlider != null && DaysAllowedText != null)
        {
            DaysAllowedText.Text = DaysAllowedSlider.Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private async void Done_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            await ApiHandlers.UpdateEventYear(api, slug, new ApiEventYear
            {
                Year = YearBox.Text!,
                DateTime = DateBox.SelectedDate?.ToString("yyyy/MM/dd HH:mm:ss zzz") ?? DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss zzz"),
                Live = LiveBox.IsChecked == true,
                DaysAllowed = Convert.ToInt32(DaysAllowedSlider.Value),
                RankingType = ((string)((ComboBoxItem)RankBox.SelectedItem!).Tag!).Equals("Chip", StringComparison.OrdinalIgnoreCase) ? "chip" : "gun",
            });
            window.Close();
        }
        catch (ApiException ex)
        {
            DialogBox.Show(ex.Message);
        }
        catch (Exception)
        {
            Log.D("UI.API.Pages.EventYearPage","Error updating.");
        }
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        window.Close();
    }
}