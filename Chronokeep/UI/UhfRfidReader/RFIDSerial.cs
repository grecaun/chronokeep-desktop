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
using System.IO.Ports;
using System.Threading;

namespace Chronokeep.UI.UhfRfidReader
{
    internal class RfidSerial(string comPort, int baudRate)
    {
        private SerialPort port = new();

        private RfidError DeviceInit()
        {
            if (comPort == "N/A" || baudRate == 0)
            {
                return RfidError.BAD_SETTINGS;
            }
            try
            {
                port = new SerialPort(comPort, baudRate)
                {
                    ReadTimeout = 500,
                    WriteTimeout = 500
                };
            }
            catch (IOException exc)
            {
                Console.WriteLine(exc.StackTrace);
                return RfidError.UNABLE_TO_CONNECT;
            }
            return RfidError.NO_ERROR;
        }

        private RfidError DeviceConnect()
        {
            try
            {
                port.Open();
                byte[] outMsg = [0xA0, 0x03, 0x50, 0x00, 0x0D];
                byte[] inMsg = new byte[56];
                outMsg[4] = CheckSum(outMsg, 4);
                port.BaseStream.Write(outMsg, 0, 5);
                Thread.Sleep(20); // Need to give it time, or we won't get the whole message.
                int received = port.Read(inMsg, 0, 56);
                while (received < 6)
                {
                    Thread.Sleep(20);
                    received = port.Read(inMsg, received, 56 - received);
                }
                if (inMsg[0] != 0xE4 || inMsg[inMsg[1] + 1] != CheckSum(inMsg, inMsg[1] + 1) || inMsg[1] < 0x04 || inMsg[4] != 0x00)
                {
                    return RfidError.UNABLE_TO_CONNECT;
                }
            }
            catch
            {
                return RfidError.UNABLE_TO_CONNECT;
            }
            return RfidError.NO_ERROR;
        }

        private void DeviceDisconnect()
        {
            port.Close();
        }

        private static void DeviceDeinit() { }

        public RfidError Connect()
        {
            RfidError err = DeviceInit();
            return err != RfidError.NO_ERROR ? err : DeviceConnect();
        }

        public void Disconnect()
        {
            DeviceDisconnect();
            DeviceDeinit();
        }

        internal static byte CheckSum(byte[] buffer, int buffLen)
        {
            byte sum = 0;
            for (int i = 0; i < buffLen; i++)
            {
                sum += buffer[i];
            }
            int bit = ~sum;
            sum = (byte)bit;
            sum += 1;
            return sum;
        }

        public RfidInfo ReadData()
        {
            byte[] outMsg = [0xA0, 0x03, 0x82, 0x00, 0xDB];
            byte[] inMsg = new byte[256];
            try
            {
                port.BaseStream.Write(outMsg, 0, 5);
                Thread.Sleep(50); // Need to give it time, or we won't get the whole message.
                int received = port.Read(inMsg, 0, 256);
                int pos = 0;
                while (pos < received && inMsg[pos] != 0xE4 && inMsg[pos] != 0xE0)
                {
                    pos++;
                }
                switch (pos)
                {
                    case > 0 and < 256:
                    {
                        for (int i = 0; i < 256 - pos; i++)
                        {
                            inMsg[i] = inMsg[i + pos];
                        }

                        break;
                    }
                    case > 255:
                        return new RfidInfo
                        {
                            ErrorCode = RfidError.NO_DATA
                        };
                }
                return new RfidInfo(inMsg);
            }
            catch
            {
                return new RfidInfo
                {
                    ErrorCode = RfidError.CONNECTION_ERROR
                };
            }
        }

    }
    public class RfidInfo
    {
        public long DecNumber { get; set; }
        public int DeviceNumber { get; set; }
        public int AntennaNumber { get; set; }
        public string HexNumber { get; set; } = "";
        private byte[] Data { get; }
        public string DataRep => BitConverter.ToString(Data);
        public int ReadNumber { get; set; }
        public RfidError ErrorCode { get; init; }

        public RfidInfo() => Data = [0x00];

        public RfidInfo(byte[] inData)
        {
            ErrorCode = RfidError.NO_ERROR;
            Data = new byte[inData[1] + 2];
            for (int i = 0; i < this.Data.Length; i++)
            {
                Data[i] = inData[i];
            }
            if (Data[^1] != RfidSerial.CheckSum(Data, Data.Length - 1))
            {
                ErrorCode = RfidError.BAD_DATA;
            }
            switch (Data.Length)
            {
                case 18:
                {
                    HexNumber = BitConverter.ToString(Data, 5, 12);
                    byte[] epc = new byte[8];
                    for (int i = 0; i < 8; i++)
                    {
                        epc[i] = inData[16 - i];
                    }
                    DecNumber = BitConverter.ToInt64(epc, 0);
                    DeviceNumber = inData[3];
                    AntennaNumber = inData[4];
                    break;
                }
                case 6:
                    ErrorCode = RfidError.NO_DATA;
                    break;
                default:
                    ErrorCode = RfidError.BAD_DATA;
                    break;
            }
        }
    }

    public enum RfidError
    {
        UNABLE_TO_CONNECT, NO_ERROR, UNKNOWN_ERROR, BAD_SETTINGS, NO_DATA, BAD_DATA, CONNECTION_ERROR
    };
}

