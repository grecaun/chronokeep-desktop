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

using System.Threading;
using Chronokeep.Helpers;

namespace Chronokeep.UI.UhfRfidReader
{
    internal class NewReader(ChipReaderWindow chipReaderWindow)
    {
        private const int Delay = 500;
        private bool keepAlive;
        private int counter = 1;
        private RfidSerial? serial;

        public void SetSerial(RfidSerial iSerial)
        {
            serial = iSerial;
        }

        public void Run()
        {
            keepAlive = serial != null;
            while (keepAlive)
            {
                counter++;
                RfidInfo read = serial!.ReadData();
                if (read.ErrorCode == RfidError.NO_ERROR)
                {
                    chipReaderWindow.AddRfidItem(read);
                }
                Thread.Sleep(Delay);
            }
            Log.D("UI.UhfRfidReader.NewReader", $"InActive - Finished after {counter} loops.");
        }

        public void Kill()
        {
            Log.D("UI.UhfRfidReader.NewReader", "Kill command received.");
            keepAlive = false;
        }
    }
}

