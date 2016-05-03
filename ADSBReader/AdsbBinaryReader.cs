using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace ADSBReader
{
    public class AdsbBinaryReader
    {
        public delegate void FrameAvailableDelegate(byte[] frame);
        public delegate void NullDelegate();

        public event FrameAvailableDelegate FrameAvailable;
        public event NullDelegate FinishedReading;

        private Thread readerThread;
        private string fPath;

        private void StartReading()
        {
            if (!File.Exists(fPath)) return;

            FileStream fs = new FileStream(fPath, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fs);

            byte[] shortFrame = new byte[7];
            byte[] longFrame = new byte[14];
            int frameType;
            int bitLength;

            if (FrameAvailable == null) return;

            while (br.Read(shortFrame, 0, 7) != 0)
            {
                frameType = shortFrame[0] >> 3;
                bitLength = AdsbBitDecoder.modesMessageLenByType(frameType);

                if (bitLength == 112)
                {
                    // Read next 7 bytes and attach to these
                    Array.Copy(shortFrame, longFrame, shortFrame.Length);

                    // Something fucked up if we hit this
                    if (br.Read(shortFrame, 0, 7) != 7) continue;

                    // Copy the next 7 bits to the end of the long frame
                    Array.Copy(shortFrame, 0, longFrame, 7, shortFrame.Length);

                    FrameAvailable(longFrame);
                }
                else if (bitLength == 56)
                {
                    FrameAvailable(shortFrame);
                }

            }

            br.Close();

            if (FinishedReading != null)
                FinishedReading();
        }

        public void ReadFromFile(string FilePath)
        {
            fPath = FilePath;
            readerThread = new Thread(new ThreadStart(StartReading));
            readerThread.Start();
        }
    }
}
