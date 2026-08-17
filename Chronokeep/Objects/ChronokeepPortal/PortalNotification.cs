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
using Chronokeep.Objects.ChronokeepRemote;

namespace Chronokeep.Objects.ChronokeepPortal
{
    internal static class PortalNotification
    {
        public const string UPS_DISCONNECTED = "UPS_DISCONNECTED";
        public const string UPS_CONNECTED = "UPS_CONNECTED";
        public const string UPS_ON_BATTERY = "UPS_ON_BATTERY";
        public const string UPS_LOW_BATTERY = "UPS_LOW_BATTERY";
        public const string UPS_ONLINE = "UPS_ONLINE";
        public const string SHUTTING_DOWN = "SHUTTING_DOWN";
        public const string RESTARTING = "RESTARTING";
        public const string HIGH_TEMP = "HIGH_TEMP";
        public const string MAX_TEMP = "MAX_TEMP";
        public const string BATTERY_LOW = "BATTERY_LOW";
        public const string BATTERY_CRITICAL = "BATTERY_CRITICAL";

        public static string GetRemoteNotificationMessage(string reader, string address, RemoteNotification message)
        {
            if (!string.IsNullOrEmpty(message.Message))
            {
                Log.E("Objects.ChronokeepPortal.PortalNotification", $"Unknown message: '{message.Type}' '{message.Message}'");
            }
            return message.Type switch
            {
                PortalError.TOO_MANY_REMOTE_API => $"Notification from '{reader}-{address}'\nOnly one remote api can be assigned to a portal.\nPlease remove or update the currently defined remote api.",
                PortalError.TOO_MANY_CONNECTIONS => $"Notification from '{reader}-{address}'\nUnable to connect to the timing system, there are too many devices already connected.",
                PortalError.SERVER_ERROR => $"Notification from '{reader}-{address}'\nThe timing system encountered an error trying to process that request.",
                PortalError.DATABASE_ERROR => $"Notification from '{reader}-{address}'\nThe timing system encountered an error with their database while trying to process that request.",
                PortalError.INVALID_READER_TYPE => $"Notification from '{reader}-{address}'\nReader type specified is not valid.",
                PortalError.READER_CONNECTION => $"Notification from '{reader}-{address}'\nUnable to connect to the reader.",
                PortalError.NOT_FOUND => $"Notification from '{reader}-{address}'\nThe timing system was not able to find the specified value.",
                PortalError.INVALID_SETTING => $"Notification from '{reader}-{address}'\nOne or more settings specified was not valid.",
                PortalError.INVALID_API_TYPE => $"Notification from '{reader}-{address}'\nAPI type specified is not valid.",
                PortalError.ALREADY_SUBSCRIBED => $"Notification from '{reader}-{address}'\nUser is already subscribed to reads/sightings from the timing system.",
                PortalError.ALREADY_RUNNING => $"Notification from '{reader}-{address}'\nUnable to start the specified reader as it is already running.",
                PortalError.NOT_RUNNING => $"Notification from '{reader}-{address}'\nUnable to stop the specified reader as it is not currently running.",
                PortalError.NO_REMOTE_API => $"Notification from '{reader}-{address}'\nUnable to upload reads to remote api as none is set up.",
                PortalError.STARTING_UP => $"Notification from '{reader}-{address}'\nTiming system is currently in startup mode.\nPlease wait to try to start the readers.",
                PortalError.INVALID_READ => $"Notification from '{reader}-{address}'\nUnable to add read.\nSpecified read is not valid.",
                PortalError.NOT_ALLOWED => $"Notification from '{reader}-{address}'\nUnable to set the time on the timing system while a reader is connected.",
                UPS_DISCONNECTED => $"Notification from '{reader}-{address}'\nUPS has been disconnected.",
                UPS_CONNECTED => $"Notification from '{reader}-{address}'\nUPS connection has been re-established.",
                UPS_ON_BATTERY => $"Notification from '{reader}-{address}'\nUPS is working from battery power.\nCheck to ensure the UPS is plugged in and the power source is working.",
                UPS_LOW_BATTERY => $"Notification from '{reader}-{address}'\nUPS battery is low. Shutdown imminent.",
                UPS_ONLINE => $"Notification from '{reader}-{address}'\nUPS is back on line power and no longer running off the UPS battery.",
                SHUTTING_DOWN => $"Notification from '{reader}-{address}'\nShutting down.",
                RESTARTING => $"Notification from '{reader}-{address}'\nRestarting.",
                HIGH_TEMP => $"Notification from '{reader}-{address}'\nTemperature is high.",
                MAX_TEMP => $"Notification from '{reader}-{address}'\nTemperature is very high. Throttling wil most likely occur.",
                BATTERY_LOW => $"Notification from '{reader}-{address}'\nBattery level is low.",
                BATTERY_CRITICAL => $"Notification from '{reader}-{address}'\nBattery level is critical.",
                _ => $"Notification from '{reader}-{address}'\nUnknown notification '{message.Type}'",
            };
        }

        public static string GetRemoteNotificationMessage(string type)
        {
            return type switch
            {
                PortalError.TOO_MANY_REMOTE_API => "Only one remote api can be assigned to a portal. Please remove or update the currently defined remote api.",
                PortalError.TOO_MANY_CONNECTIONS => "Unable to connect to the timing system, there are too many devices already connected.",
                PortalError.SERVER_ERROR => "The timing system encountered an error trying to process that request.",
                PortalError.DATABASE_ERROR => "The timing system encountered an error with their database while trying to process that request.",
                PortalError.INVALID_READER_TYPE => "Reader type specified is not valid.",
                PortalError.READER_CONNECTION => "Unable to connect to the reader.",
                PortalError.NOT_FOUND => "The timing system was not able to find the specified value.",
                PortalError.INVALID_SETTING => "One or more settings specified was not valid.",
                PortalError.INVALID_API_TYPE => "API type specified is not valid.",
                PortalError.ALREADY_SUBSCRIBED => "User is already subscribed to reads/sightings from the timing system.",
                PortalError.ALREADY_RUNNING => "Unable to start the specified reader as it is already running.",
                PortalError.NOT_RUNNING => "Unable to stop the specified reader as it is not currently running.",
                PortalError.NO_REMOTE_API => "Unable to upload reads to remote api as none is set up.",
                PortalError.STARTING_UP => "Timing system is currently in startup mode. Please wait to try to start the readers.",
                PortalError.INVALID_READ => "Unable to add read. Specified read is not valid.",
                PortalError.NOT_ALLOWED => "Unable to set the time on the timing system while a reader is connected.",
                UPS_DISCONNECTED => $"UPS has been disconnected.",
                UPS_CONNECTED => $"UPS connection has been re-established.",
                UPS_ON_BATTERY => $"UPS is working from battery power. Check to ensure the UPS is plugged in and the power source is working.",
                UPS_LOW_BATTERY => $"UPS battery is low. Shutdown imminent.",
                UPS_ONLINE => $"UPS is back on line power and no longer running off the UPS battery.",
                SHUTTING_DOWN => $"Shutting down.",
                RESTARTING => $"Restarting.",
                HIGH_TEMP => $"Temperature is high.",
                MAX_TEMP => $"Temperature is very high. Throttling wil most likely occur.",
                BATTERY_LOW => $"Battery level is low.",
                BATTERY_CRITICAL => $"Battery level is critical.",
                _ => $"Unknown notification. {type}",
            };
        }
    }
}
