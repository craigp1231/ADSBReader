using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADSBReader
{
    public class ModeSEmergencyState : ModeSMessage
    {
        public EmergencyStateEnum EmergencyState { get; private set; }
        public ushort Squawk { get; private set; }

        public ModeSEmergencyState(byte[] ICAO, EmergencyStateEnum state, ushort squawk)
            : base(ICAO)
        {
            this.EmergencyState = state;
            this.Squawk = squawk;
        }
    }
}
