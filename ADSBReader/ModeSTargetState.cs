using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADSBReader
{
    public class ModeSTargetState : ModeSMessage
    {
        public ushort? SelectedAltitude { get; private set; }
        public ushort? SelectedPressure { get; private set; }
        public ushort? SelectedHeading { get; private set; }

        public ModeSTargetState(byte[] ICAO, ushort? alt, ushort? pressure, ushort? heading)
            : base (ICAO)
        {
            this.SelectedAltitude = alt;
            this.SelectedPressure = pressure;
            this.SelectedHeading = heading;
        }
    }
}
