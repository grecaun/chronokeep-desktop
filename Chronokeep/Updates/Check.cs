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
        public int major;
        public int minor;
        public int patch;

        public Version()
        {
            major = 0;
            minor = 0;
            patch = 0;
        }

        public bool Equal(Version other)
        {
            return this.major == other.major && this.minor == other.minor && this.patch == other.patch;
        }

        public bool Newer(Version other)
        {
            if ((this.major > other.major)
                || (this.major == other.major && this.minor > other.minor)
                || (this.major == other.major && this.minor == other.minor && this.patch > other.patch))
            {
                return true;
            }
            return false;
        }

        public void Set(Version other)
        {
            this.major = other.major;
            this.minor = other.minor;
            this.patch = other.patch;
        }

        public override string ToString()
        {
            return string.Format("v{0}.{1}.{2}", major, minor, patch);
        }
    }

    public class Check
    {
        private static readonly string RepoURL = "https://api.github.com/repos/grecaun/chronokeep-windows/releases";

        public static async void Do(IMainWindow mWindow, bool messageOnNoUpdate = false)
        {
            string curVersion;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Chronokeep." + "version.txt")!)
            {
                using StreamReader reader = new(stream);
                curVersion = reader.ReadToEnd();
            }
            Version current = new();
            string[] version = curVersion.Split('.');
            if (version.Length >= 3)
            {
                current.major = int.Parse(version[0][1..]);
                current.minor = int.Parse(version[1]);
                current.patch = int.Parse(version[2].Split('-')[0]);
            }
            Log.D("Updates.Check", string.Format("Current version found {0}", current.ToString()));
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
                    releaseVersion.major = int.Parse(version[0].Replace("v", ""));
                    releaseVersion.minor = int.Parse(version[1]);
                    releaseVersion.patch = int.Parse(version[2].Split('-')[0]);
                }
                // Check for major version updates
                // Then minor version updates
                // patches
                if (releaseVersion.Newer(latestVersion))
                {
                    latestRelease = release;
                    latestVersion.Set(releaseVersion);
                }
            }
            Log.D("Updates.Check", string.Format("Latest version is {0}", latestVersion.ToString()));
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

        private static async Task<List<GithubRelease>> GetReleases()
        {
            Log.D("Updates.Check", "Getting releases.");
            string content = "";
            try
            {
                using HttpClient client = GetHttpClient();
                HttpRequestMessage request = new(HttpMethod.Get, RepoURL);
                HttpResponseMessage response = await client.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.D("Updates.Check", "Status Code OK");
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<List<GithubRelease>>(json)!;
                    return result;
                }
                Log.D("Updates.Check", "Status Code not OK");
                content = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                throw new Exception("Exception thrown getting releases: " + ex.Message + " - " + ex.InnerException);
            }
            throw new Exception(string.Format("Unable to get releases. {0}", content));
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