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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Chronokeep.Updates
{
    /// <summary>
    /// Checks a downloaded update before it's installed. The file has to match the SHA-256 listed in the release's
    /// SHA256SUMS, and GitHub has to have a build attestation for that SHA-256 from this repository's release
    /// workflow, which shows the file was built by us and not swapped in afterwards.
    /// </summary>
    internal static class UpdateVerifier
    {
        internal const string ChecksumsFileName = "SHA256SUMS";
        internal const string AttestationsUrl = "https://api.github.com/repos/grecaun/chronokeep-desktop/attestations/sha256:";

        /// <summary>
        /// Reads a SHA256SUMS file, as written by sha256sum.
        /// </summary>
        /// <param name="text">The file's contents. Each line is a hash, a space, then the file name (with a * before it in binary mode).</param>
        /// <returns>The lower case hash for each file name.</returns>
        internal static Dictionary<string, string> ParseChecksums(string text)
        {
            Dictionary<string, string> output = [];
            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.Trim();
                int space = line.IndexOf(' ');
                if (space != 64)
                {
                    continue;
                }
                string hash = line[..space].ToLowerInvariant();
                string name = line[(space + 1)..].TrimStart(' ', '*');
                if (name.Length > 0 && hash.All(Uri.IsHexDigit))
                {
                    output[name] = hash;
                }
            }
            return output;
        }

        /// <summary>
        /// Works out a file's SHA-256.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <returns>The hash as lower case hex.</returns>
        internal static string FileSha256(string path)
        {
            using FileStream file = File.OpenRead(path);
            return Convert.ToHexStringLower(SHA256.HashData(file));
        }

        /// <summary>
        /// Checks GitHub's answer to a request for a file's attestations.
        /// </summary>
        /// <param name="json">The response from the attestations API, like {"attestations":[...]}.</param>
        /// <returns>True if there's at least one attestation.</returns>
        internal static bool HasAttestation(string json)
        {
            bool output = false;
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("attestations", out JsonElement attestations)
                    && JsonValueKind.Array == attestations.ValueKind)
                {
                    output = attestations.GetArrayLength() > 0;
                }
            }
            catch (JsonException e)
            {
                Log.E("Updates.UpdateVerifier", $"Unable to read the attestations response. {e.Message}");
            }
            return output;
        }

        /// <summary>
        /// Checks a downloaded update.
        /// </summary>
        /// <param name="client">The HTTP client to use.</param>
        /// <param name="checksumsUrl">Where to download the release's SHA256SUMS, or an empty string if the release has none.</param>
        /// <param name="attestationsUrl">The attestations API address the file's SHA-256 is added to (see AttestationsUrl).</param>
        /// <param name="fileName">The downloaded file's name in the release.</param>
        /// <param name="filePath">Where the file was downloaded to.</param>
        /// <param name="token">Cancels the check.</param>
        /// <returns>An empty string if the file checks out, otherwise why it doesn't, to show the user.</returns>
        internal static async Task<string> Verify(
            HttpClient client,
            string checksumsUrl,
            string attestationsUrl,
            string fileName,
            string filePath,
            CancellationToken token
            )
        {
            string output = "";
            if (checksumsUrl.Length < 1)
            {
                return "This release doesn't list checksums for its files, so the download can't be checked.";
            }
            string fileHash = FileSha256(filePath);
            try
            {
                Dictionary<string, string> checksums = ParseChecksums(await client.GetStringAsync(checksumsUrl, token));
                if (!checksums.TryGetValue(fileName, out string? expectedHash))
                {
                    output = $"This release's checksums don't list {fileName}, so the download can't be checked.";
                }
                else if (expectedHash != fileHash)
                {
                    output = "The download doesn't match this release's checksum. It may have been damaged or changed.";
                }
                else if (!HasAttestation(await client.GetStringAsync($"{attestationsUrl}{fileHash}", token)))
                {
                    output = "GitHub has no record of this file being built by Chronokeep's release process, so it won't be installed.";
                }
            }
            catch (HttpRequestException e)
            {
                Log.E("Updates.UpdateVerifier", $"Unable to check the download. {e.Message}");
                output = "Unable to reach GitHub to check the download.";
            }
            return output;
        }
    }
}
