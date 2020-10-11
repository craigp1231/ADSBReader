using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADSBReader
{
    public class ModeSMessageConsumer
    {
        public ConcurrentDictionary<string, Aircraft> Aircraft = new ConcurrentDictionary<string, Aircraft>();

        public delegate void AircraftPositionUpdatedDelegate(Aircraft aircraft);
        public event AircraftPositionUpdatedDelegate AircraftPositionUpdated;

        public void ConsumeMessage(ModeSMessage msg)
        {
            Aircraft a = GetAircraft(msg.ICAO);

            if (msg is ModeSSquitterIdent)
            {
                ModeSSquitterIdent mmsg = (ModeSSquitterIdent)msg;
                a.Callsign = mmsg.Callsign;
                a.AircraftCategory = mmsg.AircraftCategory;
                a.MessageCount++;
            }
            else if (msg is ModeSAirbornePosition)
            {
                ModeSAirbornePosition mmsg = (ModeSAirbornePosition)msg;
                a.Altitude = mmsg.Altitude;
                a.AddPoint(mmsg.cPRType, mmsg.RawLat, mmsg.RawLon);
                a.MessageCount++;

                if (AircraftPositionUpdated != null)
                    AircraftPositionUpdated(a);
            }
            else if (msg is ModeSAirspeed)
            {
                ModeSAirspeed mmsg = (ModeSAirspeed)msg;
                a.Speed = mmsg.Airspeed;
                a.Heading = mmsg.Heading;
                a.VerticalSpeed = mmsg.VerticalSpeed;
                a.MessageCount++;
            }
            else if (msg is ModeSGroundSpeed)
            {
                ModeSGroundSpeed mmsg = (ModeSGroundSpeed)msg;
                a.Speed = mmsg.GroundSpeed;
                a.Heading = mmsg.Track;
                a.VerticalSpeed = mmsg.VerticalSpeed;
                a.MessageCount++;
            }
            else if (msg is ModeSCommBAlt)
            {
                ModeSCommBAlt mmsg = (ModeSCommBAlt)msg;

                if (mmsg.Altitude > 0)
                    a.Altitude = mmsg.Altitude;

                a.MessageCount++;
            }
            else if (msg is ModeSIdent)
            {
                ModeSIdent mmsg = (ModeSIdent)msg;
                a.Squawk = mmsg.Ident;
                a.MessageCount++;
            }
            else if (msg is ModeSTargetState)
            {
                ModeSTargetState mmsg = (ModeSTargetState)msg;

                if (mmsg.SelectedAltitude != null)
                    a.MCPAltitude = (ushort)mmsg.SelectedAltitude;
            }
            else if (msg is ModeSIAS)
            {
                ModeSIAS mmsg = (ModeSIAS)msg;
                a.IAS = mmsg.IAS;
            }

            // Process secondary
            if (msg.SecondaryMessage != null)
                ConsumeMessage(msg.SecondaryMessage);
        }

        private Aircraft GetAircraft(byte[] ICAOAddress)
        {
            if (ICAOAddress.Length != 3) return null;

            string HexAddress = string.Format("{0:X2}{1:X2}{2:X2}", ICAOAddress[0], ICAOAddress[1], ICAOAddress[2]);

            if (Aircraft.ContainsKey(HexAddress))
                return Aircraft[HexAddress];
            else
            {
                Aircraft a = new Aircraft(ICAOAddress);
                Aircraft.GetOrAdd(HexAddress, a);
                return a;
            }
        }
    }
}
