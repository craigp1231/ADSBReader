using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;

using Metacraft.SimSuite.Network;

namespace ADSBReader
{
    public class ClientListener
    {
        private TcpListener mListenerSocket;
        private bool mIsListening;
        private int mPort;

        public delegate void ClientDelegate(Socket sck);
        private ClientType mType;

        public enum ClientType
        {
            FSD,
            APL
        }

        #region Listener Methods

        public ClientListener(int port, ClientType cType)
        {
            mPort = port;
            mType = cType;

            StartListening();
        }

        public ClientListener(int port, ClientType cType, bool bStartListening)
        {
            mPort = port;
            mType = cType;
            if (bStartListening)
                StartListening();
        }

        public void StartListening()
        {
            if (mIsListening) return;

            mListenerSocket = new TcpListener(IPAddress.Any, mPort);
            mListenerSocket.Start();
            mListenerSocket.BeginAcceptSocket(new AsyncCallback(AcceptCallback), mListenerSocket);

            mIsListening = true;
        }

        /*public static void StartListening()
        {
            Listen(SERVER_PORT);
        }*/

        public void StopListening()
        {
            try
            {
                mListenerSocket.Stop();
            }
            catch
            {

            }

            mIsListening = false;
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                Socket sck = mListenerSocket.EndAcceptSocket(ar);
                sck.LingerState = new LingerOption(true, 5);

                /*AplClient sess = new AplClient(sck);

                if (NewClientConnected != null) NewClientConnected(sess);*/
                CreateClient(sck);

                mListenerSocket.BeginAcceptSocket(new AsyncCallback(AcceptCallback), mListenerSocket);
            }
            catch
            {

            }
        }

        private void CreateClient(Socket sck)
        {
            switch (mType)
            {
                case ClientType.APL:
                    new AplClient(sck);
                    break;
                case ClientType.FSD:
                    new FSDClient(new FSDSession(sck));
                    break;
            }
        }

        #endregion
    }
}
