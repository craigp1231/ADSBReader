using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ADSBReader
{
    public class AdsbFrameDecoder
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

        private const string IDENT_CHARS = "?ABCDEFGHIJKLMNOPQRSTUVWXYZ????? ???????????????0123456789??????";

        private Dictionary<byte, int> typeCounts = new Dictionary<byte, int>();

        private Dictionary<string, Aircraft> aircrafts = new Dictionary<string,Aircraft>();

        private uint[] crc_checksum = new uint[] {0x3935ea, 0x1c9af5, 0xf1b77e, 0x78dbbf, 0xc397db, 0x9e31e9, 0xb0e2f0, 0x587178,
0x2c38bc, 0x161c5e, 0x0b0e2f, 0xfa7d13, 0x82c48d, 0xbe9842, 0x5f4c21, 0xd05c14,
0x682e0a, 0x341705, 0xe5f186, 0x72f8c3, 0xc68665, 0x9cb936, 0x4e5c9b, 0xd8d449,
0x939020, 0x49c810, 0x24e408, 0x127204, 0x093902, 0x049c81, 0xfdb444, 0x7eda22,
0x3f6d11, 0xe04c8c, 0x702646, 0x381323, 0xe3f395, 0x8e03ce, 0x4701e7, 0xdc7af7,
0x91c77f, 0xb719bb, 0xa476d9, 0xadc168, 0x56e0b4, 0x2b705a, 0x15b82d, 0xf52612,
0x7a9309, 0xc2b380, 0x6159c0, 0x30ace0, 0x185670, 0x0c2b38, 0x06159c, 0x030ace,
0x018567, 0xff38b7, 0x80665f, 0xbfc92b, 0xa01e91, 0xaff54c, 0x57faa6, 0x2bfd53,
0xea04ad, 0x8af852, 0x457c29, 0xdd4410, 0x6ea208, 0x375104, 0x1ba882, 0x0dd441,
0xf91024, 0x7c8812, 0x3e4409, 0xe0d800, 0x706c00, 0x383600, 0x1c1b00, 0x0e0d80,
0x0706c0, 0x038360, 0x01c1b0, 0x00e0d8, 0x00706c, 0x003836, 0x001c1b, 0xfff409,
0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000,
0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000,
0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000};

        private uint ModeSCRC(byte[] frame)
        {
            uint crc = 0;
            ushort nBits = (ushort)(frame.Length * 8);
            int offset = nBits == 112 ? 0 : 56;

            for (ushort j = 0; j < nBits; j++)
            {
                int byteNum = j / 8;
                int bitNum = j % 8;
                int bitMask = 1 << (7 - bitNum);

                if ((frame[byteNum] & bitMask) == bitMask)
                {
                    crc ^= crc_checksum[j + offset];
                }
            }

            return crc;
        }

        private int modesMessageLenByType(DownlinkFormats fmt)
        {
            if (fmt == DownlinkFormats.LongAirToAir || fmt == DownlinkFormats.AdsbExtendedSquitter ||
                fmt == DownlinkFormats.MilitaryExtendedSquitter || fmt == DownlinkFormats.CommBAltitudeReply ||
                fmt == DownlinkFormats.CommBIdentReply)
                return 112;
            else
                return 56;
        }

        private int fixSingleBitErrors(ref byte[] frame)
        {
            int j;
            byte[] aux = new byte[14];

            for (j = 0; j < frame.Length * 8; j++)
            {
                int byteNum = j / 8;
                int bitMask = 1 << (7 - (j % 8));

                uint crc1, crc2;

                //aux = frame.Cop;
                frame.CopyTo(aux, 0);
                aux[byteNum] ^= (byte)bitMask;

                crc1 = ((uint)(aux[aux.Length - 3] << 16)) |
                       ((uint)(aux[aux.Length - 2] << 8)) |
                       ((uint)(aux[aux.Length - 1] << 0));
                crc2 = ModeSCRC(aux);

                if (crc1 == crc2)
                {
                    aux.CopyTo(frame, 0);
                    return j;
                }
            }
            return -1;
        }

        private int fixDoubleBitErrors(ref byte[] frame)
        {
            int i, j;
            byte[] aux = new byte[14];

            for (j = 0; j < frame.Length * 8; j++)
            {
                int byteNum1 = j / 8;
                int bitMask1 = 1 << (7 - (j % 8));

                for (i = j+1; i < frame.Length * 8; i++)
                {
                    int byteNum2 = i / 8;
                    int bitMask2 = 1 << (7 - (i % 8));

                    uint crc1, crc2;

                    frame.CopyTo(aux, 0);

                    aux[byteNum1] ^= (byte)bitMask1;
                    aux[byteNum2] ^= (byte)bitMask2;

                    crc1 = ((uint)(aux[aux.Length - 3] << 16)) |
                       ((uint)(aux[aux.Length - 2] << 8)) |
                       ((uint)(aux[aux.Length - 1] << 0));
                    crc2 = ModeSCRC(aux);

                    if (crc1 == crc2)
                    {
                        // We somehow have fixed the error
                        aux.CopyTo(frame, 0);
                        return j | (i << 8);
                    }
                }
            }

            return -1;
        }

        public void DecodeModeSFrame(byte[] frame)
        {
            DownlinkFormats fmt = (DownlinkFormats)(frame[0] >> 3);

            uint crc = ((uint)(frame[frame.Length - 3] << 16)) |
                       ((uint)(frame[frame.Length - 2] << 8)) |
                       ((uint)(frame[frame.Length - 1] << 0));
            bool crc_ok = ModeSCRC(frame) == crc;

            int mLen = modesMessageLenByType(fmt);

            if (fmt == DownlinkFormats.AdsbExtendedSquitter)
            {
                if (!crc_ok)
                {
                    if (fixSingleBitErrors(ref frame) != -1)
                    {
                        crc_ok = true;
                    }
                    else if (fixDoubleBitErrors(ref frame) != -1)
                    {
                        crc_ok = true;
                    }
                }

                //if (!crc_ok) return;

                /*StringBuilder icaoAddr = new StringBuilder();
                icaoAddr.AppendFormat("{0:X2}{1:X2}{2:X2}", frame[1], frame[2], frame[3]);*/
                byte[] address = new byte[] { frame[1], frame[2], frame[3] };
                Aircraft a = GetAircraft(address);


                byte sqtType = (byte)(frame[4] >> 3);
                byte sqtSubType = (byte)(frame[4] & 7);

                AddTypeCount(sqtType);

                if (sqtType >= 1 && sqtType <= 4)
                {
                    AircraftCategorys cat = (AircraftCategorys)sqtType;

                    // Aircraft Identification
                    StringBuilder callsign = new StringBuilder();
                    callsign.Append(IDENT_CHARS[frame[5] >> 2]);
                    callsign.Append(IDENT_CHARS[((frame[5] & 3) << 4) | (frame[6] >> 4)]);
                    callsign.Append(IDENT_CHARS[((frame[6] & 15) << 2) | (frame[7] >> 6)]);
                    callsign.Append(IDENT_CHARS[frame[7] & 63]);
                    callsign.Append(IDENT_CHARS[frame[8] >> 2]);
                    callsign.Append(IDENT_CHARS[((frame[8] & 3) << 4) | (frame[9] >> 4)]);
                    callsign.Append(IDENT_CHARS[((frame[9] & 15) << 2) | (frame[10] >> 6)]);
                    callsign.Append(IDENT_CHARS[frame[10] & 63]);

                    a.Callsign = callsign.ToString().Trim();
                }
                else if (sqtType >= 9 && sqtType <= 18)
                {
                    Aircraft.PointType CPRType = (Aircraft.PointType)((frame[6] & (1 << 2)) >> 2);
                    byte TimeFlag = (byte)((frame[6] & (1 << 3)) >> 3);

                    // Altitude
                    bool qBit = (byte)(frame[5] & 1) == 1;
                    if (qBit)
                    {
                        // Altitude can be decoded
                        int n = ((frame[5] >> 1) << 4) | ((frame[6] & 0xF0) >> 4);
                        a.Altitude = n * 25 - 1000;
                    }

                    int rawLat = ((frame[6] & 3) << 15) |
                                (frame[7] << 7) |
                                (frame[8] >> 1);
                    int rawLon = ((frame[8] & 1) << 16) |
                                 (frame[9] << 8) |
                                 frame[10];

                    a.AddPoint(CPRType, rawLat, rawLon);
                }
                else if (sqtType == 19)
                {
                    if (sqtSubType == 1 || sqtSubType == 2)
                    {
                        int ew_dir = (frame[5] & 4) >> 2;
                        int ew_velocity = ((frame[5] & 3) << 8) | frame[6];
                        int ns_dir = (frame[7] & 0x80) >> 7;
                        int ns_velocity = ((frame[7] & 0x7f) << 3) | ((frame[8] & 0xe0) >> 5);
                        int vert_rate_source = (frame[8] & 0x10) >> 4;
                        int vert_rate_sign = (frame[8] & 0x8) >> 3;
                        int vert_rate = ((frame[8] & 7) << 6) | ((frame[9] & 0xfc) >> 2);

                        if (ew_dir == 1) ew_velocity *= -1;
                        if (ns_dir == 1) ns_velocity *= -1;
                        if (vert_rate_sign == 1) vert_rate *= -1;

                        vert_rate = (vert_rate - 1) * 64;

                        /* Compute velocity and angle from the two speed
                         * components. */
                        double velocity = Math.Sqrt(ns_velocity * ns_velocity +
                                            ew_velocity * ew_velocity);

                        double heading = 0;
                        if (velocity > 0)
                        {
                            
                            heading = Math.Atan2(ew_velocity, ns_velocity);
                            heading = heading * 360 / (Math.PI * 2);
                            if (heading < 0) heading += 360;

                            a.Speed = (ushort)velocity;
                            a.Track = (ushort)heading;
                        }
                    }
                    else if (sqtSubType == 3 || sqtSubType == 4)
                    {
                        double heading = (360.0 / 128) * (((frame[5] & 3) << 5) | (frame[6] >> 3));
                        a.Track = (ushort)heading;
                    }
                }
                else if (sqtType == 29)
                {
                    if (sqtSubType == 2)
                    {
                        byte AltType = (byte)(frame[5] >> 7);
                        ushort MCPAlt = (ushort)((frame[5] & 127) << 4);
                        MCPAlt |= (ushort)((frame[6] & 240) >> 4);
                        MCPAlt -= 1;
                        MCPAlt *= 32;
                        a.MCPAlt = MCPAlt;
                    }
                    else if (sqtSubType == 0)
                    {
                        ushort TargetAlt = (ushort)((frame[5] & 1) << 9);
                        TargetAlt |= (ushort)((frame[6]) << 1);
                        TargetAlt |= (ushort)(frame[7] >> 7);
                        a.MCPAlt = TargetAlt;
                    }
                }
            }
        }

        private void AddTypeCount(byte sqtType)
        {
            if (typeCounts.ContainsKey(sqtType))
            {
                typeCounts[sqtType] = typeCounts[sqtType] + 1;
            }
            else
                typeCounts[sqtType] = 1;
        }

        private Aircraft GetAircraft(byte[] ICAOAddress)
        {
            if (ICAOAddress.Length != 3) return null;

            string HexAddress = string.Format("{0:X2}{1:X2}{2:X2}", ICAOAddress[0], ICAOAddress[1], ICAOAddress[2]);

            if (aircrafts.ContainsKey(HexAddress))
                return aircrafts[HexAddress];
            else
            {
                Aircraft a = new Aircraft(ICAOAddress);
                aircrafts.Add(HexAddress, a);
                return a;
            }
        }
    }
}
