using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADSBReader
{
    public class ModeSAirbornePosition : ModeSMessage
    {
        public SurveillanceStatusCategory Surveillance { get; private set; }
        public int Altitude { get; private set; }
        public bool TimeUTC { get; private set; }
        public CPRType cPRType { get; private set; }
        public int RawLat { get; private set; }
        public int RawLon { get; private set; }

        public ModeSAirbornePosition(byte[] ICAO, SurveillanceStatusCategory surveillance, int altitude, bool timeUTC, CPRType cprType, int rawLat, int rawLon)
            : base(ICAO)
        {
            this.Surveillance = surveillance;
            this.Altitude = altitude;
            this.TimeUTC = timeUTC;
            this.cPRType = cprType;
            this.RawLat = rawLat;
            this.RawLon = rawLon;
        }
    }
}
