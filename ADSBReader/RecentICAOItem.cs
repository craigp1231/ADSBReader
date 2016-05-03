using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ADSBReader
{
    public class RecentICAOItem
    {
        byte[] ICAO = new byte[3];
        public DateTime LastSeen { get; private set; }

        public int ICAOInt { get; private set; }

        public RecentICAOItem(byte[] mICAO)
        {
            this.ICAO = mICAO;
            this.LastSeen = DateTime.Now;
            this.ICAOInt = (mICAO[0] << 16) | (mICAO[1] << 8) | mICAO[2];
        }

        public void UpdateLastSeen()
        {
            this.LastSeen = DateTime.Now;
        }
    }
}
