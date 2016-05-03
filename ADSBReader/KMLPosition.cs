using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADSBReader
{
    public class KMLPosition
    {
        public float Latitude;
        public float Longitude;
        public float Altitude;

        public KMLPosition(float lat, float lon, float alt)
        {
            this.Latitude = lat;
            this.Longitude = lon;
            this.Altitude = alt / 3.2808f;
        }

        public string ToString(bool includeAltitude)
        {
            return string.Format("{0},{1},{2}", Longitude, Latitude, includeAltitude ? Altitude : 0);
        }
    }
}
