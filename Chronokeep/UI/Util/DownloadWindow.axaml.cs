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
    public partial class DownloadWindow : ChronokeepWindow
    {
        private readonly string uri;
        private readonly string downloadUri;
        private readonly string version;

        private CancellationTokenSource? cancellationToken;

        private readonly IMainWindow mWindow;

        public DownloadWindow(GithubRelease r, Updates.Version v, IMainWindow mWindow)
        {
            InitializeComponent();
            ChronokeepInitialize();
            Topmost = true;
            this.mWindow = mWindow;
            DownloadProgress.IsVisible = false;
            version = v.ToString();
            if (App.IsWindows)
            {
                downloadUri = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\chronokeep-setup-{version}.exe";
            }
            else
            {
                downloadUri = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\chronokeep-{version}.tar.gz";
                uri = "";
                Close();
                DialogBox.AsyncShow("Linux downloads not yet implemented.");
                return;
            }
            Log.D("Updates.Check", $"Download URL - {r.Assets[0].BrowserDownloadUrl}");
            uri = r.Assets[0].BrowserDownloadUrl;
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
        private static async Task DownloadFileAsync(HttpClient client, Stream destination, string uri, IProgress<double>? progress, CancellationToken token)
        {
            HttpResponseMessage response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"The request returned with HTTP status code{response.StatusCode}");
            }

            long total = response.Content.Headers.ContentLength ?? -1L;
            bool canReportProgress = total != -1 && progress != null;

            await using Stream stream = await response.Content.ReadAsStreamAsync(token);
            long totalRead = 0L;
            byte[] buffer = new byte[8192];
            bool isMoreToRead = true;
            double lastReport = 0L;
            do
            {
                token.ThrowIfCancellationRequested();
                int read = await stream.ReadAsync(buffer, token);
                if (read == 0)
                {
                    isMoreToRead = false;
                }
                else
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read), token);
                    totalRead += read;
                    double report = Math.Truncate((totalRead * 1d) / (total * 1d) * 1000) / 10;
                    if (!canReportProgress || ((!(report > lastReport + 0.5)) && !(report >= 100))) continue;
                    progress!.Report(report);
                    lastReport = report;
                }
            } while (isMoreToRead);

        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (((string)InstallButton.Content!).Equals("Download", StringComparison.OrdinalIgnoreCase))
                {
                    Log.D("Updates.DownloadWindow", $"Download clicked. Downloading to {downloadUri}");
                    DownloadProgress.IsVisible = true;
                    DownloadLabel.Text = $"Downloading {version}";
                    InstallButton.Content = "Install";
                    InstallButton.IsEnabled = false;
                    BackupDatabaseButton.IsEnabled = false;
                    BackupDatabaseButton.IsVisible = true;
                    using (HttpClient client = GetHttpClient())
                    {
                        await using FileStream file = new(downloadUri, FileMode.Create);
                        Progress<double> progress = new();
                        progress.ProgressChanged += (_, value) =>
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
                            DialogBox.AsyncShow("Unable to download update.");
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
                    install.StartInfo.FileName = downloadUri;
                    install.Start();
                    Close();
                    mWindow.Exit();
                }
                else
                {
                    Log.D("Updates.DownloadWindow", "Something went wrong and button text was not valid.");
                }
            }
            catch (Exception)
            {
                Log.D("Updates.DownloadWindow", "Something went wrong installing.");
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
            string dirPath = App.IsWindows ?
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), Constants.Settings.PROGRAM_DIR)
                : Path.Combine(Directory.GetCurrentDirectory(), "data");
            string path = Path.Combine(dirPath, MainWindow.DATABASE_FILE_NAME);
            Log.D("Updates.DownloadWindow", "Looking for database file.");
            if (!Directory.Exists(dirPath)) return;
            if (!File.Exists(path)) return;
            string backup = Path.Combine(dirPath, $"{DateTime.Now:yyyy-MM-dd}-backup-{MainWindow.DATABASE_FILE_NAME}");
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

        protected override Border? TitleBar()
        {
            return ChronokeepToolBar;
        }
    }
}
