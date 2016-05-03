using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace ADSBReader
{
    [XmlRoot("FlightPlan")]
    public class PPFlightPlan
    {
        [XmlElement("callsign")]
        public string Callsign { get; set; }

        [XmlElement("flt_rule")]
        public string FlightRule { get; set; }

        [XmlElement("flt_type")]
        public string FlightType { get; set; }

        [XmlElement("ac_type")]
        public string AircraftType { get; set; }

        [XmlElement("wake_cat")]
        public string WeightCategory { get; set; }

        [XmlElement("eqpt")]
        public string Equipment { get; set; }

        [XmlElement("sec_eqpt")]
        public string SecondaryEquipment { get; set; }

        [XmlElement("dep_icao")]
        public string DepartureICAO { get; set; }

        [XmlElement("arr_icao")]
        public string ArrivalICAO { get; set; }

        [XmlElement("alt_icao")]
        public string AlternateICAO { get; set; }

        [XmlElement("dep_name")]
        public string DepartureName { get; set; }

        [XmlElement("arr_name")]
        public string ArrivalName { get; set; }

        [XmlElement("alt_name")]
        public string AlternateName { get; set; }

        [XmlIgnore]
        private DateTime mOffBlockTime;

        [XmlElement("enroute_time")]
        public int EnrouteTime { get; set; }

        [XmlElement("route")]
        public string Route { get; set; }

        [XmlElement("remarks")]
        public string Remarks { get; set; }

        [XmlElement("eobt")]
        public int OffBlockTimeInt
        {
            set
            {
                mOffBlockTime = new DateTime(1970, 1, 1, 0, 0, 0).AddSeconds(value);
            }
            get
            {
                return (int)mOffBlockTime.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
            }
        }

        [XmlIgnore]
        public DateTime OffBlockTime
        {
            get
            {
                return mOffBlockTime;
            }
            set
            {
                mOffBlockTime = value;
            }
        }
    }
}
