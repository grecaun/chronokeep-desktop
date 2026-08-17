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

using Avalonia.Platform.Storage;
using Microsoft.Win32;
using System;

namespace Chronokeep.Helpers
{
    public static class Utils
    {
        private const string REGISTRY_KEY_NAME = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string APPS_USE_LIGHT_THEME = "AppsUseLightTheme";

        public static string UppercaseFirst(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            char[] charArray = s.ToCharArray();
            charArray[0] = char.ToUpper(charArray[0]);
            return new string(charArray);
        }

        public static int GetSystemTheme()
        {
            if (!OperatingSystem.IsWindows()) return -1;
            object? registryValue = Registry.GetValue(REGISTRY_KEY_NAME, APPS_USE_LIGHT_THEME, -1);
            if (registryValue != null)
            {
                return int.Parse(registryValue.ToString()!);
            }
            return -1;
        }

        public enum FileType { CSV, EXCEL }

        public static FilePickerFileType ExcelType { get; } = new("Excel Files")
        {
            Patterns = ["*.xlsx", "*.xls", "*.csv"],
        };

        public static FilePickerFileType LogType { get; } = new("Log Files")
        {
            Patterns = ["*.csv", "*.txt", "*.log"],
        };

        public static FilePickerFileType CsvType { get; } = new("CSV Files")
        {
            Patterns = ["*.csv"],
        };

        public static FilePickerFileType HtmlType { get; } = new("HTML Files")
        {
            Patterns = ["*.htm", "*.html"],
        };

        public static FilePickerFileType PdfType { get; } = new("PDF Files")
        {
            Patterns = ["*.pdf"],
        };

        public static FilePickerFileType SqLiteType { get; } = new("SQLite Database Files")
        {
            Patterns = ["*.sqlite"],
        };
    }
}

