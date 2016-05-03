using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADSBReader
{
    public class ModeSGroundSpeed : ModeSMessage
    {
        public ushort Track { get; private set; }
        public ushort GroundSpeed { get; private set; }
        public short VerticalSpeed { get; private set; }

        public ModeSGroundSpeed(byte[] ICAO, ushort track, ushort groundspeed, short verticalspeed)
            : base (ICAO)
        {
            this.Track = track;
            this.GroundSpeed = groundspeed;
            this.VerticalSpeed = verticalspeed;
        }
    }
}
