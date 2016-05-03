using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace ADSBReader
{
    public abstract class RadarClient
    {
        public delegate void FlightPlanDelegate(string callsign);
        public static event FlightPlanDelegate RequestFlightPlan;

        public delegate void ClientDelegate(RadarClient client);
        public static event ClientDelegate ClientConnected;
        public static event ClientDelegate ClientDisconnected;

        #region Instance Properties

        protected Socket mClientSocket;
        protected AsyncCallback mIncomingDataCallBack;
        protected string mPartialPacket = "";


        public string Tag { get; set; }

        public IPAddress ClientIPAddress
        {
            get
            {
                if (mClientSocket == null)
                    return IPAddress.None;

                if (!mClientSocket.Connected)
                    return IPAddress.None;

                IPEndPoint ep = (IPEndPoint)mClientSocket.RemoteEndPoint;

                return ep.Address;
            }
        }

        public bool Connected
        {
            get
            {
                return (mClientSocket == null) ? false : mClientSocket.Connected;
            }
        }

        #endregion

        #region Static Listener Methods

        protected static void FireClientConnected(RadarClient client)
        {
            if (ClientConnected != null) ClientConnected(client);
        }

        protected static void FireClientDisconnected(RadarClient client)
        {
            if (ClientDisconnected != null) ClientDisconnected(client);
        }

        protected static void FireFlightPlanRequest(string callsign)
        {
            if (RequestFlightPlan != null) RequestFlightPlan(callsign);
        }

        #endregion

        #region Instance Methods
        public void Disconnect()
        {
            if (mClientSocket != null)
            {
                try
                {
                    mClientSocket.Shutdown(SocketShutdown.Receive);
                    mClientSocket.Close();
                }
                catch (ObjectDisposedException) { }
                catch (SocketException) { }
                mClientSocket = null;
                FireClientDisconnected(this);
            }
        }

        public virtual void SendAircraft(Aircraft aircraft)
        {

        }

        public virtual void SendFlightPlan(PPFlightPlan fp)
        {

        }
        #endregion
    }
}
