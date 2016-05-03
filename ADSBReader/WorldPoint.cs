using System;

namespace ADSBReader
{
    public class WorldPoint
    {
        public float Latitude;
        public float Longitude;

        public WorldPoint(float lat, float lon)
        {
            this.Latitude = lat;
            this.Longitude = lon;
        }

        public override string ToString()
        {
            return string.Format("{0:f6},{1:f6}", Latitude, Longitude);
        }
    }
}
