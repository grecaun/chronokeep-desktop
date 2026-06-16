using Avalonia.Controls;
using Chronokeep.UI.API.Windows;

namespace Chronokeep.UI.API.Pages;

public partial class ApiErrorPage : UserControl
{
    private readonly ApiWindow window;

    public ApiErrorPage(ApiWindow window, bool noApi)
    {
        InitializeComponent();
        this.window = window;
        if (noApi)
        {
            ErrorLabel.Text = "An API must be set up before you can use this tool.";
        }
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        window.Close();
    }
}