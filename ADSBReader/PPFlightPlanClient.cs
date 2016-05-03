using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Net;
using System.Xml.Serialization;
using System.IO;
using System.Threading;

namespace ADSBReader
{
    public static class PPFlightPlanClient
    {
        public delegate void ReceivedFlightPlanDel(PPFlightPlan FlightPlan);
        public static event ReceivedFlightPlanDel ReceivedFlightPlan;

        private static ConcurrentDictionary<string, PPFlightPlan> mFlightPlans = new ConcurrentDictionary<string,PPFlightPlan>();

        private const string FLIGHTPLANURLFORMAT = "http://www.craig-phillips.co.uk/navdata/getFlightPlanXML.php?cs={0}";
        private const string RECENTFLIGHTPLANURL = "http://www.craig-phillips.co.uk/navdata/getRecentFlightsXML.php";
        private const int DATAREFRESH = 120;   // Timeout in milliseconds (2 mins)

        private static bool IsStarted = false;

        public static void Start()
        {
            IsStarted = true;
            Thread t = new Thread(new ThreadStart(WaitForUpdate));
            t.Start();
        }

        public static void Stop()
        {
            IsStarted = false;
        }

        private static void WaitForUpdate()
        {
            if (!IsStarted)
                return;

            RequestRecentFlightPlans();

            // Wait for timeout
            DateTime start = DateTime.Now;
            while (IsStarted && (DateTime.Now - start).TotalSeconds < DATAREFRESH)
            {
                Thread.Sleep(250);
            }
            //Thread.Sleep(DATAREFRESH);

            // Recall this
            if (IsStarted)
                WaitForUpdate();
        }

        private static void RequestRecentFlightPlans()
        {
            WebClient wc = new WebClient();
            wc.DownloadStringCompleted+=new DownloadStringCompletedEventHandler(RecentFPStringCompleted);
            wc.DownloadStringAsync(new Uri(RECENTFLIGHTPLANURL));
        }

        private static void RecentFPStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            if (e.Error != null)
                return;

            string[] callsigns = e.Result.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string callsign in callsigns)
            {
                if (callsign.Trim().Length > 0)
                {
                    Console.WriteLine("Getting recent flight plan for {0}", callsign.Trim());
                    GetFlightPlan(callsign.Trim());
                }
            }
        }

        public static void GetFlightPlan(string Callsign)
        {
            // Flight plan already exists
            if (ReceivedFlightPlan != null && mFlightPlans.ContainsKey(Callsign))
            {
                ReceivedFlightPlan(mFlightPlans[Callsign]);
                return;
            }

            if (Callsign.Length == 0)
                return;

            WebClient wc = new WebClient();
            wc.DownloadStringCompleted += new DownloadStringCompletedEventHandler(wc_DownloadStringCompleted);
            wc.DownloadStringAsync(new Uri(string.Format(FLIGHTPLANURLFORMAT, Callsign)));
        }

        private static void wc_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            if (e.Error != null)
                return;

            XmlSerializer serializer = new XmlSerializer(typeof(PPFlightPlan));
            MemoryStream ms = new MemoryStream(Encoding.ASCII.GetBytes(e.Result));
            PPFlightPlan plan = (PPFlightPlan)serializer.Deserialize(ms);

            if (ReceivedFlightPlan != null && plan.Callsign != null)
            {
                bool succ = mFlightPlans.TryAdd(plan.Callsign, plan);
                ReceivedFlightPlan(plan);
            }
        }
    }
}
