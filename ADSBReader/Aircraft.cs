using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using System.IO;
using System.Data.SQLite;

namespace ADSBReader
{
    public class Aircraft
    {
        public byte[] ICAOAddress { get; private set; }
        public string HexAddress { get; private set; }

        public string AircraftReg { get; private set; }
        public string AircraftType { get; private set; }

        private string _callsign;
        private DateTime _callsign_time;
        private int _altitude;
        private DateTime _altitude_time;
        private AircraftEmitterCategorys _aircraftcategory;
        private DateTime _aircraftcategory_time;
        private ushort _speed;
        private DateTime _speed_time;
        private ushort _heading;
        private DateTime _heading_time;
        private string _squawk;
        private DateTime _squawk_time;
        private short _verticalspeed;
        private DateTime _verticalspeed_time;
        private ushort _mcpaltitude;
        private DateTime _mcpaltitude_time;
        private bool _mcpaltchanged;

        private List<KMLPosition> _position_store = new List<KMLPosition>();

        public string Callsign { 
            get
            {
                return _callsign;
            }
            set
            {
                _callsign = value;

                if (value != null)
                {
                    _callsign_time = DateTime.Now;
                    LastSeen = DateTime.Now;
                }
            }
        }
        public int Altitude
        {
            get
            {
                return _altitude;
            }
            set
            {
                _altitude = value;
                _altitude_time = DateTime.Now;
                LastSeen = DateTime.Now;
            }
        }
        public AircraftEmitterCategorys AircraftCategory { 
            get
            {
                return _aircraftcategory;
            }
            set
            {
                _aircraftcategory = value;
                _aircraftcategory_time = DateTime.Now;
                LastSeen = DateTime.Now;
            }
        }
        public ushort Speed { 
            get
            {
                return _speed;
            }
            set
            {
                _speed = value;

                _speed_time = DateTime.Now;
                LastSeen = DateTime.Now;
            }
        }
        public ushort Heading { 
            get
            {
                return _heading;
            }
            set
            {
                _heading = value;

                _heading_time = DateTime.Now;
                LastSeen = DateTime.Now;
            }
        }
        public string Squawk { 
            get
            {
                return _squawk;
            }
            set
            {
                _squawk = value;
                if (value != null)
                {
                    _squawk_time = DateTime.Now;
                    LastSeen = DateTime.Now;
                }
            }
        }
        public short VerticalSpeed
        {
            get
            {
                return _verticalspeed;
            }
            set
            {
                _verticalspeed = value;

                _verticalspeed_time = DateTime.Now;
                LastSeen = DateTime.Now;
            }
        }
        public ushort MCPAltitude
        {
            get
            {
                return _mcpaltitude;
            }
            set
            {
                if (_mcpaltitude != value)
                {
                    _mcpaltchanged = true;
                }
                else if ((DateTime.Now - _mcpaltitude_time).TotalSeconds > 5)
                {
                    _mcpaltchanged = false;
                }

                _mcpaltitude = value;

                _mcpaltitude_time = DateTime.Now;
                LastSeen = DateTime.Now;
            }
        }

        public DateTime CallsignTime
        {
            get { return _callsign_time; }
        }
        public DateTime AltitudeTime
        {
            get { return _altitude_time; }
        }
        public DateTime AircraftCategoryTime
        {
            get { return _aircraftcategory_time; }
        }
        public DateTime SpeedTime
        {
            get { return _speed_time; }
        }
        public DateTime HeadingTime
        {
            get { return _heading_time; }
        }
        public DateTime SquawkTime
        {
            get { return _squawk_time; }
        }
        public DateTime VerticalSpeedTime
        {
            get { return _verticalspeed_time; }
        }
        public DateTime MCPAltitudeTime
        {
            get { return _mcpaltitude_time; }
        }
        public bool MCPAltChanged
        {
            get
            {
                if (_mcpaltitude_time == DateTime.MinValue) return false;

                if ((DateTime.Now - _mcpaltitude_time).TotalSeconds > 5)
                {
                    _mcpaltchanged = false;
                    return false;
                }

                return _mcpaltchanged;
            }
        }

        //public ushort MCPAlt { get; set; }
        public DateTime LastSeen { get; private set; }
        public int MessageCount { get; set; }

        public WorldPoint Position { get; private set; }
        public DateTime PositionTime { get; private set; }

        private CPRPoint CPREvenPoint;
        private CPRPoint CPROddPoint;

        const double AirDlat0 = 360d / 60d;
        const double AirDlat1 = 360d / 59d;

        public Aircraft(byte[] address)
        {
            this.ICAOAddress = address;

            if (address.Length == 3)
                this.HexAddress = string.Format("{0:X2}{1:X2}{2:X2}", address[0], address[1], address[2]);

            // Get aircraft info

            if (File.Exists("aircraft.sqb"))
            {
                string cs = "Data Source=aircraft.sqb;Version=3;";

                using (SQLiteConnection con = new SQLiteConnection(cs))
                {
                    con.Open();

                    string stm = string.Format("SELECT Registration, ICAOTypeCode, OperatorFlagCode FROM Aircraft WHERE ModeS='{0}' LIMIT 1", this.HexAddress);

                    using (SQLiteCommand cmd = new SQLiteCommand(stm, con))
                    {
                        using (SQLiteDataReader rdr = cmd.ExecuteReader())
                        {
                            if (rdr.Read())
                            {
                                try
                                {
                                    this.AircraftReg = rdr.GetString(0);
                                    this.AircraftType = rdr.GetString(1);

                                    if (this.Callsign == string.Empty || this.Callsign == null)
                                        this.Callsign = rdr.GetValue(2).ToString();
                                }
                                catch { }
                                /*Console.WriteLine(rdr.GetInt32(0) + " "
                                    + rdr.GetString(1) + " " + rdr.GetInt32(2));*/
                                
                            }
                        }
                    }

                    con.Close();
                }
            }
        }

        public string GetCoordinateString()
        {
            StringBuilder sb = new StringBuilder();

            foreach (KMLPosition p in _position_store)
            {
                sb.AppendFormat("{0} ", p.ToString(true));
            }

            return sb.ToString();
        }

        public void AddPoint(CPRType type, int RawLat, int RawLon)
        {
            if (type == CPRType.Even)
                CPREvenPoint = new CPRPoint(RawLat, RawLon);
            else
                CPROddPoint = new CPRPoint(RawLat, RawLon);

            if (CPREvenPoint != null && CPROddPoint != null)
            {
                // If the time difference between odd and even points is 10 then decode the CPR
                if (Math.Abs((CPREvenPoint.Date - CPROddPoint.Date).TotalSeconds) <= 10)
                {
                    decodeCPR();
                }
            }

            this.LastSeen = DateTime.Now;
        }

        private void decodeCPR()
        {
            double lat0 = CPREvenPoint.RawLat;
            double lat1 = CPROddPoint.RawLat;
            double lon0 = CPREvenPoint.RawLon;
            double lon1 = CPROddPoint.RawLon;

            int j = (int)Math.Floor(((59 * lat0 - 60 * lat1) / 131072) + 0.5); 
            double rlat0 = AirDlat0 * (cprModFunction(j,60) + lat0 / 131072); 
            double rlat1 = AirDlat1 * (cprModFunction(j,59) + lat1 / 131072);

            if (rlat0 >= 270) rlat0 -= 360;
            if (rlat1 >= 270) rlat1 -= 360;

            if (cprNLFunction(lat0) != cprNLFunction(lat1)) return;

            if (CPREvenPoint.Date >= CPROddPoint.Date)
            {
                int ni = cprNFunction(rlat0, 0);
                int m = (int)Math.Floor((((lon0 * (cprNLFunction(rlat0) - 1)) -
                                (lon1 * cprNLFunction(rlat0))) / 131072) + 0.5);
                double Lon = cprDlonFunction(rlat0, 0) * (cprModFunction(m, ni) + lon0 / 131072);
                double Lat = rlat0;

                if (Lon > 180) Lon -= 360;

                Position = new WorldPoint((float)Lat, (float)Lon);

                //Debug.WriteLine(Position);
            }
            else
            {
                int ni = cprNFunction(rlat1, 1);
                int m = (int)Math.Floor((((lon0 * (cprNLFunction(rlat1) - 1)) -
                                (lon1 * cprNLFunction(rlat1))) / 131072.0) + 0.5);
                double Lon = cprDlonFunction(rlat1, 1) * (cprModFunction(m, ni) + lon1 / 131072);
                double Lat = rlat1;

                if (Lon > 180) Lon -= 360;

                Position = new WorldPoint((float)Lat, (float)Lon);

                //Debug.WriteLine(Position);
            }

            KMLPosition p1 = _position_store.Count > 0 ? _position_store.Last() : null;
            if (p1 == null || (Math.Abs(p1.Latitude - Position.Latitude) > 0 && Math.Abs(p1.Latitude - Position.Latitude) < 1 &&
                Math.Abs(p1.Longitude - Position.Longitude) > 0 && Math.Abs(p1.Longitude - Position.Longitude) < 1))
            {
                p1 = new KMLPosition(Position.Latitude, Position.Longitude, this.Altitude);
                _position_store.Add(p1);
            }

            PositionTime = DateTime.Now;
        }

        private int cprModFunction(int a, int b)
        {
            int res = a % b; 
            if (res < 0) res += b; 
            return res; 
        }

        /* The NL function uses the precomputed table from 1090-WP-9-14 */
        private int cprNLFunction(double lat)
        {
            if (lat < 0) lat = -lat; /* Table is simmetric about the equator. */
            if (lat < 10.47047130) return 59;
            if (lat < 14.82817437) return 58;
            if (lat < 18.18626357) return 57;
            if (lat < 21.02939493) return 56;
            if (lat < 23.54504487) return 55;
            if (lat < 25.82924707) return 54;
            if (lat < 27.93898710) return 53;
            if (lat < 29.91135686) return 52;
            if (lat < 31.77209708) return 51;
            if (lat < 33.53993436) return 50;
            if (lat < 35.22899598) return 49;
            if (lat < 36.85025108) return 48;
            if (lat < 38.41241892) return 47;
            if (lat < 39.92256684) return 46;
            if (lat < 41.38651832) return 45;
            if (lat < 42.80914012) return 44;
            if (lat < 44.19454951) return 43;
            if (lat < 45.54626723) return 42;
            if (lat < 46.86733252) return 41;
            if (lat < 48.16039128) return 40;
            if (lat < 49.42776439) return 39;
            if (lat < 50.67150166) return 38;
            if (lat < 51.89342469) return 37;
            if (lat < 53.09516153) return 36;
            if (lat < 54.27817472) return 35;
            if (lat < 55.44378444) return 34;
            if (lat < 56.59318756) return 33;
            if (lat < 57.72747354) return 32;
            if (lat < 58.84763776) return 31;
            if (lat < 59.95459277) return 30;
            if (lat < 61.04917774) return 29;
            if (lat < 62.13216659) return 28;
            if (lat < 63.20427479) return 27;
            if (lat < 64.26616523) return 26;
            if (lat < 65.31845310) return 25;
            if (lat < 66.36171008) return 24;
            if (lat < 67.39646774) return 23;
            if (lat < 68.42322022) return 22;
            if (lat < 69.44242631) return 21;
            if (lat < 70.45451075) return 20;
            if (lat < 71.45986473) return 19;
            if (lat < 72.45884545) return 18;
            if (lat < 73.45177442) return 17;
            if (lat < 74.43893416) return 16;
            if (lat < 75.42056257) return 15;
            if (lat < 76.39684391) return 14;
            if (lat < 77.36789461) return 13;
            if (lat < 78.33374083) return 12;
            if (lat < 79.29428225) return 11;
            if (lat < 80.24923213) return 10;
            if (lat < 81.19801349) return 9;
            if (lat < 82.13956981) return 8;
            if (lat < 83.07199445) return 7;
            if (lat < 83.99173563) return 6;
            if (lat < 84.89166191) return 5;
            if (lat < 85.75541621) return 4;
            if (lat < 86.53536998) return 3;
            if (lat < 87.00000000) return 2;
            else return 1;
        }

        private int cprNFunction(double lat, int isodd)
        {
            int nl = cprNLFunction(lat) - isodd;
            if (nl < 1) nl = 1;
            return nl;
        }

        private double cprDlonFunction(double lat, int isodd)
        {
            return 360.0 / cprNFunction(lat, isodd);
        }

    }
}
