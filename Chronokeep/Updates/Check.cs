using Chronokeep.Helpers;
using Chronokeep.Interfaces.UI;
using Chronokeep.UI;
using Chronokeep.UI.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Chronokeep.Updates
{
    public class Version
    {
        public int Major;
        public int Minor;
        public int Patch;

        public bool Equal(Version other)
        {
            return Major == other.Major && Minor == other.Minor && Patch == other.Patch;
        }

        public bool Newer(Version other)
        {
            return (Major > other.Major)
                   || (Major == other.Major && Minor > other.Minor)
                   || (Major == other.Major && Minor == other.Minor && Patch > other.Patch);
        }

        public void Set(Version other)
        {
            Major = other.Major;
            Minor = other.Minor;
            Patch = other.Patch;
        }

        public override string ToString()
        {
            return $"v{Major}.{Minor}.{Patch}";
        }
    }

    public static class Check
    {
        private const string RepoUrl = "https://api.github.com/repos/grecaun/chronokeep-desktop/releases";

        public static async void Do(IMainWindow mWindow, bool messageOnNoUpdate = false)
        {
            try
            {
                string curVersion;
                await using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Chronokeep.version.txt")!)
                {
                    using StreamReader reader = new(stream);
                    curVersion = await reader.ReadToEndAsync();
                }
                Version current = new();
                string[] version = curVersion.Split('.');
                if (version.Length >= 3)
                {
                    current.Major = int.Parse(version[0][1..]);
                    current.Minor = int.Parse(version[1]);
                    current.Patch = int.Parse(version[2].Split('-')[0]);
                }
                Log.D("Updates.Check", $"Current version found {current}");
                List<GithubRelease> releases;
                try
                {
                    releases = await GetReleases();
                }
                catch (Exception ex)
                {
                    Log.E("Updates.Check", ex.Message);
                    DialogBox.Show("Unable to check for update.");
                    return;
                }
                GithubRelease? latestRelease = null;
                Version latestVersion = new();
                foreach (GithubRelease release in releases)
                {
                    Version releaseVersion = new();
                    version = release.Name.Split('.');
                    if (version.Length >= 3)
                    {
                        releaseVersion.Major = int.Parse(version[0].Replace("v", ""));
                        releaseVersion.Minor = int.Parse(version[1]);
                        releaseVersion.Patch = int.Parse(version[2].Split('-')[0]);
                    }
                    // Check for major version updates
                    // Then minor version updates
                    // patches
                    if (!releaseVersion.Newer(latestVersion)) continue;
                    latestRelease = release;
                    latestVersion.Set(releaseVersion);
                }
                Log.D("Updates.Check", $"Latest version is {latestVersion}");
                if (latestVersion.Newer(current))
                {
                    Log.D("Updates.Check", "Newer version found.");
                    DownloadWindow downloadWindow = new(latestRelease!, latestVersion, mWindow);
                    _ = downloadWindow.ShowDialog(MainWindow.MWindow!);
                }
                else if (messageOnNoUpdate)
                {
                    DialogBox.Show("No updates found.");
                }
            }
            catch (Exception)
            {
                Log.D("Updates.Check", "Error checking releases.");
            }
        }

        private static async Task<List<GithubRelease>> GetReleases()
        {
            Log.D("Updates.Check", "Getting releases.");
            string content;
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new(HttpMethod.Get, RepoUrl);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Updates.Check", "Status Code OK");
                    string json = await response.Content.ReadAsStringAsync();
                    List<GithubRelease>? result = JsonSerializer.Deserialize<List<GithubRelease>>(json)!;
                    return result;
                }
                Log.D("Updates.Check", "Status Code not OK");
                content = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Exception thrown getting releases: {ex.Message} - {ex.InnerException}");
            }
            throw new Exception($"Unable to get releases. {content}");
        }

        private static HttpClient GetHttpClient()
        {
            HttpClient client = new();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.TryParseAdd("Chronokeep Desktop Application");
            return client;
        }
    }
}