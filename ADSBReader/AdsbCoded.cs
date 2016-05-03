using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace ADSBReader
{
    public delegate void FrameReceivedDelegate(byte[] frame, int actualLength);

    public class AdsbCoded
    {
        public event FrameReceivedDelegate FrameReceived;

        public void ReadFile(string path)
        {
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs);
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                byte[] frame = new byte[14];

                line = line.Replace("*", "").Replace(";", "");

                if (line.Length < 28) continue;

                for(int i = 0; i < 14; i++)
                {
                    string HexChar = line.Substring(i * 2, 2);

                    byte b = Byte.Parse(HexChar, System.Globalization.NumberStyles.HexNumber);

                    frame[i] = b;
                }

                if (FrameReceived != null)
                    FrameReceived(frame, frame.Length);
            }

            sr.Close();
        }
    }
}
