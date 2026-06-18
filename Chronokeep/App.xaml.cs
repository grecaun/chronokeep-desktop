using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Fonts;
using Chronokeep.Helpers;
using Chronokeep.UI;
using Sentry;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Chronokeep
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public class App : Application
    {
        internal static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        
        public override void Initialize()
        {
            SentrySdk.Init(options =>
            {
                options.Dsn = "https://69f609b68d42089ccea1545e8942c9e6@o4510042186514432.ingest.us.sentry.io/4510042244317184";
                options.IsGlobalModeEnabled = true;
                options.StackTraceMode = StackTraceMode.Enhanced;
                options.SendDefaultPii = false;
#if DEBUG
                options.Environment = "debug";
#else
                options.Environment = "release";
#endif
                string gitVersion;
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Chronokeep.version.txt")!)
                {
                    using StreamReader reader = new(stream);
                    gitVersion = reader.ReadToEnd();
                }
                options.Release = $"chronokeep-windows@{gitVersion}";
            });
            Log.D("UI.MainWindow", "Looking for log directory.");
            string logDirPath = IsWindows ?
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), Constants.Settings.PROGRAM_DIR, "logs")
                : Path.Combine(Directory.GetCurrentDirectory(), "logs");
            if (!Directory.Exists(logDirPath))
            {
                Log.D("UI.MainWindow", "Creating log directory.");
                Directory.CreateDirectory(logDirPath);
            }
            Globals.ErrorLogPath = Path.Combine(logDirPath, $"{DateTime.Now:yyyyMMdd}_error_log.txt");
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                bool safeMode = false;
                foreach (string arg in desktop.Args!)
                {
                    Log.D("AppStartup", $"Startup arg: {arg}");
                    if (arg.Contains("safe", StringComparison.OrdinalIgnoreCase))
                    {
                        safeMode = true;
                    }
                }
                if (safeMode)
                {
                    desktop.MainWindow = new MinWindow();
                }
                else
                {
                    desktop.MainWindow = new MainWindow();
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        [STAThread]
        static void Main(string[] args)
            => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .ConfigureFonts(fontManager =>
                {
                    fontManager.AddFontCollection(new ChronokeepFontCollection());
                })
                .LogToTrace();

        private static void CaptureException(Exception ex)
        {
            string logDirPath = IsWindows ?
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), Constants.Settings.PROGRAM_DIR, "logs")
                : Path.Combine(Directory.GetCurrentDirectory(), "logs");
            if (!Directory.Exists(logDirPath))
            {
                Log.D("UI.MainWindow", "Creating log directory.");
                Directory.CreateDirectory(logDirPath);
            }
            string date = DateTime.Now.ToString("yyyyMMdd");
            string logPath = Path.Combine(logDirPath, $"{date}_crash_0.txt");
            int ix = 0;
            while (File.Exists(logPath))
            {
                ix++;
                logPath = Path.Combine(logDirPath, $"{date}_crash_{ix}.txt");
            }
            File.WriteAllText(logPath, ex.StackTrace);
            SentrySdk.CaptureException(ex);
        }
    }

    public sealed class ChronokeepFontCollection() : EmbeddedFontCollection(
        new Uri("fonts:ChronokeepFonts", UriKind.Absolute),
        new Uri("avares://Chronokeep/fonts", UriKind.Absolute));
}
