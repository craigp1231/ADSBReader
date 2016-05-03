using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADSBReader
{
    public class AdsbRawNet : NetworkInterface
    {
        public event AdsbBinaryReader.FrameAvailableDelegate FrameReceived;

        private string leftover = string.Empty;

        public AdsbRawNet() : base("Adsb", 30004, false)
        {

        }

        protected override void ProcessIncoming(string data)
        {
            data = leftover + data;
            leftover = string.Empty;
            string[] lines = data.Split(new string[] {"\r\n"}, StringSplitOptions.RemoveEmptyEntries);

            foreach(string l in lines)
            {
                if (!l.Contains('*') || !l.Contains(";"))
                {
                    leftover += l;
                    continue;
                }

                string line = l.Replace("*", "").Replace(";", "");

                byte[] frame = new byte[line.Length / 2];

                if (line.Length / 2 < 7) continue;

                for (int i = 0; i < line.Length / 2; i++)
                {
                    string HexChar = line.Substring(i * 2, 2);

                    byte b = Byte.Parse(HexChar, System.Globalization.NumberStyles.HexNumber);

                    frame[i] = b;
                }

                if (FrameReceived != null)
                    FrameReceived(frame);
            }

            base.ProcessIncoming(data);
        }
    }
}
