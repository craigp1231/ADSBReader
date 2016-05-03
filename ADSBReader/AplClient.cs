using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.IO;
using System.Web;
using System.Security.Cryptography.X509Certificates;

namespace ADSBReader
{
    public class AplClient : RadarClient
    {
        const int SERVER_PORT = 6807;

        //private SslStream sslClient;
        private NetworkStream sslClient;

        public AplClient(Socket socket)
        {
            mClientSocket = socket;

            //global::RadarServer.Properties.Resources.encr
            //byte[] ssc = Certificate.CreateSelfSignCertificatePfx("CN=home.craig-phillips.co.uk", DateTime.Now, DateTime.MaxValue);

            //X509Certificate cert = new X509Certificate2(global::RadarServer.Properties.Resources.RealRadarKey);

            NetworkStream NS = new NetworkStream(mClientSocket);
            sslClient = NS;
            //sslClient = new SslStream(NS);
            //sslClient.AuthenticateAsServer(cert, false, System.Security.Authentication.SslProtocols.Default, false);

            FireClientConnected(this);

            //FireConnectionInfo();

            WaitForData();
        }

        private void SendData(byte[] bytes)
        {
            if (!this.Connected) return;
            try
            {
                //byte[] bytes = System.Text.Encoding.ASCII.GetBytes(data);
                //if (mClientSocket != null) mClientSocket.BeginSend(bytes, 0, bytes.Length, SocketFlags.None, new AsyncCallback(SendCallback), mClientSocket);
                if (sslClient != null)
                    //sslClient.BeginWrite(bytes, 0, bytes.Length, new AsyncCallback(SendCallback), mClientSocket);
                    sslClient.Write(bytes, 0, bytes.Length);
            }
            catch (SocketException se)
            {
                string err = string.Format("Send failed: ({0}) {1}", se.ErrorCode, se.Message);
                //RaiseNetworkError(err);
                if ((se.ErrorCode == 10053) || (se.ErrorCode == 10054)) Disconnect();
            }
        }

        private void SendCallback(IAsyncResult iar)
        {
            try
            {
                Socket sock = (Socket)iar.AsyncState;
                int bytesSent = sock.EndSend(iar);
            }
            catch (ObjectDisposedException) { } // OK to swallow these ... just means the socket was closed.
        }

        private class SocketPacket
        {
            public NetworkStream mThisSocket;
            public byte[] mDataBuffer = new byte[1024];
        }

        private void WaitForData()
        {
            try
            {
                if (mIncomingDataCallBack == null) mIncomingDataCallBack = new AsyncCallback(OnDataReceived);
                SocketPacket theSockPkt = new SocketPacket();
                theSockPkt.mThisSocket = sslClient;

                // Start listening to the data asynchronously.
                if (sslClient == null) return;
                sslClient.BeginRead(theSockPkt.mDataBuffer, 0, theSockPkt.mDataBuffer.Length, mIncomingDataCallBack, theSockPkt);
                
                /*mClientSocket.BeginReceive(
                    theSockPkt.mDataBuffer,
                    0, theSockPkt.mDataBuffer.Length,
                    SocketFlags.None,
                    mIncomingDataCallBack,
                    theSockPkt
                );*/
            }
            catch (SocketException se)
            {
                string err = string.Format("BeginReceive failed: ({0}) {1}", se.ErrorCode, se.Message);
                //RaiseNetworkError(err);
            }
        }

        private void OnDataReceived(IAsyncResult asyn)
        {
            try
            {
                SocketPacket theSockId = (SocketPacket)asyn.AsyncState;
                //int bytesReceived = theSockId.mThisSocket.EndReceive(asyn);
                int bytesReceived = theSockId.mThisSocket.EndRead(asyn);
                if (bytesReceived == 0)
                {
                    Disconnect();
                    return;
                }
                byte[] b = new byte[bytesReceived];
                Array.Copy(theSockId.mDataBuffer, b, bytesReceived);
                ProcessData(b);
                WaitForData();
            }
            catch (ObjectDisposedException)
            {
                Disconnect();
            }
            catch (SocketException se)
            {
                string err = string.Format("EndReceive failed: ({0}) {1}", se.ErrorCode, se.Message);
                //RaiseNetworkError(err);
                Disconnect();
            }
        }

        private void ProcessData(byte[] data)
        {
            MemoryStream MS = new MemoryStream(data);
            BinaryReader BR = new BinaryReader(MS);

            while (MS.Position < MS.Length)
            {
                byte opcode = BR.ReadByte();

                switch (opcode)
                {
                    case 3:
                        string callsign = BR.ReadString();
                        FireFlightPlanRequest(callsign);
                        break;
                    case 4:
                        string devName = Uri.UnescapeDataString(BR.ReadString());
                        string devIdent = Uri.UnescapeDataString(BR.ReadString());
                        string appIdent = BR.ReadString();
                        string devID = BR.ReadString();

                        //device = ServerNetwork.GetDevice(devName, devIdent, appIdent, devID);

                        //FireConnectionInfo();
                        break;
                }
            }
        }

        public override void SendAircraft(Aircraft aircraft)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);

            try
            {
                // Aircraft opcode
                bw.Write((byte)1);

                //bw.Write(ByteStr(aircraft.Callsign, 10));
                bw.Write(aircraft.Callsign);
                bw.Write(aircraft.AircraftType);
                bw.Write(aircraft.AircraftReg);
                bw.Write(aircraft.HexAddress);
                bw.Write((float)aircraft.Position.Longitude);
                bw.Write((float)aircraft.Position.Latitude);
                bw.Write((ushort)aircraft.Altitude);
                bw.Write((ushort)aircraft.MCPAltitude);
                bw.Write((ushort)aircraft.Speed);
                bw.Write((ushort)aircraft.Heading);
                bw.Write(aircraft.Squawk);

                SendData(ms.ToArray());
            }
            catch { }

            bw.Close();
        }

        public override void SendFlightPlan(PPFlightPlan fp)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);

            // Aircraft opcode
            bw.Write((byte)2);

            bw.Write(fp.Callsign);
            bw.Write(fp.AircraftType);
            bw.Write(fp.WeightCategory);
            bw.Write(fp.Equipment);
            bw.Write(fp.SecondaryEquipment);
            bw.Write(fp.FlightRule);
            bw.Write(fp.FlightType);
            bw.Write(fp.DepartureICAO);
            bw.Write(fp.ArrivalICAO);
            bw.Write(fp.AlternateICAO);
            bw.Write(fp.DepartureName);
            bw.Write(fp.ArrivalName);
            bw.Write(fp.AlternateName);
            bw.Write(fp.OffBlockTimeInt);
            bw.Write(fp.EnrouteTime);
            bw.Write(fp.Route);
            bw.Write(fp.Remarks);

            SendData(ms.ToArray());

            bw.Close();
        }

    }
}
