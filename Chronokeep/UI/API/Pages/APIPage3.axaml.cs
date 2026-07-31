using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Chronokeep.Helpers;
using Chronokeep.Network.API;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronoKeepAPI;
using Chronokeep.UI.API.Windows;
using Chronokeep.UI.Util;

namespace Chronokeep.UI.API.Pages;

public partial class ApiPage3 : UserControl
{
    private readonly ApiWindow window;
    private readonly ApiObject api;
    private readonly Event theEvent;
    private readonly string slug;

    private GetEventYearsResponse? years;

    public ApiPage3(ApiWindow window, ApiObject api, Event theEvent, string slug)
    {
        InitializeComponent();
        this.window = window;
        this.api = api;
        this.theEvent = theEvent;
        this.slug = slug;
        GetEventYears();
    }

    private async void GetEventYears()
    {
        try
        {
            try
            {
                years = await ApiHandlers.GetEventYears(api, slug);
            }
            catch (ApiException ex)
            {
                DialogBox.AsyncShow(ex.Message);
                window.Close();
                return;
            }
            YearCopyBox.Items.Add(new ComboBoxItem
            {
                Content = "New Year",
                Tag = "NEW"
            });
            int ix = 0;
            int count = 1;
            foreach (ApiEventYear y in years.EventYears)
            {
                YearCopyBox.Items.Add(new ComboBoxItem
                {
                    Content = y.Year,
                    Tag = y.Year
                });
                if (theEvent.YearCode == y.Year)
                {
                    ix = count;
                }
                count++;
            }
            YearCopyBox.SelectedIndex = ix;
            NewPanel.IsVisible = ix == 0;
            YearBox.Text = theEvent.YearCode;
            DateBox.SelectedDate = DateTime.Parse(theEvent.Date);
            if (theEvent.EventType == Constants.Timing.EVENT_TYPE_BACKYARD_ULTRA)
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
            RankBox.SelectedIndex = theEvent.RankByGun ? 0 : 1;
            YearPanel.IsVisible = true;
            HoldingLabel.IsVisible = false;
        }
        catch (Exception)
        {
            Log.D("UI.API.Pages.ApiPage3", "Error getting event years.");
        }
    }

    private void YearBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        NewPanel.IsVisible = (string)((ComboBoxItem)YearCopyBox.SelectedItem!).Tag! == "NEW";
    }

    private void DaysAllowed_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (DaysAllowedSlider != null && DaysAllowedText != null)
        {
            DaysAllowedText.Text = DaysAllowedSlider.Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private async void Next_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Log.D("UI.API.APIPage3", $"DateTime: {DateBox.SelectedDate}");
            string year = (string)((ComboBoxItem)YearCopyBox.SelectedItem!).Tag!;
            if (year == "NEW")
            {
                try
                {
                    EventYearResponse addResponse = await ApiHandlers.AddEventYear(api, slug, new ApiEventYear
                    {
                        Year = YearBox.Text!,
                        DateTime = DateBox.SelectedDate?.ToString("yyyy/MM/dd HH:mm:ss zzz") ?? DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss zzz"),
                        Live = LiveBox.IsChecked == true,
                        DaysAllowed = Convert.ToInt32(DaysAllowedSlider.Value),
                        RankingType = ((string)((ComboBoxItem)RankBox.SelectedItem!).Tag!).Equals("Chip", StringComparison.OrdinalIgnoreCase) ? "chip" : "gun",
                    });
                    year = addResponse.EventYear.Year;
                }
                catch (ApiException ex)
                {
                    DialogBox.AsyncShow(ex.Message);
                    return;
                }
            }
            window.Finish(year);
        }
        catch (Exception)
        {
            Log.D("UI.API.Pages.ApiPage3", "Error finishing.");
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        window.Close();
    }
}