using System;
using System.IO;
using System.Threading;

namespace ADSBReader
{
    //public delegate void MagnitudeReceivedDelegate (short mag);

    public unsafe class RTLReader
    {
        public event SamplesAvailableDelegate SamplesAvailable;

        private Thread readerThread;
        private string fPath;

        private void StartReading()
        {
            if (!File.Exists(fPath)) return;

            FileStream fs = new FileStream(fPath, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fs);

            UnsafeBuffer ub = UnsafeBuffer.Create(131072, sizeof(Complex));
            Complex* cb = (Complex*)ub;


            long sampleCount = 0;

            try
            {
                for (int i = 0; i <= fs.Length / 2; i++)
                {
                    int ImagineNum = br.ReadByte() - 127;
                    int RealNum = br.ReadByte() - 127;

                    if (ImagineNum < 0) ImagineNum = -ImagineNum;
                    if (RealNum < 0) RealNum = -RealNum;

                    cb[sampleCount].Imag = ImagineNum;
                    cb[sampleCount].Real = RealNum;

                    sampleCount++;

                    if (sampleCount > 131072)
                    {
                        SamplesAvailableEventArgs args = new SamplesAvailableEventArgs();
                        args.Buffer = cb;
                        args.Length = 131072;
                        SamplesAvailable(this, args);
                        sampleCount = 0;
                    }
                }
            }
            catch
            {

            }

            br.Close();

            if (sampleCount > 0)
            {
                SamplesAvailableEventArgs args = new SamplesAvailableEventArgs();
                args.Buffer = cb;
                args.Length = (int)sampleCount;
                SamplesAvailable(this, args);
                sampleCount = 0;
            }
        }

        public void ReadFromFile(string FilePath)
        {
            fPath = FilePath;
            readerThread = new Thread(new ThreadStart(StartReading));
            readerThread.Start();
        }
    }
}
