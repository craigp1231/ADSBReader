using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace ADSBReader
{
    public class NetworkInterface
    {
        private TcpListener listener;
        private NetworkClient client;

        public bool IsConnected {get; private set;}

        /*private string nHost;
        private int nPort;*/
        private string nClientName;
        private int nPort;

        public event EventHandler<MessageEventArgs> MessageDebug;
        public event EventHandler<EventArgs> OnConnected;
        public event EventHandler<EventArgs> OnDisconnected;

        public NetworkInterface(string clientname, int port, bool Listen)
        {
            nClientName = clientname;
            nPort = port;

            // Start listening on Port
            if (Listen)
            {
                try
                {
                    listener = new TcpListener(IPAddress.Any, port);
                    listener.Start();
                    listener.BeginAcceptTcpClient(AcceptTcpClientCallback, null);
                }
                catch
                {
                    Debug(string.Format("Cannot start listening for clients {0}", clientname));
                }
            }
        }

        public void Start(string host)
        {
            try
            {
                /*nhost = host;
                nPort = port;*/

                IsConnected = false;
                
                TcpClient c = new TcpClient();
                //c.Connect(host, port);
                c.BeginConnect(host, nPort, ConnectCallback, c);

                

                /*listener = new TcpListener(IPAddress.Any, nPort);
                listener.Start();
                listener.BeginAcceptTcpClient(AcceptTcpClientCallback, null);*/

                //Debug(string.Format("Network Connected on Port {0}", port));

                
            }
            catch
            {
                Debug(string.Format("Cannot Connect to {0}", nClientName));
            }
        }

        public void Stop()
        {
            try
            {
                if (IsConnected)
                {
                    client.TcpClient.Close();
                }
                listener.Stop();
            }
            catch { }
        }

        protected virtual void Debug(string message)
        {
            MessageDebug.SafeInvoke(this, new MessageEventArgs(message));
        }

        protected virtual void Connected()
        {
            IsConnected = true;
            OnConnected.SafeInvoke(this, new EventArgs());
        }

        protected virtual void Disconnected()
        {
            IsConnected = false;
            OnDisconnected.SafeInvoke(this, new EventArgs());
        }

        protected virtual void ProcessIncoming(string data)
        { }

        protected virtual void ProcessIncoming(byte[] data)
        { }

        private void ConnectCallback(IAsyncResult result)
        {
            try
            {
                TcpClient c = (TcpClient)result.AsyncState;
                c.EndConnect(result);

                byte[] buffer = new byte[c.ReceiveBufferSize];
                client = new NetworkClient(c, buffer);

                NetworkStream networkStream = client.NetworkStream;
                networkStream.BeginRead(client.Buffer, 0, client.Buffer.Length, ReadCallback, client);

                Connected();

                //Write("bang\r\n");
            }
            catch
            {
                Debug(string.Format("Cannot Connect to {0}", nClientName));
            }
        }

        private void AcceptTcpClientCallback(IAsyncResult result)
        {
            try
            {
                TcpClient tcpClient = listener.EndAcceptTcpClient(result);
                byte[] buffer = new byte[tcpClient.ReceiveBufferSize];
                client = new NetworkClient(tcpClient, buffer);

                NetworkStream networkStream = client.NetworkStream;
                networkStream.BeginRead(client.Buffer, 0, client.Buffer.Length, ReadCallback, client);

                //listener.Stop();
                // Multiple connections
                listener.BeginAcceptTcpClient(AcceptTcpClientCallback, null);

                Debug(string.Format("Accepted Client Connected to {0}", nClientName));

                Connected();
            }
            catch { }
        }

        protected void Write(string data)
        {
            Write(Encoding.ASCII.GetBytes(data));
        }

        protected void Write(byte[] bytes)
        {
            try
            {
                if (client != null && client.TcpClient.Connected && client.NetworkStream.CanWrite)
                {
                    client.NetworkStream.BeginWrite(bytes, 0, bytes.Length, WriteCallback, client);
                }
            }
            catch
            {
                Debug("Error Occurred when writing to network stream");
            }
        }

        private void WriteCallback(IAsyncResult result)
        {
            try
            {
                NetworkClient tcpClient = result.AsyncState as NetworkClient;
                NetworkStream networkStream = tcpClient.NetworkStream;
                networkStream.EndWrite(result);
            }
            catch
            {
                Debug("Error Occurred when writing to Network stream");
            }
        }

        private void ReadCallback(IAsyncResult result)
        {
            try
            {
                NetworkClient client2 = result.AsyncState as NetworkClient;
                if (client2 == null) return;
                NetworkStream networkStream = client2.NetworkStream;
                int read = networkStream.EndRead(result);
                if (read == 0)
                {
                    Debug("Network Disconnected");
                    OnDisconnected.SafeInvoke(this, new EventArgs());
                    //Start(nHost, nPort);
                    return;
                }

                string data = Encoding.ASCII.GetString(client2.Buffer, 0, read);
                ProcessIncoming(data);

                byte[] b = new byte[read];
                Array.Copy(client2.Buffer, b, read);

                ProcessIncoming(b);

                //Do something with the data object here.
                networkStream.BeginRead(client2.Buffer, 0, client2.Buffer.Length, ReadCallback, client2);
            }
            catch { }
        }
    }

    /// <summary>
    /// Internal class to join the TCP client and buffer together 
    /// for easy management in the server
    /// </summary>
    internal class NetworkClient
    {
        /// <summary>
        /// Constructor for a new Client
        /// </summary>
        /// <param name="tcpClient">The TCP client</param>
        /// <param name="buffer">The byte array buffer</param>
        public NetworkClient(TcpClient tcpClient, byte[] buffer)
        {
            if (tcpClient == null) throw new ArgumentNullException("tcpClient");
            if (buffer == null) throw new ArgumentNullException("buffer");
            this.TcpClient = tcpClient;
            this.Buffer = buffer;
        }

        /// <summary>
        /// Gets the TCP Client
        /// </summary>
        public TcpClient TcpClient { get; private set; }

        /// <summary>
        /// Gets the Buffer.
        /// </summary>
        public byte[] Buffer { get; private set; }

        /// <summary>
        /// Gets the network stream
        /// </summary>
        public NetworkStream NetworkStream { get { return TcpClient.GetStream(); } }
    }
}
