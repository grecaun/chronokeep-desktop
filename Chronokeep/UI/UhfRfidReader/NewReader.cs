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
                if (read.ErrorCode == RfidError.NOERR)
                {
                    chipReaderWindow.AddRfidItem(read);
                }
                Thread.Sleep(Delay);
            }
            Log.D("UI.UhfRfidReader.NewReader", "InActive - Finished after " + counter + @" loops.");
        }

        public void Kill()
        {
            Log.D("UI.UhfRfidReader.NewReader", "Kill command received.");
            keepAlive = false;
        }
    }
}
