using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ADSBReader
{
    public abstract class ModeSMessage
    {
        public byte[] ICAO;
        public string HexICAO;
        public bool CRCOk;
        public byte[] CRC;
        public DateTime Received {get; private set; }
        public ModeSMessage SecondaryMessage = null;

        public ModeSMessage(byte[] ICAO)
        {
            this.ICAO = ICAO;

            this.Received = DateTime.Now;

            if (ICAO.Length == 3)
                this.HexICAO = string.Format("{0:2X}{1:2X}{2:2X}", ICAO[0], ICAO[1], ICAO[2]);
        }
    }
}
