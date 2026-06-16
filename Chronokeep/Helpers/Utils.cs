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
