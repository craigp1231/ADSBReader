using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using System.Net.Sockets;

using Metacraft.SimSuite.Network;
using Metacraft.SimSuite.Network.PDU;

namespace ADSBReader
{
    public class FSDClient : RadarClient
    {
        const int SERVER_PORT = 6809;

        private FSDSession session;

        public FSDClient(FSDSession s)
        {
            //mClientSocket = socket;
            //WaitForData();
            session = s;
            FireClientConnected(this);

            // Subscribe to events
            session.AddATCReceived += new EventHandler<DataReceivedEventArgs<PDUAddATC>>(session_AddATCReceived);
            session.ClientQueryReceived += new EventHandler<DataReceivedEventArgs<PDUClientQuery>>(session_ClientQueryReceived);
            session.NetworkDisconnected += new EventHandler<NetworkEventArgs>(session_NetworkDisconnected);
            session.ClientIdentificationReceived += new EventHandler<DataReceivedEventArgs<PDUClientIdentification>>(session_ClientIdentificationReceived);

            session.IsReadyForData();

            //Console.WriteLine("Events setup");
        }

        public override void SendAircraft(Aircraft aircraft)
        {
            int sqk = aircraft.Squawk == null ? 0 : int.Parse(aircraft.Squawk);

            // Send Aircraft position
            PDUPilotPosition PP = new PDUPilotPosition(aircraft.Callsign, sqk, true, false,
                NetworkRating.OBS, aircraft.Position.Latitude, aircraft.Position.Longitude, aircraft.Altitude, aircraft.Altitude, (int)aircraft.Speed, 0, 0, (int)aircraft.Heading);

            session.SendPDU(PP);

            // Send aircraft reg/type

            PDUClientQueryResponse res = new PDUClientQueryResponse(aircraft.Callsign, "CLIENT", ClientQueryType.RealName, new List<string>() { aircraft.AircraftType + " " + aircraft.AircraftReg, "", "1" });
            session.SendPDU(res);

            if (aircraft.MCPAltitude > 0)
            {
                PDUSharedState ss = new PDUSharedState("SERVER", "CLIENT", SharedStateType.TempAlt, aircraft.Callsign, aircraft.MCPAltitude.ToString());
                session.SendPDU(ss);
            }
            
        }

        public override void SendFlightPlan(PPFlightPlan flightplan)
        {
            Metacraft.SimSuite.Network.FlightRules fr = FlightRules.IFR;
            switch (flightplan.FlightRule)
            {
                case "S":
                    fr = FlightRules.SVFR;
                    break;
                case "V":
                    fr = FlightRules.VFR;
                    break;
            }

            string actype = flightplan.AircraftType + "/Q";
            if (flightplan.WeightCategory == "H") actype = "H/" + actype;

            Regex r = new Regex("([NKM])(\\d{3,4})([FASM])(\\d{3,4})");
            Match m = r.Match(flightplan.Route);

            string tas = "450";
            string cruisealt = "FL350";

            if (m.Success)
            {
                tas = m.Groups[2].Value;
                cruisealt = "FL" + m.Groups[4].Value;
            }

            string deptime = string.Format("{0:00}{1:00}", flightplan.OffBlockTime.Hour, flightplan.OffBlockTime.Minute);

            string hrs = string.Format("{0:00}", (int)(flightplan.EnrouteTime / 60));
            string mins = string.Format("{0:00}", flightplan.EnrouteTime % 60);

            // Convert to PDU flight plan
            PDUFlightPlan fpl = new PDUFlightPlan(flightplan.Callsign, "CLIENT", fr, actype,
                tas, flightplan.DepartureICAO, deptime, deptime, cruisealt, flightplan.ArrivalICAO, hrs, mins, hrs, mins, flightplan.AlternateICAO, flightplan.Remarks + " /v/", flightplan.Route);

            session.SendPDU(fpl);
        }

        void session_ClientIdentificationReceived(object sender, DataReceivedEventArgs<PDUClientIdentification> e)
        {
            string devsoftware = e.PDU.ClientName;
            string devserial = string.Format("{0:X}", (uint)int.Parse(e.PDU.SysUID));
        }

        void session_AddATCReceived(object sender, DataReceivedEventArgs<PDUAddATC> e)
        {
            //Console.WriteLine("ATC Received");

            // ATC logon
            PDUAddATC AA = e.PDU;

            session.Tag = AA.From;

            /*if (mConnections.ContainsKey(AA.From))
            {
                // To ensure the other connected client is still connected after this one disconnects
                sess.Tag = "DUPLICATE";

                PDUKillRequest kill = new PDUKillRequest("SERVER", AA.From, "Callsign already exists");
                sess.SendPDU(kill);

                return;
            }*/

            PDUTextMessage tm = new PDUTextMessage("SERVER", AA.From, "FlightRadar24 FSD Server");
            session.SendPDU(tm);
        }

        void session_ClientQueryReceived(object sender, DataReceivedEventArgs<PDUClientQuery> e)
        {
            // The client is requesting some data
            PDUClientQuery query = e.PDU;

            string callsign;

            switch (query.QueryType)
            {
                case ClientQueryType.FlightPlan:
                    callsign = query.Payload[0];
                    FireFlightPlanRequest(callsign);
                    break;
                /*case ClientQueryType.RealName:
                    callsign = query.To;
                    break;*/
            }
        }

        void session_NetworkDisconnected(object sender, NetworkEventArgs e)
        {
            FireClientDisconnected(this);
        }
    }
}
