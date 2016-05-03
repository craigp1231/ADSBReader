using System;
using System.Collections.Generic;
//using System.Collections.Concurrent;
using System.Linq;
using System.Text;

namespace ADSBReader
{
    public static class ServerNetwork
    {
        //private static ConcurrentBag<RadarClient> mConnections = new ConcurrentBag<RadarClient>();
        private static List<RadarClient> mConnections = new List<RadarClient>();

        public delegate void FlightPlanRequestDelegate(string callsign);
        public delegate void ClientEventDelegate(RadarClient client);

        public static event FlightPlanRequestDelegate FlightPlanRequested;

        public static event ClientEventDelegate ClientConnected;
        public static event ClientEventDelegate ClientDisconnected;
        public static event ClientEventDelegate ClientChanged;

        private static List<ClientListener> listeners;

        //private static RadarServerDBEntities rse = new RadarServerDBEntities();

        public static int ConnectionsCount
        {
            get
            {
                lock (mConnections)
                {
                    /*return (from rc in mConnections
                            where rc.Authenticated
                            select rc).Count();*/
                    return mConnections.Count;
                }
                /*lock (mConnections)
                {
                    return mConnections.Count;
                }*/
            }
        }

        public static void Start()
        {
            listeners = new List<ClientListener>();

            // Ensure that no connections are open
            /*var openconns = from c in rse.Connections
                            where c.CloseDate == null
                            select c;
            foreach (Connection c in openconns)
            {
                c.CloseDate = DateTime.Now;
            }
            rse.SaveChanges();*/
            // Display needs to be refreshed

            listeners.Add(new ClientListener(6809, ClientListener.ClientType.FSD));
            listeners.Add(new ClientListener(6807, ClientListener.ClientType.APL));

            FSDClient.ClientConnected += new RadarClient.ClientDelegate(ClientConnectedCB);
            FSDClient.ClientDisconnected += new RadarClient.ClientDelegate(ClientDisconnectedCB);
            FSDClient.RequestFlightPlan += new RadarClient.FlightPlanDelegate(RequestFlightPlanCB);

            AplClient.ClientConnected += new RadarClient.ClientDelegate(ClientConnectedCB);
            AplClient.ClientDisconnected += new RadarClient.ClientDelegate(ClientDisconnectedCB);
            AplClient.RequestFlightPlan += new RadarClient.FlightPlanDelegate(RequestFlightPlanCB);
        }

        public static void Stop()
        {
            foreach (ClientListener l in listeners)
                l.StopListening();

            listeners.Clear();
        }

        private static void RequestFlightPlanCB(string callsign)
        {
            if (FlightPlanRequested != null) FlightPlanRequested(callsign);
        }

        private static void ClientDisconnectedCB(RadarClient client)
        {
            lock (mConnections)
            {
                bool rem = mConnections.Remove(client);
            }

            if (ClientDisconnected != null) ClientDisconnected(client);
        }

        private static void ClientConnectedCB(RadarClient client)
        {
            lock (mConnections)
            {
                mConnections.Add(client);
            }
            if (ClientConnected != null) ClientConnected(client);
        }

        public static void FlightPlanReceived(PPFlightPlan fp)
        {
            lock (mConnections)
            {
                foreach (RadarClient rc in mConnections)
                {
                    rc.SendFlightPlan(fp);
                }
            }
        }

        public static void ReceivedAircraft(Aircraft aircraft)
        {
            lock (mConnections)
            {
                foreach (RadarClient rc in mConnections)
                {
                    rc.SendAircraft(aircraft);
                }
            }
        }
    }
}
