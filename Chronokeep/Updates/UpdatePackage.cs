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

using System;
using System.Collections.Generic;
using System.IO;

namespace Chronokeep.Updates
{
    /// <summary>
    /// The kinds of update the release builds, one for each way Chronokeep is installed.
    /// </summary>
    internal enum UpdateKind
    {
        None,
        WindowsInstaller,
        MacDiskImage,
        LinuxDeb,
        LinuxRpm
    }

    /// <summary>
    /// Works out which release file updates this computer and how to install it.
    /// </summary>
    internal static class UpdatePackage
    {
        /// <summary>
        /// The kind of update this computer needs.
        /// </summary>
        /// <param name="fileExists">Checks if a file exists, used to see which package manager Linux has.</param>
        /// <returns>The kind of update, or None if there isn't one for this system.</returns>
        internal static UpdateKind CurrentKind(Func<string, bool> fileExists)
        {
            UpdateKind output = UpdateKind.None;
            if (OperatingSystem.IsWindows())
            {
                output = UpdateKind.WindowsInstaller;
            }
            else if (OperatingSystem.IsMacOS())
            {
                output = UpdateKind.MacDiskImage;
            }
            else if (OperatingSystem.IsLinux())
            {
                output = LinuxKind(fileExists);
            }
            return output;
        }

        /// <summary>
        /// The Linux package type to update with, based on the package manager installed.
        /// </summary>
        /// <param name="fileExists">Checks if a file exists.</param>
        /// <returns>LinuxDeb for dpkg systems (Debian, Ubuntu), LinuxRpm for rpm systems (Fedora, openSUSE), otherwise None.</returns>
        internal static UpdateKind LinuxKind(Func<string, bool> fileExists)
        {
            UpdateKind output = UpdateKind.None;
            if (fileExists("/usr/bin/dpkg"))
            {
                output = UpdateKind.LinuxDeb;
            }
            else if (fileExists("/usr/bin/rpm"))
            {
                output = UpdateKind.LinuxRpm;
            }
            return output;
        }

        /// <summary>
        /// Finds the release file for a kind of update.
        /// </summary>
        /// <param name="release">The release.</param>
        /// <param name="kind">The kind of update.</param>
        /// <returns>The file, or null if the release doesn't have one.</returns>
        internal static Assets? FindAsset(GithubRelease release, UpdateKind kind)
        {
            string extension = kind switch
            {
                UpdateKind.WindowsInstaller => ".exe",
                UpdateKind.MacDiskImage => ".dmg",
                UpdateKind.LinuxDeb => ".deb",
                UpdateKind.LinuxRpm => ".rpm",
                _ => ""
            };
            Assets? output = null;
            if (extension.Length < 1)
            {
                return output;
            }
            foreach (Assets asset in release.Assets)
            {
                if (asset.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    output = asset;
                    break;
                }
            }
            return output;
        }

        /// <summary>
        /// Finds the release's SHA256SUMS file.
        /// </summary>
        /// <param name="release">The release.</param>
        /// <returns>Where to download it, or an empty string if the release doesn't have one.</returns>
        internal static string ChecksumsUrl(GithubRelease release)
        {
            string output = "";
            foreach (Assets asset in release.Assets)
            {
                if (UpdateVerifier.ChecksumsFileName == asset.Name)
                {
                    output = asset.BrowserDownloadUrl;
                    break;
                }
            }
            return output;
        }

        /// <summary>
        /// The command that installs a downloaded Linux package. pkexec asks for an administrator password
        /// in a window, since installing a package needs root.
        /// </summary>
        /// <param name="kind">LinuxDeb or LinuxRpm.</param>
        /// <param name="packagePath">The full path to the downloaded package.</param>
        /// <param name="fileExists">Checks if a file exists, used to find the package manager.</param>
        /// <returns>The program and its arguments, or an empty list if there's no way to install it.</returns>
        internal static List<string> LinuxInstallCommand(UpdateKind kind, string packagePath, Func<string, bool> fileExists)
        {
            List<string> output = [];
            if (!fileExists("/usr/bin/pkexec"))
            {
                return output;
            }
            if (UpdateKind.LinuxDeb == kind && fileExists("/usr/bin/apt-get"))
            {
                // apt-get installs a local file when given its full path, and pulls in anything it depends on.
                output = ["/usr/bin/pkexec", "/usr/bin/apt-get", "install", "-y", packagePath];
            }
            else if (UpdateKind.LinuxRpm == kind && fileExists("/usr/bin/dnf"))
            {
                output = ["/usr/bin/pkexec", "/usr/bin/dnf", "install", "-y", packagePath];
            }
            else if (UpdateKind.LinuxRpm == kind && fileExists("/usr/bin/zypper"))
            {
                output = ["/usr/bin/pkexec", "/usr/bin/zypper", "--non-interactive", "install", packagePath];
            }
            return output;
        }

        /// <summary>
        /// The Chronokeep.app this program is running from on macOS.
        /// </summary>
        /// <param name="baseDirectory">The program's folder, AppContext.BaseDirectory, which is Chronokeep.app/Contents/MacOS/.</param>
        /// <returns>The app's path, or an empty string if the program isn't running from an app.</returns>
        internal static string MacAppBundle(string baseDirectory)
        {
            string output = "";
            DirectoryInfo macOS = new(baseDirectory.TrimEnd('/', '\\'));
            DirectoryInfo? contents = macOS.Parent;
            DirectoryInfo? bundle = contents?.Parent;
            if ("MacOS" == macOS.Name
                && null != contents && "Contents" == contents.Name
                && null != bundle && bundle.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            {
                output = bundle.FullName;
            }
            return output;
        }

        /// <summary>
        /// A shell script that replaces Chronokeep.app with the one in a downloaded .dmg once this program has closed,
        /// then opens the new one. Run it with sh, passing this program's process id, the .dmg's path and the app's path.
        /// If anything goes wrong it puts the old app back and opens the .dmg so the update can be done by hand.
        /// </summary>
        internal const string MacUpdateScript = """
            #!/bin/sh
            PID="$1"
            DMG="$2"
            APP="$3"
            # Wait for Chronokeep to close.
            while kill -0 "$PID" 2>/dev/null; do sleep 1; done
            MOUNT="$(mktemp -d)"
            if ! hdiutil attach -nobrowse -readonly -mountpoint "$MOUNT" "$DMG"; then
                open "$DMG"
                exit 1
            fi
            # Move the old app aside first so it can be put back, and only delete it once the new one is in place.
            rm -rf "$APP.old"
            if ! mv "$APP" "$APP.old"; then
                hdiutil detach "$MOUNT" -quiet
                open "$DMG"
                exit 1
            fi
            if ! ditto "$MOUNT/Chronokeep.app" "$APP"; then
                rm -rf "$APP"
                mv "$APP.old" "$APP"
                hdiutil detach "$MOUNT" -quiet
                open "$DMG"
                exit 1
            fi
            rm -rf "$APP.old"
            hdiutil detach "$MOUNT" -quiet
            rm -f "$DMG"
            open "$APP"
            """;

        /// <summary>
        /// A shell script that starts Chronokeep again once this program has closed. Run it with sh, passing this
        /// program's process id and the program to start.
        /// </summary>
        internal const string RestartScript = """
            #!/bin/sh
            PID="$1"
            # Wait for Chronokeep to close.
            while kill -0 "$PID" 2>/dev/null; do sleep 1; done
            exec "$2"
            """;
    }
}
