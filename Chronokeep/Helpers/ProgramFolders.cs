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
using System.IO;
using System.Runtime.InteropServices;

namespace Chronokeep.Helpers
{
    /// <summary>
    /// The folders the program keeps its database, logs and exports in. They're shared by every user on the computer:
    /// Public Documents on Windows, /Users/Shared on macOS and /var/lib/chronokeep on Linux.
    /// </summary>
    internal static class ProgramFolders
    {
        // Every user can read, write and open these folders.
        private const UnixFileMode SharedFolderMode =
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
        // Every user can read and write these files.
        private const UnixFileMode SharedFileMode =
            UnixFileMode.UserRead | UnixFileMode.UserWrite |
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite;

        /// <summary>The folder the database is kept in on this computer.</summary>
        internal static string Data => GetDataFolder(CurrentPlatform());

        /// <summary>The folder error and crash logs are kept in on this computer.</summary>
        internal static string Logs => Path.Combine(Data, "logs");

        /// <summary>The default folder for exports on this computer.</summary>
        internal static string Exports => Path.Combine(Data, "exports");

        private static OSPlatform CurrentPlatform()
        {
            OSPlatform output = OSPlatform.Linux;
            if (OperatingSystem.IsWindows())
            {
                output = OSPlatform.Windows;
            }
            else if (OperatingSystem.IsMacOS())
            {
                output = OSPlatform.OSX;
            }
            return output;
        }

        /// <summary>
        /// The folder the database is kept in.
        /// </summary>
        /// <param name="platform">The operating system. Anything other than Windows or macOS is treated as Linux.</param>
        /// <returns>The data folder's full path.</returns>
        internal static string GetDataFolder(OSPlatform platform)
        {
            string output = "/var/lib/chronokeep";
            if (OSPlatform.Windows == platform)
            {
                output = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), Constants.Settings.PROGRAM_DIR);
            }
            else if (OSPlatform.OSX == platform)
            {
                // .NET doesn't map any special folder to this one on macOS.
                output = $"/Users/Shared/{Constants.Settings.PROGRAM_DIR}";
            }
            return output;
        }

        /// <summary>
        /// Creates a folder, and any parent folders the program needs, if it doesn't exist yet. On macOS and Linux a
        /// folder the program creates is made readable and writable by every user so anyone can use the program.
        /// </summary>
        /// <param name="path">The folder to create.</param>
        /// <returns>True if the folder exists once this is done.</returns>
        internal static bool CreateFolder(string path)
        {
            bool output = false;
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    Directory.CreateDirectory(path);
                }
                else if (!Directory.Exists(path))
                {
                    string? parent = Path.GetDirectoryName(path);
                    if (null != parent && !Directory.Exists(parent) && !CreateFolder(parent))
                    {
                        return output;
                    }
                    Directory.CreateDirectory(path, SharedFolderMode);
                    // A new folder's permissions are limited by the user's umask, so set them again.
                    File.SetUnixFileMode(path, SharedFolderMode);
                }
                output = Directory.Exists(path);
            }
            catch (Exception e)
            {
                Log.E("Helpers.ProgramFolders", $"Unable to create folder {path}. {e.Message}");
            }
            return output;
        }

        /// <summary>
        /// Checks that this user can create files in a folder.
        /// </summary>
        /// <param name="path">The folder to check.</param>
        /// <returns>True if a file could be created and removed there.</returns>
        internal static bool CanWrite(string path)
        {
            bool output = false;
            try
            {
                string testFile = Path.Combine(path, $".chronokeep_write_test_{Guid.NewGuid():N}");
                using (File.Create(testFile, 1, FileOptions.DeleteOnClose))
                {
                    output = true;
                }
            }
            catch (Exception e)
            {
                Log.E("Helpers.ProgramFolders", $"Unable to write to folder {path}. {e.Message}");
            }
            return output;
        }

        /// <summary>
        /// Lets every user read and write a file this user created, like the database or a log file.
        /// Does nothing on Windows, where the shared folder already allows it.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <returns>True if the file is shared, or nothing needed to be done.</returns>
        internal static bool ShareFile(string path)
        {
            bool output = true;
            if (OperatingSystem.IsWindows())
            {
                return output;
            }
            try
            {
                File.SetUnixFileMode(path, SharedFileMode);
            }
            catch
            {
                // Log.E shares its own log files through this, so logging here could loop.
                output = false;
            }
            return output;
        }

        /// <summary>
        /// What to tell the user when the data folder can't be created or written to.
        /// </summary>
        /// <param name="path">The data folder.</param>
        /// <returns>The message to show.</returns>
        internal static string FolderProblemMessage(string path)
        {
            string output = $"Chronokeep is unable to create or write to its data folder at {path}.";
            if (OperatingSystem.IsLinux())
            {
                output = $"Chronokeep keeps its data in {path}, which an administrator needs to set up. Run these commands and then start Chronokeep again:\n\nsudo mkdir -p {path}\nsudo chmod 777 {path}";
            }
            return output;
        }
    }
}
