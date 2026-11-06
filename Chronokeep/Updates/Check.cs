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
using System.Text.RegularExpressions;
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

        private static readonly Regex VersionPattern = new(@"^\s*[vV]?(\d+)\.(\d+)\.(\d+)");

        /// <summary>
        /// Reads "major.minor.patch" from the start of a tag, release name, or git describe output,
        /// with an optional leading "v" and anything after it ignored ("v2.0.0-beta", "v1.9.10-5-gabc123").
        /// </summary>
        public static bool TryParse(string? text, out Version version)
        {
            version = new Version();
            Match match = VersionPattern.Match(text ?? "");
            if (!match.Success
                || !int.TryParse(match.Groups[1].Value, out int major)
                || !int.TryParse(match.Groups[2].Value, out int minor)
                || !int.TryParse(match.Groups[3].Value, out int patch))
            {
                return false;
            }
            version.Major = major;
            version.Minor = minor;
            version.Patch = patch;
            return true;
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
                Version.TryParse(curVersion, out Version current);
                Log.D("Updates.Check", $"Current version found {current}");
                List<GithubRelease> releases;
                try
                {
                    releases = await GetReleases();
                }
                catch (Exception ex)
                {
                    Log.E("Updates.Check", ex.Message);
                    DialogBox.AsyncShow("Unable to check for update.");
                    return;
                }
                (GithubRelease? latestRelease, Version latestVersion) = FindLatest(releases);
                Log.D("Updates.Check", $"Latest version is {latestVersion}");
                if (latestVersion.Newer(current))
                {
                    Log.D("Updates.Check", "Newer version found.");
                    DownloadWindow downloadWindow = new(latestRelease!, latestVersion, mWindow);
                    _ = downloadWindow.ShowDialog(MainWindow.MWindow!);
                }
                else if (messageOnNoUpdate)
                {
                    DialogBox.AsyncShow("No updates found.");
                }
            }
            catch (Exception)
            {
                Log.D("Updates.Check", "Error checking releases.");
            }
        }

        /// <summary>
        /// Picks the newest published release. Drafts and prereleases are skipped, the version comes
        /// from the tag (falling back to the release name), and releases without a readable version are ignored.
        /// </summary>
        internal static (GithubRelease? Release, Version Version) FindLatest(IEnumerable<GithubRelease> releases)
        {
            GithubRelease? latestRelease = null;
            Version latestVersion = new();
            foreach (GithubRelease release in releases)
            {
                if (release.Draft || release.PreRelease) continue;
                if (!Version.TryParse(release.TagName, out Version releaseVersion)
                    && !Version.TryParse(release.Name, out releaseVersion)) continue;
                if (!releaseVersion.Newer(latestVersion)) continue;
                latestRelease = release;
                latestVersion.Set(releaseVersion);
            }
            return (latestRelease, latestVersion);
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
