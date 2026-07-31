using Chronokeep.Database;
using Chronokeep.Objects;
using Chronokeep.Objects.ChronokeepPortal;
using Chronokeep.Objects.ChronokeepRemote;
using Chronokeep.UI.Util;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Chronokeep.Helpers
{
    internal static class Globals
    {
        public static int UploadInterval = -1;
        public static int DownloadInterval = -1;
        public static int AnnouncerWindow = 45;
        public static string ErrorLogPath = "";

        public static void SetupValues(IdbInterface db)
        {
            if (!int.TryParse(db.GetAppSetting(Constants.Settings.UPLOAD_INTERVAL)!.Value, out UploadInterval))
            {
                DialogBox.AsyncShow("Something went wrong trying to get the upload interval.");
            }
            if (!int.TryParse(db.GetAppSetting(Constants.Settings.DOWNLOAD_INTERVAL)!.Value, out DownloadInterval))
            {
                DialogBox.AsyncShow("Something went wrong trying to get the download interval.");
            }
            if (!int.TryParse(db.GetAppSetting(Constants.Settings.ANNOUNCER_WINDOW)!.Value, out AnnouncerWindow))
            {
                DialogBox.AsyncShow("Something went wrong trying to get the announcer window.");
            }
        }

        private static readonly List<BibChipAssociation> ignoredChips = [];
        private static readonly Lock ignoredChipsLock = new();

        public static List<BibChipAssociation> GetIgnoredChips()
        {
            List<BibChipAssociation> output = [];
            if (!ignoredChipsLock.TryEnter(1000)) return output;
            try
            {
                output.AddRange(ignoredChips);
            }
            finally
            {
                ignoredChipsLock.Exit();
            }
            return output;
        }

        public static void UpdateIgnoredChips(IdbInterface database)
        {
            if (!ignoredChipsLock.TryEnter(1000)) return;
            try
            {
                ignoredChips.Clear();
                database.GetBibChips(-1);
                ignoredChips.AddRange(database.GetBibChips(-1));
            }
            finally
            {
                ignoredChipsLock.Exit();
            }
        }

        private static readonly Dictionary<(string, RemoteNotification), ReaderMessage> readerMessages = [];
        private static readonly Lock readerMessageLock = new();

        public static List<ReaderMessage> GetReaderMessages()
        {
            List<ReaderMessage> output = [];
            if (!readerMessageLock.TryEnter(1000)) return output;
            try
            {
                output.AddRange(readerMessages.Values);
            }
            finally
            {
                readerMessageLock.Exit();
            }
            return output;
        }

        public static void UpdateReaderMessages(List<ReaderMessage> msgs)
        {
            if (!readerMessageLock.TryEnter(1000)) return;
            try
            {
                foreach (ReaderMessage m in msgs)
                {
                    if (readerMessages.TryGetValue((m.SystemName, m.Message), out ReaderMessage? found))
                    {
                        found.Notified = m.Notified;
                    }
                }
            }
            finally
            {
                readerMessageLock.Exit();
            }
        }

        public static void UpdateReaderMessage(ReaderMessage msg)
        {
            if (!readerMessageLock.TryEnter(1000)) return;
            try
            {
                if (readerMessages.TryGetValue((msg.SystemName, msg.Message), out ReaderMessage? found))
                {
                    found.Notified = msg.Notified;
                }
            }
            finally
            {
                readerMessageLock.Exit();
            }
        }

        public static void ClearReaderMessages()
        {
            if (!readerMessageLock.TryEnter(1000)) return;
            try
            {
                readerMessages.Clear();
            }
            finally
            {
                readerMessageLock.Exit();
            }
        }

        public static bool AddReaderMessage(ReaderMessage msg)
        {
            if (!readerMessageLock.TryEnter(1000)) return false;
            try
            {
                readerMessages.Add((msg.SystemName, msg.Message), msg);
            }
            finally
            {
                readerMessageLock.Exit();
            }
            return true;
        }
    }
    public class ReaderMessage : IComparable
    {
        public SeverityLevel Severity;
        public RemoteNotification Message = new();
        public bool Notified;
        public string SystemName = "";
        public string Address = "";

        public enum SeverityLevel
        {
            High,
            Moderate,
            Low
        }

        public string Who => SystemName;
        public string Where => Address;
        public string When => Message.When;
        public string Information => PortalNotification.GetRemoteNotificationMessage(Message.Type);
        public string DialogBoxString => PortalNotification.GetRemoteNotificationMessage(SystemName, Address, Message);
        public string SeverityString => Severity switch
        {
            SeverityLevel.High => "High",
            SeverityLevel.Moderate => "Moderate",
            _ => "Low"
        };
        public string Background => Severity switch
        {
            SeverityLevel.High => "#3FFF0000",
            SeverityLevel.Moderate => "#4FF75605",
            _ => "#3FF7CF05"
        };

        public int CompareTo(object? other)
        {
            if (other is not ReaderMessage message) return -1;
            if (DateTime.TryParse(Message.When, out DateTime thisWhen)
                && DateTime.TryParse(message.Message.When, out DateTime otherWhen))
            {
                return thisWhen.CompareTo(otherWhen);
            }
            return string.Compare(Message.Type, message.Message.Type, StringComparison.Ordinal);
        }

        public bool Equals(ReaderMessage other)
        {
            return Severity == other.Severity && When.Equals(other.When, StringComparison.Ordinal) && Address.Equals(other.Address, StringComparison.Ordinal) && Message.Type.Equals(other.Message.Type, StringComparison.Ordinal);
        }
    }
}
