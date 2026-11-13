using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(Chronokeep.Test.UI.Infrastructure.UiTestApp))]

namespace Chronokeep.Test.UI.Infrastructure
{
    /// <summary>
    /// Headless stand-in for <see cref="App"/>. It loads the same themes and resource dictionaries
    /// the real app uses, but skips App.Initialize so tests never start Sentry or touch the shared
    /// program folder, and never open MainWindow.
    /// </summary>
    public class UiTestApp : Application
    {
        private static readonly Uri BaseUri = new("avares://Chronokeep.test/");

        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<UiTestApp>()
            // Real (Skia) text layout: the headless stand-in hangs laying out a message with a blank line in it.
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .ConfigureFonts(fontManager => fontManager.AddFontCollection(new ChronokeepFontCollection()));

        public override void Initialize()
        {
            RequestedThemeVariant = ThemeVariant.Light;
            Styles.Add(new FluentTheme());
            Styles.Add(new StyleInclude(BaseUri)
            {
                Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml")
            });
            Resources.MergedDictionaries.Add(new ResourceInclude(BaseUri)
            {
                Source = new Uri("avares://Chronokeep/Icons.axaml")
            });
            Resources.MergedDictionaries.Add(new ResourceInclude(BaseUri)
            {
                Source = new Uri("avares://Chronokeep/Colors.axaml")
            });
        }
    }
}
