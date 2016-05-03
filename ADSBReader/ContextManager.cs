using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using System.IO;

namespace ADSBReader
{
    public unsafe class ContextManager : ApplicationContext
    {
        private FormMain _frm;
        private AdsbBitDecoder _decoder;
        private RTLReader _rtl;
        //private AdsbFrameDecoder _framedec;
        private RtlSdrIO _rtlDevice = new RtlSdrIO();

        private AdsbRawNet _adsbRawNet;

        private ModeSMessageConsumer _consumer = new ModeSMessageConsumer();

        private System.Windows.Forms.Timer fpsTimer = new System.Windows.Forms.Timer();

        private System.Windows.Forms.Timer plotTimer = new System.Windows.Forms.Timer();

        private AdsbBinaryReader _abr;

        private long frameCount = 0;

        //private AdsbCoded _codedAdsb;

        public ContextManager()
        {
            _frm = new FormMain();
            _frm.Aircraft = _consumer.Aircraft;
            _frm.FormClosed += _frm_FormClosed;
            _frm.Show();

            //_framedec = new AdsbFrameDecoder();
            fpsTimer.Interval = 500;
            fpsTimer.Tick += fpsTimer_Tick;
            fpsTimer.Start();

            plotTimer.Interval = 5000;
            plotTimer.Tick += plotTimer_Tick;
            plotTimer.Start();

            //_consumer.AircraftPositionUpdated += _consumer_AircraftPositionUpdated;

            _decoder = new AdsbBitDecoder();
            //_decoder.ConfidenceLevel = 4;
            //_decoder.Timeout = 120;
            //_decoder.FrameReceived += _decoder_FrameReceived;
            _decoder.ModeSMessageReceived += _decoder_ModeSMessageReceived;

            // Receive flight plans
            PPFlightPlanClient.ReceivedFlightPlan += new PPFlightPlanClient.ReceivedFlightPlanDel(FlightPlanClient_ReceivedFlightPlan);
            PPFlightPlanClient.Start();

            // Initiate client handler
            ServerNetwork.FlightPlanRequested += new ServerNetwork.FlightPlanRequestDelegate(serverNetwork_FlightPlanRequested);
            ServerNetwork.ClientConnected += new ServerNetwork.ClientEventDelegate(serverNetwork_ClientConnected);
            ServerNetwork.ClientDisconnected += new ServerNetwork.ClientEventDelegate(serverNetwork_ClientDisconnected);
            ServerNetwork.ClientChanged += new ServerNetwork.ClientEventDelegate(serverNetwork_ClientDisconnected);
            ServerNetwork.Start();

            /*_adsbRawNet = new AdsbRawNet();
            _adsbRawNet.FrameReceived += _abr_FrameAvailable;
            _adsbRawNet.Start("192.168.0.21");*/

            _rtlDevice.Open();
            _rtlDevice.SelectDevice(0);
            _rtlDevice.Device.UseTunerAGC = true;
            _rtlDevice.Device.Samplerate = 2000000;
            _rtlDevice.Start(rtl_SamplesAvailable);
#warning File RTL
            /*_rtl = new RTLReader();
            _rtl.SamplesAvailable += _rtl_SamplesAvailable;
            _rtl.ReadFromFile(@"C:\Users\Craig Phillips\Documents\dump1090-win.1.09.0608.14\output3.bin");*/

            /*_codedAdsb = new AdsbCoded();
            _codedAdsb.FrameReceived += _codedAdsb_FrameReceived;
            _codedAdsb.ReadFile(@"C:\Users\Craig Phillips\Documents\Visual Studio 2013\Projects\ADSBReader\ADSBReader\out.bin");*/

            /*_abr = new AdsbBinaryReader();
            _abr.FrameAvailable += _abr_FrameAvailable;
            _abr.FinishedReading += _abr_FinishedReading;
            _abr.ReadFromFile("out.bin");*/
        }

        void plotTimer_Tick(object sender, EventArgs e)
        {
            var acs = from a in _consumer.Aircraft
                      where (DateTime.Now - a.Value.PositionTime).TotalSeconds <= 5 && a.Value.Callsign != null && a.Value.Callsign.Length > 3
                      select a.Value;

            foreach (Aircraft a in acs)
            {
                ServerNetwork.ReceivedAircraft(a);
            }
        }

        /*void _consumer_AircraftPositionUpdated(Aircraft aircraft)
        {
            if (aircraft.Callsign != null && aircraft.Position != null && aircraft.Callsign!= null && aircraft.Callsign.Length > 3)
                ServerNetwork.ReceivedAircraft(aircraft);
        }*/

        void _abr_FinishedReading()
        {
            FileStream fs = new FileStream("kml.txt", FileMode.Create, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs);

            foreach (KeyValuePair<string, Aircraft> kvp in _consumer.Aircraft)
            {
                Aircraft a = kvp.Value;

                sw.WriteLine("\t<Placemark>");
		        sw.WriteLine("\t\t<name>{0}</name>", a.HexAddress);
		        sw.WriteLine("\t\t<styleUrl>#msn_ylw-pushpin</styleUrl>");
		        sw.WriteLine("\t\t<LineString>");
			    sw.WriteLine("\t\t\t<tessellate>1</tessellate>");
                sw.WriteLine("\t\t\t<gx:altitudeMode>relativeToSeaFloor</gx:altitudeMode>");
			    sw.WriteLine("\t\t\t<coordinates>");
				sw.WriteLine("\t\t\t\t{0}", a.GetCoordinateString());
			    sw.WriteLine("\t\t\t</coordinates>");
		        sw.WriteLine("\t\t</LineString>");
                sw.WriteLine("\t</Placemark>");
            }

            sw.Close();
        }

        void _abr_FrameAvailable(byte[] frame)
        {
            ModeSMessage msg = _decoder.DecodeModeSMessage(frame);

            if (msg != null)
                _decoder_ModeSMessageReceived(msg);
        }

        private void serverNetwork_ClientDisconnected(RadarClient client)
        {
            
        }

        private void serverNetwork_ClientConnected(RadarClient client)
        {
            
        }

        void _rtl_SamplesAvailable(object sender, SamplesAvailableEventArgs e)
        {
            _decoder.ProcessSamples(e.Buffer, e.Length);
        }

        void _decoder_ModeSMessageReceived(ModeSMessage msg)
        {
            Interlocked.Increment(ref frameCount);
            _consumer.ConsumeMessage(msg);
        }

        void fpsTimer_Tick(object sender, EventArgs e)
        {
            float fps = frameCount * 1000 / fpsTimer.Interval;
            _frm.UpdateFPS(fps);
            frameCount = 0;
        }

        void _rtl_MagnitudeReceived(short mag)
        {
            if (_decoder == null) return;

            //_decoder.ProcessSample(mag);
        }

        void _frm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _rtlDevice.Stop();
            ServerNetwork.Stop();
            PPFlightPlanClient.Stop();
            Application.Exit();
        }

        private void rtl_SamplesAvailable(object sender, Complex* buf, int length)
        {
            /*for (var i = 0; i < length; i++)
            {
                var real = buf[i].Real;
                var imag = buf[i].Imag;

                var mag = real * real + imag * imag;

                _decoder.ProcessSample(mag);
            }*/
            _decoder.ProcessSamples(buf, length);
        }

        private void serverNetwork_FlightPlanRequested(string callsign)
        {
            PPFlightPlanClient.GetFlightPlan(callsign);
        }

        private void FlightPlanClient_ReceivedFlightPlan(PPFlightPlan FlightPlan)
        {
            ServerNetwork.FlightPlanReceived(FlightPlan);
        }
    }
}
