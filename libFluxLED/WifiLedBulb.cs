using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;


namespace FluxLED
{
    public class WifiLedBulb : IDisposable
    {
        private IPAddress m_ipAddress;
        private Socket m_socket;

        private bool m_useChecksum = true;
        private int m_timeout = 5000;
        private int m_queryLen = 0;
        private bool m_rgbwprotocol = false;
        private byte[] m_rawState;

        private enum LedProtocolType
        {
            kLedNet_9byte,
            kLedNet_8byte,
            kLedNet_Original
        }
        private LedProtocolType m_protocol = LedProtocolType.kLedNet_8byte;

        private enum LedMode
        {
            kUnknown,
            kColor,
            kWW,
            kCustom,
            kPreset,
            kSunrise,
            kSunset
        }
        private LedMode m_mode;

        private const int WIFI_PORT = 5577;
        

        private static readonly byte[] NEW_QUERY_MSG = { 0x81, 0x8a, 0x8b };
        private static readonly byte[] OLD_QUERY_MSG = { 0xef, 0x01, 0x77 };

        private static readonly byte[] OLD_ON_MSG = { 0xcc, 0x23, 0x33 };
        private static readonly byte[] NEW_ON_MSG = { 0x71, 0x23, 0x0f };

        private static readonly byte[] OLD_OFF_MSG = { 0xcc, 0x24, 0x33 };
        private static readonly byte[] NEW_OFF_MSG = { 0x71, 0x24, 0x0f };

        private static readonly byte[] GET_CLOCK_MSG = { 0x11, 0x1a, 0x1b, 0x0f };
        private static readonly byte[] GET_TIMERS_MSG = { 0x22, 0x2a, 0x2b, 0x0f };

        private static byte COLOR_ONLY_WRITEMASK = 0xf0;
        private static byte WHITE_ONLY_WRITEMASK = 0x0f;
        private static byte COLOR_AND_WHITE_WRITEMASK = 0x00;

        public string ID
        {
            private set;
            get;
        }

        public string Model
        {
            private set;
            get;
        }

        public bool IsOn
        {
            private set;
            get;
        }

        public bool RGBWCapable
        {
            private set;
            get;
        }

        public byte Brightness
        {
            get
            {
                if (m_mode == LedMode.kWW)
                    return m_rawState[9];

                return GetRGBColor().BrightnessByte;
            }
        }

        public bool Connected
        {
            get { return m_socket.Connected; }
        }

        public WifiLedBulb(string ip_address, string id, string model)
        {
            m_ipAddress = IPAddress.Parse(ip_address);
            ID = id;
            Model = model;
            m_socket = new Socket(m_ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        public void Connect()
        {
            if (m_socket.Connected)
            {
                m_socket.Close();
                m_socket = new Socket(m_ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            }

            m_socket.Connect(m_ipAddress, WIFI_PORT);
        }

        public void UpdateState(int retries = 2)
        {
            if (!m_socket.Connected)
                Connect();
            byte[] state = QueryState(retries);
            // typical response:
            // pos  0  1  2  3  4  5  6  7  8  9 10
            //    66 01 24 39 21 0a ff 00 00 01 99
            //     |  |  |  |  |  |  |  |  |  |  |
            //     |  |  |  |  |  |  |  |  |  |  checksum
            //     |  |  |  |  |  |  |  |  |  warmwhite
            //     |  |  |  |  |  |  |  |  blue
            //     |  |  |  |  |  |  |  green 
            //     |  |  |  |  |  |  red
            //     |  |  |  |  |  speed: 0f = highest f0 is lowest
            //     |  |  |  |  <don't know yet>
            //     |  |  |  preset pattern             
            //     |  |  off(23)/on(24)
            //     |  type
            // msg head
            //        

            // response from a 5-channel LEDENET controller:
            // pos  0  1  2  3  4  5  6  7  8  9 10 11 12 13
            //    81 25 23 61 21 06 38 05 06 f9 01 00 0f 9d
            //     |  |  |  |  |  |  |  |  |  |  |  |  |  |
            //     |  |  |  |  |  |  |  |  |  |  |  |  |  checksum
            //     |  |  |  |  |  |  |  |  |  |  |  |  color mode (f0 colors were set, 0f whites, 00 all were set)
            //     |  |  |  |  |  |  |  |  |  |  |  cold-white
            //     |  |  |  |  |  |  |  |  |  |  <don't know yet>
            //     |  |  |  |  |  |  |  |  |  warmwhite
            //     |  |  |  |  |  |  |  |  blue
            //     |  |  |  |  |  |  |  green
            //     |  |  |  |  |  |  red
            //     |  |  |  |  |  speed: 0f = highest f0 is lowest
            //     |  |  |  |  <don't know yet>
            //     |  |  |  preset pattern
            //     |  |  off(23)/on(24)
            //     |  type
            // msg head
            //

            // Devices that don't require a separate rgb/w bit
            if (state[1] == 0x04 ||
                state[1] == 0x33 ||
                state[1] == 0x81)
                m_rgbwprotocol = true;

            //Devices that actually support rgbw
            if (state[1] == 0x04 ||
                state[1] == 0x25 ||
                state[1] == 0x33 ||
                state[1] == 0x81)
                RGBWCapable = true;

            //Devices that use an 8-byte protocol
            if (state[1] == 0x25 ||
                state[1] == 0x27 ||
                state[1] == 0x35)
                m_protocol = LedProtocolType.kLedNet_9byte;

            //Devices that use the original LEDENET protocol
            if(state[1] == 0x01)
            {
                m_protocol = LedProtocolType.kLedNet_Original;
                m_useChecksum = false;
            }

            byte pattern = state[3];
            byte ww_level = state[9];
            m_mode = DetermineMode(ww_level, pattern);
            if(m_mode == LedMode.kUnknown)
            {
                if (retries < 1)
                    throw new UnknownModeException();
                UpdateState(retries - 1);
                return;
            }

            byte power_state = state[2];
            if(power_state == 0x23)
            {
                IsOn = true;
            }
            else if(power_state == 0x24)
            {
                IsOn = false;
            }

            m_rawState = state;
        }

        public void TurnOn()
        {
            SendMsg(GetOnOffMessage(true));
            IsOn = true;
        }

        public void TurnOff()
        {
            SendMsg(GetOnOffMessage(false));
            IsOn = false;
        }

        public RGBColor GetRGBColor()
        {
            if (m_mode != LedMode.kColor)
                return new RGBColor(255, 255, 255);
            else
                return new RGBColor(m_rawState[6], m_rawState[7], m_rawState[8]);
        }

        public WhiteColor GetWhiteColor()
        {
            if (m_mode != LedMode.kColor)
                return new WhiteColor(255, 255);

            return new WhiteColor(m_rawState[9], m_rawState[10]);
        }

        private byte[] GetColorSetMsg(bool persist, byte write_mask)
        {
            byte[] msg;
            int write_mask_idx;
            //TODO: There's also an 9 byte protocol (8 bytes of data and 1 checksum),
            //but it's not clear how to detect that, so it's not supported for now
            if (m_protocol == LedProtocolType.kLedNet_8byte)
            {
                msg = new byte[7];
                write_mask_idx = 5;
            }
            else if(m_protocol == LedProtocolType.kLedNet_9byte)
            {
                msg = new byte[8];
                write_mask_idx = 6;
            }
            else
            {
                throw new InvalidOperationException("This LED Protocol does not suport this function");
            }

            if (persist)
                msg[0] = 0x31;
            else
                msg[0] = 0x41;

            if(!m_rgbwprotocol)
                msg[write_mask_idx] = write_mask; //write mask
            msg[write_mask_idx + 1] = 0x0f; //terminator

            return msg;
        }

        public void SetRGB(RGBColor color, bool persist = true)
        {
            var bcolor = color.AsBytes();
            byte[] msg;
            if (m_protocol == LedProtocolType.kLedNet_Original)
            {
                msg = new byte[5];
                msg[0] = 0x56;
                msg[1] = bcolor.r;
                msg[2] = bcolor.g;
                msg[3] = bcolor.b;
                msg[4] = 0xaa;
            }
            else
            {
                msg = GetColorSetMsg(persist, COLOR_ONLY_WRITEMASK);
                msg[1] = bcolor.r;
                msg[2] = bcolor.g;
                msg[3] = bcolor.b;
            }

            SendMsg(msg);
            UpdateState();
        }

        public void SetWhite(WhiteColor white, bool persist = true)
        {
            if (m_protocol == LedProtocolType.kLedNet_Original)
                throw new InvalidOperationException("This light does not support Warm White settings");

            var msg = GetColorSetMsg(persist, WHITE_ONLY_WRITEMASK);

            msg[1] = 0x0;
            msg[2] = 0x0;
            msg[3] = 0x0;

            msg[4] = white.WarmWhite;

            if (m_protocol == LedProtocolType.kLedNet_9byte)
                msg[5] = white.ColdWhite;

            SendMsg(msg);

            UpdateState();
        }

        public void SetRGBW(RGBColor color, WhiteColor white, bool persist = true)
        {
            if (m_protocol == LedProtocolType.kLedNet_Original)
                throw new InvalidOperationException("Bulb does not support Warm White settings");
            if (!m_rgbwprotocol)
                throw new InvalidOperationException("Bulb does not support setting RGB and WW simultaneously");

            var bcolor = color.AsBytes();
            var msg = GetColorSetMsg(persist, COLOR_AND_WHITE_WRITEMASK);

            msg[1] = bcolor.r;
            msg[2] = bcolor.g;
            msg[3] = bcolor.b;

            msg[4] = white.WarmWhite;

            if(m_protocol == LedProtocolType.kLedNet_9byte)
            {
                msg[5] = white.ColdWhite;
            }

            SendMsg(msg);

            UpdateState();
        }

        public DateTime GetClock()
        {
            SendMsg(GET_CLOCK_MSG);
            byte[] response = ReadMsg(12);
            return new DateTime(2000 + response[3], response[4], response[5], response[6], response[7], response[8]);
        }

        public void SetClock(DateTime dateTime)
        {
            byte[] msg = new byte[11];
            msg[0] = 0x10;
            msg[1] = 0x14;
            msg[2] = (byte)(dateTime.Year - 2000);
            msg[3] = (byte)dateTime.Month;
            msg[4] = (byte)dateTime.Day;
            msg[5] = (byte)dateTime.Hour;
            msg[6] = (byte)dateTime.Minute;
            msg[7] = (byte)dateTime.Second;
            msg[8] = (dateTime.DayOfWeek == DayOfWeek.Sunday ? (byte)7 : (byte)dateTime.DayOfWeek);
            msg[9] = 0x00;
            msg[10] = 0x0f;
            SendMsg(msg);
        }

        public void SetPresetPattern(PresetPattern pattern, int speed)
        {
            byte[] pattern_msg = new byte[4];
            pattern_msg[0] = 0x61;
            pattern_msg[1] = (byte)pattern;
            pattern_msg[2] = Utils.ConvertSpeedToDelay(speed);
            pattern_msg[3] = 0x0f;

            SendMsg(pattern_msg);
            UpdateState();
        }

        public LedTimer[] GetTimers()
        {
            SendMsg(GET_TIMERS_MSG);
            int resp_len = 88;
            byte[] response = ReadMsg(88);

            if (response.Length < resp_len)
                throw new Exception("Response too short");

            LedTimer[] timers = new LedTimer[6];
            byte[] timer_bytes = new byte[14];

            for (int i = 0; i < 6; ++i)
            {
                Array.Copy(response, (i * 14) + 2, timer_bytes, 0, 14);
                timers[i] = LedTimer.FromBytes(timer_bytes);
            }

            return timers;
        }

        public void SendTimers(List<LedTimer> timers)
        {
            //Can't have more than 6 active timers
            if (timers.Count(t => t.IsActive) > 6)
                throw new ArgumentException("More than 6 active timers specified to send", "timers");

            //Make sure we have at least 6 timers
            while (timers.Count() < 6)
            {
                timers.Add(new TurnOffLedTimer());
            }

            //Move active timers to front of the list
            var ordered_timers = timers.OrderBy(t => t, Comparer<LedTimer>.Create((x, y) => x.IsActive ? -1 : y.IsActive ? 1 : 0));

            //Each timer is 14 bytes, +1 start byte and +2 end bytes for the buffer
            var msg = new byte[(6 * 14) + 3];

            //Start byte
            msg[0] = 0x21;

            var idx = 1;
            foreach(var t in timers.Take(6))
            {
                
                byte[] timer_bytes = t.ToBytes();
                Array.Copy(timer_bytes, 0, msg, idx, 14);
                idx += 14;
            }

            //End bytes
            msg[idx] = 0x00;
            msg[idx + 1] = 0xf0;

            SendMsg(msg);

            //not sure what the resp is, prob some sort of ack?
            ReadMsg(1);
            ReadMsg(3);
        }

        public void Dispose()
        {
            if(m_socket.Connected)
            {
                m_socket.Close();
            }
            m_socket.Dispose();
        }

        private void SendMsg(byte[] bytes)
        {
            if(m_useChecksum)
            {
                int sum = bytes.Sum(x => (int)x);
                byte checksum = (byte)sum;
                Array.Resize(ref bytes, bytes.Length + 1);
                bytes[bytes.Length - 1] = checksum;
            }

            m_socket.Send(bytes);
        }

        private byte[] ReadMsg(int expected)
        {
            byte[] buffer = new byte[expected];
            m_socket.ReceiveTimeout = m_timeout;
            int bytes_read = m_socket.Receive(buffer);
            Array.Resize(ref buffer, bytes_read);
            return buffer;
        }

        private void DetermineQueryLength(int retries)
        {
            SendMsg(NEW_QUERY_MSG);
            var response = ReadMsg(2);
            if(response.Length == 2)
            {
                m_queryLen = 14;
                return;
            }

            SendMsg(OLD_QUERY_MSG);
            response = ReadMsg(2);
            if (response.Length >= 2)
            {
                if(response[1] == 0x1)
                {
                    m_protocol = LedProtocolType.kLedNet_Original;
                    m_useChecksum = false;
                    m_queryLen = 11;
                    return;
                }
                else
                {
                    m_useChecksum = true;
                }
            }
            if (retries > 0)
            {
                DetermineQueryLength(retries - 1);
                return;
            }
            throw new UnknownProtocolException("Unable to determine protocol.");
        }

        private byte[] GetQueryMsg()
        {
            if (m_protocol == LedProtocolType.kLedNet_Original)
                return OLD_QUERY_MSG;
            else
                return NEW_QUERY_MSG;
        }

        private byte[] QueryState(int retries)
        {
            if (m_queryLen == 0)
                DetermineQueryLength(2);

            try
            {
                Connect();
                SendMsg(GetQueryMsg());
                return ReadMsg(m_queryLen);
            }
            catch (SocketException)
            {
                if(retries < 1)
                {
                    IsOn = false;
                    return null;
                }
                return QueryState(retries - 1);
            }
        }

        private LedMode DetermineMode(byte ww_level, byte pattern_code)
        {
            LedMode mode = LedMode.kUnknown;
            if(pattern_code == 0x61 || pattern_code == 0x62)
            {
                if (RGBWCapable)
                    mode = LedMode.kColor;
                else if (ww_level != 0)
                    mode = LedMode.kWW;
                else
                    mode = LedMode.kColor;
            }
            else if(pattern_code == 0x60)
            {
                mode = LedMode.kCustom;
            }
            else if(pattern_code == 0x41)
            {
                mode = LedMode.kColor;
            }
            else if(Enum.IsDefined(typeof(PresetPattern), pattern_code))
            {
                mode = LedMode.kPreset;
            }
            else if(Enum.IsDefined(typeof(BuiltInTimer), pattern_code))
            {
                if (pattern_code == (byte)BuiltInTimer.Sunrise)
                    mode = LedMode.kSunrise;
                if (pattern_code == (byte)BuiltInTimer.Sunset)
                    mode = LedMode.kSunset;
            }
            return mode;
        }

        private byte[] GetOnOffMessage(bool turn_on)
        {
            if(m_protocol == LedProtocolType.kLedNet_Original)
            {
                return turn_on ? OLD_ON_MSG : OLD_OFF_MSG;
            }
            else
            {
                return turn_on ? NEW_ON_MSG : NEW_OFF_MSG;
            }
        }
    }
}
