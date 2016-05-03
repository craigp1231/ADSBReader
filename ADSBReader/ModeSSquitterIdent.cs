using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADSBReader
{
    public class ModeSSquitterIdent : ModeSMessage
    {
        public AircraftEmitterCategorys AircraftCategory { get; private set; }
        public string Callsign { get; private set; }


        public ModeSSquitterIdent(byte[] ICAO, AircraftEmitterCategorys category, string callsign)
            : base(ICAO)
        {
            AircraftCategory = category;
            Callsign = callsign;
        }
    }
}
