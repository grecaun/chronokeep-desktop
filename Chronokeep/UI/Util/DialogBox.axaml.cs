using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chronokeep.UI.Util;

public partial class DialogBox : Window
{
    public delegate void LeftClickDelegate();

    private string CopyText = "";

    public DialogBox(string Message, string LeftButtonContent, string RightButtonContent, bool ShowLeftButton, LeftClickDelegate LeftClick)
    {
        InitializeComponent();
        MessageBox.Text = Message;
        LeftButton.Content = LeftButtonContent;
        RightButton.Content = RightButtonContent;
        if (ShowLeftButton)
        {
            LeftButton.IsVisible = true;
        }
        else
        {
            LeftButton.IsVisible = false;
        }
        LeftButton.Click += (sender, e) =>
        {
            Close();
            LeftClick();
        };
        RightButton.Click += (sender, e) =>
        {
            Close();
        };
        this.MinWidth = 400.0;
        this.Width = 400.0;
        this.MinHeight = 200.0;
        this.Topmost = true;
    }

    public async static void Show(string Message)
    {
        DialogBox output = new(
            Message,
            "",
            "OK",
            false,
            () => { }
            );
        await output.ShowDialog(MainWindow.mWindow!);
    }

    public async static void Show(string Message, string LeftButtonContent, string RightButtonContent, LeftClickDelegate LeftClick)
    {
        DialogBox output = new(
            Message,
            LeftButtonContent,
            RightButtonContent,
            true,
            LeftClick
            );
        await output.ShowDialog(MainWindow.mWindow!);
    }

    public async static void Show(string Message, string CopyText)
    {
        DialogBox output = new(
            Message,
            "",
            "OK",
            false,
            () => { }
            )
        {
            CopyText = CopyText
        };
        output.CopyBox.Text = CopyText;
        output.CopyBox.IsVisible = true;
        output.Width = 500.0;
        await output.ShowDialog(MainWindow.mWindow!);
    }

    private void CopyBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CopyText = CopyBox.Text!;
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}