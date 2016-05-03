using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADSBReader
{
    public class ModeSCommBAlt : ModeSMessage
    {
        public int Altitude { get; private set; }

        public ModeSCommBAlt(byte[] ICAO, int alt)
            : base (ICAO)
        {
            this.Altitude = alt;
        }
    }
}
