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

using Avalonia.Controls;
using Avalonia.Interactivity;
using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.Updates;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        // The update's name in the release, used to find its checksum.
        private readonly string fileName;
        private readonly string checksumsUrl;
        private readonly UpdateKind kind;

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
            // Check.Do only opens this window when the release has an update for this computer.
            kind = UpdatePackage.CurrentKind(File.Exists);
            Assets? asset = UpdatePackage.FindAsset(r, kind);
            uri = asset?.BrowserDownloadUrl ?? "";
            fileName = asset?.Name ?? "";
            checksumsUrl = UpdatePackage.ChecksumsUrl(r);
            Log.D("Updates.Check", $"Download URL - {uri}");
            downloadUri = UpdateKind.WindowsInstaller == kind
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"chronokeep-setup-{version}.exe")
                : Path.Combine(Path.GetTempPath(), fileName);
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
                    cancellationToken = new CancellationTokenSource();
                    using HttpClient client = GetHttpClient();
                    try
                    {
                        await using (FileStream file = new(downloadUri, FileMode.Create))
                        {
                            Progress<double> progress = new();
                            progress.ProgressChanged += (_, value) =>
                            {
                                Log.D("Updates.Check", $"Download at {value}%");
                                DownloadProgress.Value = value;
                            };
                            await DownloadFileAsync(client, file, uri, progress, cancellationToken.Token);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.E("Updates.Check", $"Error downloading update. {ex.Message}");
                        DeleteDownload();
                        DialogBox.AsyncShow("Unable to download update.");
                        Close();
                        return;
                    }
                    // Don't offer to install anything that doesn't check out.
                    DownloadLabel.Text = $"Checking {version}";
                    string problem = await UpdateVerifier.Verify(client, checksumsUrl, UpdateVerifier.AttestationsUrl, fileName, downloadUri, cancellationToken.Token);
                    if (problem.Length > 0)
                    {
                        Log.E("Updates.DownloadWindow", $"Update failed its check. {problem}");
                        DeleteDownload();
                        DialogBox.AsyncShow(problem);
                        Close();
                        return;
                    }
                    DownloadLabel.Text = $"Ready to install {version}";
                    InstallButton.IsEnabled = true;
                    BackupDatabaseButton.IsEnabled = true;
                }
                else if (((string)InstallButton.Content).Equals("Install", StringComparison.OrdinalIgnoreCase))
                {
                    Log.D("Updates.DownloadWindow", "Install clicked.");
                    switch (kind)
                    {
                        case UpdateKind.WindowsInstaller:
                            using (Process install = new())
                            {
                                install.StartInfo.FileName = downloadUri;
                                install.Start();
                            }
                            Close();
                            mWindow.Exit();
                            break;
                        case UpdateKind.MacDiskImage:
                            InstallMac();
                            break;
                        case UpdateKind.LinuxDeb:
                        case UpdateKind.LinuxRpm:
                            await InstallLinux();
                            break;
                        default:
                            break;
                    }
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

        private void DeleteDownload()
        {
            try
            {
                File.Delete(downloadUri);
            }
            catch (Exception e)
            {
                Log.E("Updates.DownloadWindow", $"Unable to delete the download. {e.Message}");
            }
        }

        /// <summary>
        /// Runs one of UpdatePackage's shell scripts. It keeps running after Chronokeep closes.
        /// </summary>
        /// <param name="script">The script's contents.</param>
        /// <param name="name">The file name to save the script as.</param>
        /// <param name="arguments">The script's arguments.</param>
        private static void StartScript(string script, string name, List<string> arguments)
        {
            string scriptPath = Path.Combine(Path.GetTempPath(), name);
            File.WriteAllText(scriptPath, script);
            using Process process = new();
            process.StartInfo.FileName = "/bin/sh";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.ArgumentList.Add(scriptPath);
            foreach (string argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }
            process.Start();
        }

        /// <summary>
        /// Replaces Chronokeep.app with the downloaded one after Chronokeep closes. If the app can't be replaced
        /// (it isn't running from an app, or this user can't change the folder it's in) the .dmg is opened instead
        /// so the new app can be dragged into Applications.
        /// </summary>
        private void InstallMac()
        {
            string app = UpdatePackage.MacAppBundle(AppContext.BaseDirectory);
            string? appFolder = app.Length > 0 ? Path.GetDirectoryName(app) : null;
            if (null == appFolder || !ProgramFolders.CanWrite(appFolder))
            {
                using Process open = new();
                open.StartInfo.FileName = "open";
                open.StartInfo.UseShellExecute = false;
                open.StartInfo.ArgumentList.Add(downloadUri);
                open.Start();
            }
            else
            {
                StartScript(UpdatePackage.MacUpdateScript, "chronokeep-update.sh", [Environment.ProcessId.ToString(), downloadUri, app]);
            }
            Close();
            mWindow.Exit();
        }

        /// <summary>
        /// Installs the downloaded package with the system's package manager, which asks for an administrator
        /// password, then starts the new version once Chronokeep closes.
        /// </summary>
        private async Task InstallLinux()
        {
            List<string> command = UpdatePackage.LinuxInstallCommand(kind, downloadUri, File.Exists);
            if (command.Count < 1)
            {
                DialogBox.AsyncShow($"Chronokeep can't install the update itself on this computer. The package was saved to {downloadUri}.");
                Close();
                return;
            }
            DownloadLabel.Text = $"Installing {version}";
            InstallButton.IsEnabled = false;
            using Process install = new();
            install.StartInfo.FileName = command[0];
            install.StartInfo.UseShellExecute = false;
            foreach (string argument in command.Skip(1))
            {
                install.StartInfo.ArgumentList.Add(argument);
            }
            install.Start();
            await install.WaitForExitAsync();
            if (0 != install.ExitCode)
            {
                Log.E("Updates.DownloadWindow", $"Package install exited with {install.ExitCode}.");
                DialogBox.AsyncShow($"The update wasn't installed. The package was saved to {downloadUri} if you'd like to install it yourself.");
                DownloadLabel.Text = $"Ready to install {version}";
                InstallButton.IsEnabled = true;
                return;
            }
            DeleteDownload();
            StartScript(UpdatePackage.RestartScript, "chronokeep-restart.sh", [Environment.ProcessId.ToString(), "/usr/bin/chronokeep"]);
            Close();
            mWindow.Exit();
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
            string dirPath = ProgramFolders.Data;
            string path = Path.Combine(dirPath, MainWindow.DATABASE_FILE_NAME);
            Log.D("Updates.DownloadWindow", "Looking for database file.");
            if (!Directory.Exists(dirPath)) return;
            if (!File.Exists(path)) return;
            string backup = Path.Combine(dirPath, $"{DateTime.Now:yyyy-MM-dd}-backup-{MainWindow.DATABASE_FILE_NAME}");
            try
            {
                BackupBlock.Text = $"{BackupBlock.Text}\nBacking up database.";
                File.Copy(path, backup, false);
                ProgramFolders.ShareFile(backup);
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

