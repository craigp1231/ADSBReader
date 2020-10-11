using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ADSBReader
{
    public unsafe class AdsbBitDecoder
    {
        //public event FrameReceivedDelegate FrameReceived;
        public delegate void ModeSMessageDelegate(ModeSMessage msg);
        public event ModeSMessageDelegate ModeSMessageReceived;

        private const int MODES_LONG_MSG_BITS = 112;
        private const int MODES_SHORT_MSG_BITS = 56;
        private const int MODES_PREAMBLE_US = 8;
        private const int MODES_ICAO_CACHE_TTL = 30;

        private const bool AggressiveMode = false;
        private const bool FixErrors = true;

        private List<RecentICAOItem> RecentICAOs = new List<RecentICAOItem>();

        private Dictionary<byte, int> frameTypeCounts = new Dictionary<byte, int>();
        private Dictionary<byte, int> bdsTypeCounts = new Dictionary<byte, int>();

        private ushort[] _magnitudeLookup;

        private const string IDENT_CHARS = "?ABCDEFGHIJKLMNOPQRSTUVWXYZ????? ???????????????0123456789??????";

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

        public AdsbBitDecoder()
        {
            // Initialise magitiude look up table
            // This is to speed up the process as it is assumed Round, Sqrt are expensive
            _magnitudeLookup = new ushort[129 * 129 * 2];
            byte i, q;
            for (i = 0; i <= 128; i++)
            {
                for (q = 0; q <= 128; q++)
                {
                    _magnitudeLookup[i * 129 + q] = (ushort)Math.Round(Math.Sqrt(i * i + q * q) * 360);
                }
            }
        }

        private void AddFrameTypeCount(byte sqtType)
        {
            if (frameTypeCounts.ContainsKey(sqtType))
            {
                frameTypeCounts[sqtType] = frameTypeCounts[sqtType] + 1;
            }
            else
                frameTypeCounts[sqtType] = 1;
        }

        private void AddBDSTypeCount(byte bdsType)
        {
            if (bdsTypeCounts.ContainsKey(bdsType))
            {
                bdsTypeCounts[bdsType] = bdsTypeCounts[bdsType] + 1;
            }
            else
                bdsTypeCounts[bdsType] = 1;
        }

        public void ProcessSamples(Complex* buf, int length)
        {
            ushort[] magnitudes = new ushort[length];

            for (int i = 0; i < length; i++)
            {
                magnitudes[i] = _magnitudeLookup[buf[i].Imag * 129 + buf[i].Real];
            }

            ProcessMagnitudes(magnitudes);
        }

        /* This function does not really correct the phase of the message, it just
         * applies a transformation to the first sample representing a given bit:
         *
         * If the previous bit was one, we amplify it a bit.
         * If the previous bit was zero, we decrease it a bit.
         *
         * This simple transformation makes the message a bit more likely to be
         * correctly decoded for out of phase messages:
         *
         * When messages are out of phase there is more uncertainty in
         * sequences of the same bit multiple times, since 11111 will be
         * transmitted as continuously altering magnitude (high, low, high, low...)
         * 
         * However because the message is out of phase some part of the high
         * is mixed in the low part, so that it is hard to distinguish if it is
         * a zero or a one.
         *
         * However when the message is out of phase passing from 0 to 1 or from
         * 1 to 0 happens in a very recognizable way, for instance in the 0 -> 1
         * transition, magnitude goes low, high, high, low, and one of of the
         * two middle samples the high will be *very* high as part of the previous
         * or next high signal will be mixed there.
         *
         * Applying our simple transformation we make more likely if the current
         * bit is a zero, to detect another zero. Symmetrically if it is a one
         * it will be more likely to detect a one because of the transformation.
         * In this way similar levels will be interpreted more likely in the
         * correct way. */
        private void applyPhaseCorrection(ref ushort[] m)
        {
            int j;

            //m += 16; /* Skip preamble. */
            for (j = 16; j < (MODES_LONG_MSG_BITS - 1) * 2; j += 2)
            {
                if (m[j] > m[j + 1])
                {
                    /* One */
                    m[j + 2] = (ushort)((m[j + 2] * 5) / 4);
                }
                else
                {
                    /* Zero */
                    m[j + 2] = (ushort)((m[j + 2] * 4) / 5);
                }
            }
        }

        /* Return -1 if the message is out of fase left-side
         * Return  1 if the message is out of fase right-size
         * Return  0 if the message is not particularly out of phase.
         *
         * Note: this function will access m[-1], so the caller should make sure to
         * call it only if we are not at the start of the current buffer. */
        private int detectOutOfPhase(ushort[] m)
        {
            if (m[4] > m[3] / 3) return 1;
            if (m[11] > m[10] / 3) return 1;
            if (m[7] > m[8] / 3) return -1;
            if (m[0] > m[2] / 3) return -1;
            return 0;
        }

        private void ProcessMagnitudes(ushort[] m)
        {
            ushort[] aux = new ushort[(MODES_LONG_MSG_BITS + MODES_PREAMBLE_US) * 2];

            ushort[] frameBits = new ushort[MODES_LONG_MSG_BITS * 2];
            bool use_correction = false;

            for (uint j = 0; j < m.Length - (MODES_LONG_MSG_BITS + MODES_PREAMBLE_US) * 2; j++)
            {
                //bool good_message = false;
                
                int high;

                // First pass without correction, second pass with correction (but doesnt have to be re-checked)
                if (!use_correction)
                {
                    /* First check of relations between the first 10 samples
                     * representing a valid preamble. We don't even investigate further
                     * if this simple test is not passed. */
                    if (!(m[j] > m[j + 1] &&
                        m[j + 1] < m[j + 2] &&
                        m[j + 2] > m[j + 3] &&
                        m[j + 3] < m[j] &&
                        m[j + 4] < m[j] &&
                        m[j + 5] < m[j] &&
                        m[j + 6] < m[j] &&
                        m[j + 7] > m[j + 8] &&
                        m[j + 8] < m[j + 9] &&
                        m[j + 9] > m[j + 6]))
                    {
                        // Unexpected ratio
                        continue;
                    }

                    /* The samples between the two spikes must be < than the average
                     * of the high spikes level. We don't test bits too near to
                     * the high levels as signals can be out of phase so part of the
                     * energy can be in the near samples. */
                    high = (m[j] + m[j + 2] + m[j + 7] + m[j + 9]) / 6;
                    if (m[j + 4] >= high ||
                        m[j + 5] >= high)
                    {
                        // Too high level in samples between 3 and 6
                        continue;
                    }

                    /* Similarly samples in the range 11-14 must be low, as it is the
                     * space between the preamble and real data. Again we don't test
                     * bits too near to high levels, see above. */
                    if (m[j + 11] >= high ||
                        m[j + 12] >= high ||
                        m[j + 13] >= high ||
                        m[j + 14] >= high)
                    {
                        // Too high level in samples between 10 and 15
                        continue;
                    }
                }

                // We now have a valid preamble, we copy the next LONG bits to an array for processing
                Array.Copy(m, j + MODES_PREAMBLE_US * 2, frameBits, 0, frameBits.Length);

                /* If the previous attempt with this message failed, retry using
                 * magnitude correction. */
                if (use_correction && j > 0)
                {
                    //memcpy(aux, m + j + MODES_PREAMBLE_US * 2, sizeof(aux));

                    Array.Copy(m, j - 1, aux, 0, aux.Length);

                    if (j > 0 && detectOutOfPhase(aux) != 0)
                    {
                        Array.Copy(m, j, aux, 0, aux.Length);
                        applyPhaseCorrection(ref aux);
                        //Modes.stat_out_of_phase++;
                        Array.Copy(aux, MODES_PREAMBLE_US * 2, frameBits, 0, frameBits.Length);
                    }
                }

                /* Decode all the next 112 bits, regardless of the actual message
                 * size. We'll check the actual message type later. */
                int low, i, errors, delta;
                byte[] bits = new byte[MODES_LONG_MSG_BITS];

                errors = 0;                
                for (i = 0; i < MODES_LONG_MSG_BITS * 2; i += 2)
                {
                    low = frameBits[i];
                    high = frameBits[i + 1];
                    delta = low - high;
                    if (delta < 0) delta = -delta;

                    if (i > 0 && delta < 256)
                    {
                        bits[i / 2] = bits[i / 2 - 1];
                    }
                    else if (low == high)
                    {
                        /* Checking if two adiacent samples have the same magnitude
                         * is an effective way to detect if it's just random noise
                         * that was detected as a valid preamble. */
                        bits[i / 2] = 2; /* error */
                        if (i < MODES_SHORT_MSG_BITS * 2) errors++;
                    }
                    else if (low > high)
                    {
                        bits[i / 2] = 1;
                    }
                    else
                    {
                        /* (low < high) for exclusion  */
                        bits[i / 2] = 0;
                    }
                }

                /* Pack bits into bytes */
                byte[] msg = new byte[MODES_LONG_MSG_BITS / 8];
                for (i = 0; i < MODES_LONG_MSG_BITS; i += 8)
                {
                    msg[i / 8] = (byte)
                        (bits[i] << 7 |
                        bits[i + 1] << 6 |
                        bits[i + 2] << 5 |
                        bits[i + 3] << 4 |
                        bits[i + 4] << 3 |
                        bits[i + 5] << 2 |
                        bits[i + 6] << 1 |
                        bits[i + 7]);
                }

                int msgtype = msg[0] >> 3;
                int msglen = modesMessageLenByType(msgtype) / 8;

                /* Last check, high and low bits are different enough in magnitude
                 * to mark this as real message and not just noise? */
                delta = 0;
                for (i = 0; i < msglen * 8 * 2; i += 2)
                {
                    delta += Math.Abs(m[j + i + MODES_PREAMBLE_US * 2] -
                                 m[j + i + MODES_PREAMBLE_US * 2 + 1]);
                }
                delta /= msglen * 4;

                /* Filter for an average delta of three is small enough to let almost
                 * every kind of message to pass, but high enough to filter some
                 * random noise. */
                if (delta < 10 * 255)
                {
                    use_correction = false;
                    continue;
                }

                if (errors == 0 || (AggressiveMode && errors <= 3))
                {
                    // Decode Mode S
                    ModeSMessage mmsg = null;

                    // Only copy bytes we need
                    byte[] bcpy = new byte[msglen];
                    Array.Copy(msg, bcpy, msglen);

                    if ((mmsg = DecodeModeSMessage(bcpy)) != null)
                    {
                        j += (uint)(MODES_PREAMBLE_US + (msglen * 8)) * 2;

                        if (ModeSMessageReceived != null)
                        {
                             ModeSMessageReceived(mmsg);
                        }
                            
                        continue;
                    }
                }

                if (!use_correction)
                {
                    use_correction = true;
                    j--;
                }
                else
                    use_correction = false;

                /*if (FrameReceived != null)
                    FrameReceived(msg, msg.Length);*/
            }
        }

        /* Given the Downlink Format (DF) of the message, return the message length
         * in bits. */
        public static int modesMessageLenByType(int type)
        {
            if (type == 16 || type == 17 ||
                type == 19 || type == 20 ||
                type == 21)
                return MODES_LONG_MSG_BITS;
            else
                return MODES_SHORT_MSG_BITS;
        }

        public ModeSMessage DecodeModeSMessage(byte[] msg)
        {
            ModeSMessage decodedMsg = null;

            DownlinkFormats fmt = (DownlinkFormats)(msg[0] >> 3);

            int msgbits = modesMessageLenByType((int)fmt);
            int errorbit = -1;          // No error

            uint crc = ((uint)(msg[msgbits / 8 - 3] << 16)) |
                       ((uint)(msg[msgbits / 8 - 2] << 8)) |
                       ((uint)(msg[msgbits / 8 - 1] << 0));
            bool crc_ok = ModeSCRC(msg) == crc;

            // Fix CRC errors, if we CRCs dont match
            if (!crc_ok && FixErrors && (fmt == DownlinkFormats.AllCallReply || fmt == DownlinkFormats.AdsbExtendedSquitter))
            {
                if ((errorbit = fixSingleBitErrors(ref msg)) != -1)
                {
                    crc = ModeSCRC(msg);
                    crc_ok = true;
                }
                else if (AggressiveMode && fmt == DownlinkFormats.AdsbExtendedSquitter && (errorbit = fixDoubleBitErrors(ref msg)) != -1)
                {
                    crc = ModeSCRC(msg);
                    crc_ok = true;
                }
            }

            // Aircraft ICAO address (24 bit)
            byte[] ICAO = new byte[3];
            Array.Copy(msg, 1, ICAO, 0, 3);

            if (fmt != DownlinkFormats.AllCallReply && fmt != DownlinkFormats.AdsbExtendedSquitter)
            {
                // Brute Force Aircraft address by XORing CRC
                uint wipcrc = ModeSCRC(msg);

                ICAO[0] = (byte)(((wipcrc >> 16) & 0xff) ^ msg[msgbits / 8 - 3]);
                ICAO[1] = (byte)(((wipcrc >> 8) & 0xff) ^ msg[msgbits / 8 - 2]);
                ICAO[2] = (byte)(((wipcrc >> 0) & 0xff) ^ msg[msgbits / 8 - 1]);

                if (isRecentICAO(ICAO))
                {
                    // We were able to recover the message
                    crc_ok = true;
                }
            }
            else
            {
                if (crc_ok && errorbit == -1)
                {
                    addRecentICAO(ICAO);
                }
            }

            if (!crc_ok) return decodedMsg;

            switch (fmt)
            {
                case DownlinkFormats.AdsbExtendedSquitter:
                {
                    byte sqtType = (byte)(msg[4] >> 3);
                    byte sqtSubType = (byte)(msg[4] & 7);

                    AddFrameTypeCount(sqtType);

                    if (sqtType >= 1 && sqtType <= 4)
                    {
                        byte category = (byte)(msg[4] & 7);

                        StringBuilder callsign = new StringBuilder();
                        callsign.Append(IDENT_CHARS[msg[5] >> 2]);
                        callsign.Append(IDENT_CHARS[((msg[5] & 3) << 4) | (msg[6] >> 4)]);
                        callsign.Append(IDENT_CHARS[((msg[6] & 15) << 2) | (msg[7] >> 6)]);
                        callsign.Append(IDENT_CHARS[msg[7] & 63]);
                        callsign.Append(IDENT_CHARS[msg[8] >> 2]);
                        callsign.Append(IDENT_CHARS[((msg[8] & 3) << 4) | (msg[9] >> 4)]);
                        callsign.Append(IDENT_CHARS[((msg[9] & 15) << 2) | (msg[10] >> 6)]);
                        callsign.Append(IDENT_CHARS[msg[10] & 63]);

                        AircraftEmitterCategorys aircraftCat = AircraftEmitterCategorys.NoCategoryA;

                        switch (sqtType)
                        {
                            case 4:
                                aircraftCat = (AircraftEmitterCategorys)category;
                                break;
                            case 3:
                                aircraftCat = (AircraftEmitterCategorys)(category + 7);
                                break;
                            case 2:
                                aircraftCat = (AircraftEmitterCategorys)(category + 14);
                                break;
                            case 1:
                                aircraftCat = AircraftEmitterCategorys.NoCategoryD;
                                break;
                        }

                        decodedMsg = new ModeSSquitterIdent(ICAO, aircraftCat, callsign.ToString().Trim());
                    }
                    else if (sqtType >= 9 && sqtType <= 18)
                    {
                        SurveillanceStatusCategory ssc = (SurveillanceStatusCategory)((msg[4] >> 1) & 3);
                        CPRType cprType = (CPRType)((msg[6] & (1 << 2)) >> 2);
                        bool TimeFlag = (byte)((msg[6] & (1 << 3)) >> 3) == 1;

                        int Altitude = -1;

                        // Altitude
                        bool qBit = (byte)(msg[5] & 1) == 1;
                        if (qBit)
                        {
                            // Altitude can be decoded
                            int n = ((msg[5] >> 1) << 4) | ((msg[6] & 0xF0) >> 4);
                            //a.Altitude = n * 25 - 1000;
                            Altitude = n * 25 - 1000;
                        }

                        int rawLat = ((msg[6] & 3) << 15) |
                                    (msg[7] << 7) |
                                    (msg[8] >> 1);
                        int rawLon = ((msg[8] & 1) << 16) |
                                     (msg[9] << 8) |
                                     msg[10];

                        decodedMsg = new ModeSAirbornePosition(ICAO, ssc, Altitude, TimeFlag, cprType, rawLat, rawLon);
                    }
                    else if (sqtType == 19)
                    {
                        if (sqtSubType == 1 || sqtSubType == 2)
                        {
                            int ew_dir = (msg[5] & 4) >> 2;
                            int ew_velocity = (((msg[5] & 3) << 8) | msg[6]) - 1;
                            int ns_dir = (msg[7] & 0x80) >> 7;
                            int ns_velocity = (((msg[7] & 0x7f) << 3) | ((msg[8] & 0xe0) >> 5)) - 1;
                            int vert_rate_source = (msg[8] & 0x10) >> 4;
                            int vert_rate_sign = (msg[8] & 0x8) >> 3;
                            int vert_rate = ((msg[8] & 7) << 6) | ((msg[9] & 0xfc) >> 2) -1;

                            if (sqtSubType == 2)
                            {
                                ew_velocity *= 4;
                                ns_velocity *= 4;
                            }

                            if (ew_dir == 1) ew_velocity *= -1;
                            if (ns_dir == 1) ns_velocity *= -1;
                            if (vert_rate_sign == 1) vert_rate *= -1;

                            vert_rate *= 64;

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
                            }

                            decodedMsg = new ModeSGroundSpeed(ICAO, (ushort)heading, (ushort)velocity, (short)vert_rate);
                        }
                        else if (sqtSubType == 3 || sqtSubType == 4)
                        {
                            double heading = (360.0 / 128) * (((msg[5] & 3) << 5) | (msg[6] >> 3));

                            int vert_rate_source = (msg[8] & 0x10) >> 4;
                            int vert_rate_sign = (msg[8] & 0x8) >> 3;
                            int vert_rate = (((msg[8] & 7) << 6) | ((msg[9] & 0xfc) >> 2)) - 1;

                            if (vert_rate > 0)
                            {
                                vert_rate = vert_rate * 64;
                                if (vert_rate_sign == 1) vert_rate *= -1;
                            }
                            else
                                vert_rate = -32476;

                            bool AirspeedTAS = ((msg[7] >> 7) & 1) == 1;

                            int airspeed = (((msg[7] & 127) << 3) | (msg[8] >> 5)) - 1;

                            if (sqtSubType == 4)
                                airspeed *= 4;

                            decodedMsg = new ModeSAirspeed(ICAO, (ushort)heading, (ushort)airspeed, (short)vert_rate);
                        }
                    }
                    else if (sqtType == 28)
                    {
                        if (sqtSubType == 1)
                        {
                            EmergencyStateEnum ese = (EmergencyStateEnum)(msg[5] >> 5);
                            int a, b, c, d;

                            // C1-A1-C2-A2-C4-A4-ZERO-B1-D1-B2-D2-B4-D4

                            a = ((msg[6] & 0x80) >> 5) |
                                ((msg[5] & 0x02) >> 0) |
                                ((msg[5] & 0x08) >> 3);
                            b = ((msg[6] & 0x02) << 1) |
                                ((msg[6] & 0x08) >> 2) |
                                ((msg[6] & 0x20) >> 5);
                            c = ((msg[5] & 0x01) << 2) |
                                ((msg[5] & 0x04) >> 1) |
                                ((msg[5] & 0x10) >> 4);
                            d = ((msg[6] & 0x01) << 2) |
                                ((msg[6] & 0x04) >> 1) |
                                ((msg[6] & 0x10) >> 4);

                            ushort sqk = (ushort)(a * 1000 + b * 100 + c * 10 + d);

                            decodedMsg = new ModeSEmergencyState(ICAO, ese, sqk);
                        }
                    }
                    else if (sqtType == 29)
                    {
                        sqtSubType = (byte)((msg[4] >> 1) & 3);
                        if (sqtSubType == 1)
                        {
                            bool isAltFMS = ((msg[5] >> 7) & 1) == 1;

                            // Negative alt is invalid
                            int? selAlt = ((msg[5] & 127) << 4) | (msg[6] >> 4);
                            selAlt--;
                            selAlt *= 32;

                            float pressure = ((msg[6] & 15) << 5) | (msg[7] >> 3);
                            pressure = pressure -2;
                            pressure *= 0.8f;

                            bool hdgValid = ((msg[7] >> 2) & 1) == 1;
                            bool hdgNegative = ((msg[7] >> 1) & 1) == 1;

                            float? heading = (msg[8] >> 1) | ((msg[7] & 1) << 7);
                            heading = heading * 180 / 256;

                            if (hdgNegative)
                                heading *= -1;

                            heading += 180;

                            if (selAlt < 0)
                                selAlt = null;

                            if (!hdgValid)
                                heading = null;

                            ushort? alt = selAlt == null ? null : (ushort?)RoundToNearest((double)selAlt, 100);
                            ushort? prs = pressure == null ? null : (ushort?)(RoundToNearest((double)pressure, 1) + 800);
                            ushort? hdg = heading == null ? null : (ushort?)RoundToNearest((double)heading, 1);

                            decodedMsg = new ModeSTargetState(ICAO, alt, prs, hdg);
                        }
                    }

                }
                break;
                case DownlinkFormats.AltitudeReply:
                {
                    int Altitude = -1;

                    // Altitude
                    bool qBit = ((msg[3] >> 1) & 1) == 1;
                    if (qBit)
                    {
                        // Altitude can be decoded
                        int n = ((msg[2] & 31) << 6) |
                                ((msg[3] & 0x80) >> 2) |
                                ((msg[3] & 0x20) >> 1) |
                                 (msg[3] & 15);
                        //a.Altitude = n * 25 - 1000;
                        Altitude = n * 25 - 1000;
                    }

                    decodedMsg = new ModeSCommBAlt(ICAO, Altitude);
                }
                break;
                case DownlinkFormats.CommBAltitudeReply:
                {
                    int Altitude = -1;

                    // Altitude
                    bool qBit = ((msg[3] >> 1) & 1) == 1;
                    if (qBit)
                    {
                        // Altitude can be decoded
                        int n = ((msg[2] & 31) << 6) |
                                ((msg[3] & 0x80) >> 2) |
                                ((msg[3] & 0x20) >> 1) |
                                 (msg[3] & 15); 
                        //a.Altitude = n * 25 - 1000;
                        Altitude = n * 25 - 1000;
                    }

                    ModeSCommBAlt ms = new ModeSCommBAlt(ICAO, Altitude);

                    byte[] mb = new byte[7];
                    Array.Copy(msg, 4, mb, 0, 7);
                    ms.SecondaryMessage = DecodeMBField(ICAO, mb);

                    decodedMsg = ms;
                }
                break;
                case DownlinkFormats.CommBIdentReply:
                {
                    int a, b, c, d;

                    a = ((msg[3] & 0x80) >> 5) |
                        ((msg[2] & 0x02) >> 0) |
                        ((msg[2] & 0x08) >> 3);
                    b = ((msg[3] & 0x02) << 1) |
                        ((msg[3] & 0x08) >> 2) |
                        ((msg[3] & 0x20) >> 5);
                    c = ((msg[2] & 0x01) << 2) |
                        ((msg[2] & 0x04) >> 1) |
                        ((msg[2] & 0x10) >> 4);
                    d = ((msg[3] & 0x01) << 2) |
                        ((msg[3] & 0x04) >> 1) |
                        ((msg[3] & 0x10) >> 4);
                    string sqk = string.Format("{0:0000}", a * 1000 + b * 100 + c * 10 + d);

                    ModeSIdent ms = new ModeSIdent(ICAO, sqk);

                    byte[] mb = new byte[7];
                    Array.Copy(msg, 4, mb, 0, 7);
                    ms.SecondaryMessage = DecodeMBField(ICAO, mb);

                    decodedMsg = ms;
                }
                break;
            }

            return decodedMsg;
        }

        private ModeSMessage DecodeMBField(byte[] ICAO, byte[] msg)
        {
            //AddBDSTypeCount(msg[0]);

            if (msg[0] == 0x20)
            {
                // Callsign
                StringBuilder callsign = new StringBuilder();
                callsign.Append(IDENT_CHARS[msg[1] >> 2]);
                callsign.Append(IDENT_CHARS[((msg[1] & 3) << 4) | (msg[2] >> 4)]);
                callsign.Append(IDENT_CHARS[((msg[2] & 15) << 2) | (msg[3] >> 6)]);
                callsign.Append(IDENT_CHARS[msg[3] & 63]);
                callsign.Append(IDENT_CHARS[msg[4] >> 2]);
                callsign.Append(IDENT_CHARS[((msg[4] & 3) << 4) | (msg[5] >> 4)]);
                callsign.Append(IDENT_CHARS[((msg[5] & 15) << 2) | (msg[6] >> 6)]);
                callsign.Append(IDENT_CHARS[msg[6] & 63]);

                string cs = callsign.ToString().Trim();

                return new ModeSSquitterIdent(ICAO, AircraftEmitterCategorys.NoCategoryD, cs);
            }
            else if ((msg[5] >> 1) == 0 && (msg[4] & 1) == 0)
            {
                // Assume this is a target state
                int selalt = ((msg[0] & 127) << 5) | (msg[1] >> 3);
                selalt *= 16;

                selalt = RoundToNearest(selalt, 100);

                float pressure = ((((msg[3] & 0x1f) << 7) | (msg[4] >> 1)) / 10) +800;

                // Valid if alt is devisible by 1000, this is a fail safe as we dont know for sure this is a target state parket
                if (selalt % 1000 == 0 && selalt > 0)
                    return new ModeSTargetState(ICAO, (ushort?)selalt, (ushort?)pressure, null);
            }
            else if ((msg[1] & 8) == 8)
            {
                // attempt to decode indicated airspeed

                int ias = (msg[1] & 7) << 7 | (msg[2] >> 1);

                return new ModeSIAS(ICAO, (ushort)ias);
            }

            return null;
        }

        private int RoundToNearest(double num, int roundTo)
        {
            return (int)Math.Round(num / roundTo) * roundTo;
        }

        private void addRecentICAO(byte[] ICAO)
        {
            int iICAO = (ICAO[0] << 16) | (ICAO[1] << 8) | ICAO[2];

            var item = (from c in RecentICAOs
                        where c.ICAOInt == iICAO
                        select c).FirstOrDefault();

            if (item != null)
                item.UpdateLastSeen();
            else
            {
                RecentICAOs.Add(new RecentICAOItem(ICAO));
            }
        }

        private bool isRecentICAO(byte[] ICAO)
        {
            int iICAO = (ICAO[0] << 16) | (ICAO[1] << 8) | ICAO[2];

            var item = (from c in RecentICAOs
                        where c.ICAOInt == iICAO
                        select c).FirstOrDefault();

            if (item != null && (DateTime.Now - item.LastSeen).TotalSeconds <= MODES_ICAO_CACHE_TTL)
            {
                return true;
            }

            return false;
        }

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

        private int fixSingleBitErrors(ref byte[] frame)
        {
            int j;
            byte[] aux = new byte[frame.Length];

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
            byte[] aux = new byte[frame.Length];

            for (j = 0; j < frame.Length * 8; j++)
            {
                int byteNum1 = j / 8;
                int bitMask1 = 1 << (7 - (j % 8));

                for (i = j + 1; i < frame.Length * 8; i++)
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
    }
}
