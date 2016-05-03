using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADSBReader
{
    public class ModeSAirspeed : ModeSMessage
    {
        public ushort Heading { get; private set; }
        public ushort Airspeed { get; private set; }
        public short VerticalSpeed { get; private set; }

        public ModeSAirspeed(byte[] ICAO, ushort heading, ushort airspeed, short verticalspeed)
            : base (ICAO)
        {
            this.Heading = heading;
            this.Airspeed = airspeed;
            this.VerticalSpeed = verticalspeed;
        }
    }
}
