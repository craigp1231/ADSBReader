using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ADSBReader
{
    public enum DownlinkFormats : byte
    {
        ShortAirToAir = 0,
        AltitudeReply = 4,
        IdentReply = 5,
        AllCallReply = 11,
        LongAirToAir = 16,
        AdsbExtendedSquitter = 17,
        MilitaryExtendedSquitter = 19,
        CommBAltitudeReply = 20,
        CommBIdentReply = 21,
        Military = 22,
        ExtendedLength = 24,
    }

    public enum AircraftCategorys : byte
    {
        CategoryD = 1,
        CategoryC = 2,
        CategoryB = 3,
        CategoryA = 4,
    }

    public enum AircraftEmitterCategorys
    {
        NoCategoryA,
        Light,
        Small,
        Large,
        HighVortexLarge,
        Heavy,
        HighPerformance,
        NoCategoryB,
        Glider,
        LighterThanAir,
        Parachutist,
        UltraLight,
        ReservedB,
        UnmannedVehicle,
        SpaceVehicle,
        NoCategoryC,
        EmergencySurfaceVehicle,
        ServiceVehicle,
        PointObsticle,
        ClusterObsticle,
        LineObsticle,
        NoCategoryD,
    }

    public enum SurveillanceStatusCategory
    {
        NoCondition,
        PermanentAlert,
        TemporaryAlert,
        SPI,                // Ident
    }

    public enum CPRType
    {
        Even,
        Odd
    }

    public enum EmergencyStateEnum
    {
        NoEmergency,
        GeneralEmergency,
        Lifeguard,
        MinimumFuel,
        NoComms,
        Hijack,
        Downed,
    }
}
