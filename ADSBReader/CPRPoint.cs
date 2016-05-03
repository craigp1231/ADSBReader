using System;

namespace ADSBReader
{
    public class CPRPoint
    {
        public int RawLat { get; private set; }
        public int RawLon { get; private set; }
        public DateTime Date { get; private set; }

        public CPRPoint(int lat, int lon)
        {
            this.RawLat = lat;
            this.RawLon = lon;
            this.Date = DateTime.Now;
        }
    }
}
