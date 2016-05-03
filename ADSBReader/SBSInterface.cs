using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADSBReader
{
    public class SBSInterface : NetworkInterface
    {
        public SBSInterface(int port)
            : base("SBSInterface", port, true)
        { }
    }
}
