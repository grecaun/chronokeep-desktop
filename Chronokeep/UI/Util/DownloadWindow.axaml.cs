using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Updates;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Chronokeep.UI.Util
{
    /// <summary>
    /// Interaction logic for DownloadWindow.xaml
    /// </summary>
    public partial class DownloadWindow : Window
    {
        private readonly string uri;
        private readonly string download_uri;
        private readonly string version;

        private CancellationTokenSource? cancellationToken = null;

        private readonly IMainWindow mWindow;

        public DownloadWindow(GithubRelease r, Updates.Version v, IMainWindow mWindow)
        {
            InitializeComponent();
            Topmost = true;
            this.mWindow = mWindow;
            this.MinHeight = 0;
            this.Height = 250;
            this.MinWidth = 0;
            this.Width = 400;
            DownloadProgress.IsVisible = false;
            this.version = v.ToString();
            if (App.IsWindows)
            {
                download_uri = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\chronokeep-setup-{version}.exe";
            }
            else
            {
                download_uri = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\chronokeep-{version}.tar.gz";
                Close();
                DialogBox.Show("Linux downloads not yet implemented.");
                return;
            }
            Log.D("Updates.Check", string.Format("Download URL - {0}", r.Assets[0].BrowserDownloadURL));
            uri = r.Assets[0].BrowserDownloadURL;
            Activate();
        }

        private static HttpClient GetHttpClient()
        {
            HttpClient client = new()
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.TryParseAdd("Chronokeep Desktop Application");
            return client;
        }
        private static async Task DownloadFileAsync(HttpClient client, Stream destination, string uri, IProgress<double> progress, CancellationToken token)
        {
            HttpResponseMessage response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"The request returned with HTTP status code{response.StatusCode}");
            }

            long total = response.Content.Headers.ContentLength ?? -1L;
            bool canReportProgress = total != -1 && progress != null;

            using var stream = await response.Content.ReadAsStreamAsync(token);
            long totalRead = 0L;
            byte[] buffer = new byte[8192];
            bool isMoreToRead = true;
            double lastReport = 0L;
            do
            {
                token.ThrowIfCancellationRequested();
                var read = await stream.ReadAsync(buffer, token);
                if (read == 0)
                {
                    isMoreToRead = false;
                }
                else
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read), token);
                    totalRead += read;
                    double report = Math.Truncate((totalRead * 1d) / (total * 1d) * 1000) / 10;
                    if (canReportProgress && ((report > lastReport + 0.5) || report == 100))
                    {
                        progress!.Report(report);
                        lastReport = report;
                    }
                }
            } while (isMoreToRead);

        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (((string)InstallButton.Content!).Equals("Download", StringComparison.OrdinalIgnoreCase))
            {
                Log.D("Updates.DownloadWindow", $"Download clicked. Downloading to {download_uri}");
                DownloadProgress.IsVisible = true;
                DownloadLabel.Text = $"Downloading {version}";
                InstallButton.Content = "Install";
                InstallButton.IsEnabled = false;
                BackupDatabaseButton.IsEnabled = false;
                BackupDatabaseButton.IsVisible = true;
                using (var client = GetHttpClient())
                {
                    using var file = new FileStream(download_uri, FileMode.Create);
                    var progress = new Progress<double>();
                    progress.ProgressChanged += (s, value) =>
                    {
                        Log.D("Updates.Check", $"Download at {value}%");
                        DownloadProgress.Value = value;
                    };
                    cancellationToken = new CancellationTokenSource();
                    try
                    {
                        await DownloadFileAsync(client, file, uri, progress, cancellationToken.Token);
                    }
                    catch (Exception ex)
                    {
                        Log.E("Updates.Check", $"Error downloading update. {ex.Message}");
                        DialogBox.Show("Unable to download update.");
                        Close();
                    }
                }
                InstallButton.IsEnabled = true;
                BackupDatabaseButton.IsEnabled = true;
            }
            else if (((string)InstallButton.Content).Equals("Install", StringComparison.OrdinalIgnoreCase))
            {
                Log.D("Updates.DownloadWindow", "Install clicked.");
                using Process install = new();
                install.StartInfo.FileName = download_uri;
                install.Start();
                Close();
                mWindow.Exit();
            }
            else
            {
                Log.D("Updates.DownloadWindow", "Something went wrong and button text was not valid.");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Log.D("Updates.DownloadWindow", "Cancel clicked.");
            Close();
        }

        private void Window_Closing(object sender, WindowClosingEventArgs e)
        {
            cancellationToken?.Cancel();
        }

        private void BackupDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            Log.D("Updates.DownloadWindow", "Backup Database clicked.");
            UpdatePanel.IsVisible = false;
            BackupDatabaseButton.IsVisible = false;
            BackupPanel.IsVisible = true;
            BackupBlock.Text = $"{BackupBlock.Text}\nChecking for old database files.";
            // TODO - Might be windows specific -- turn into non-platform specific code.
            string dirPath = App.IsWindows ?
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), Constants.Settings.PROGRAM_DIR)
                : Path.Combine(Directory.GetCurrentDirectory(), "data");
            string path = Path.Combine(dirPath, MainWindow.DatabaseFileName);
            Log.D("Updates.DownloadWindow", "Looking for database file.");
            if (Directory.Exists(dirPath))
            {
                if (File.Exists(path))
                {
                    string backup = Path.Combine(dirPath, $"{DateTime.Now:yyyy-MM-dd}-backup-{MainWindow.DatabaseFileName}");
                    try
                    {
                        BackupBlock.Text = $"{BackupBlock.Text}\nBacking up database.";
                        File.Copy(path, backup, false);
                        BackupBlock.Text = $"{BackupBlock.Text}\n{backup}";
                    }
                    catch
                    {
                        BackupBlock.Text = $"{BackupBlock.Text}\nError backing up database.";
                    }
                }
            }
        }

        private void OnMinimize(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnMaximize(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            MaximizeIcon?.IsVisible = WindowState == WindowState.Normal;
            UnMaximizeIcon?.IsVisible = WindowState == WindowState.Maximized;
        }
    }
}
