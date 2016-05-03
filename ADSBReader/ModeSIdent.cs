using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADSBReader
{
    public class ModeSIdent : ModeSMessage
    {
        public string Ident { get; private set; }

        public ModeSIdent(byte[] ICAO, string ident)
            : base (ICAO)
        {
            this.Ident = ident;
        }
    }
}
