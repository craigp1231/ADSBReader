using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADSBReader
{
    public class ModeSIAS : ModeSMessage
    {
        public ushort IAS { get; private set; }

        public ModeSIAS(byte[] ICAO, ushort IAS)
            : base (ICAO)
        {
            this.IAS = IAS;
        }
    }
}
