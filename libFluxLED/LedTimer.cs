using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FluxLED
{
    public class LedTimer
    {
        public bool IsActive
        {
            get; set;
        }

        public enum Days : byte
        {
            None = 0x0,
            Monday = 0x2,
            Tuesday = 0x4,
            Wednesday = 0x8,
            Thursday = 0x10,
            Friday = 0x20,
            Saturday = 0x40,
            Sunday = 0x80,
            Everyday = Monday | Tuesday | Wednesday | Thursday | Friday | Saturday | Sunday,
            Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
            Weekend = Saturday | Sunday
        }

        public Days RepeatDays
        {
            get; set;
        }

        public DateTime Schedule
        {
            get; set;
        }

        public bool TurnOn
        {
            get; set;
        } = true;

        public static LedTimer FromBytes(byte[] bytes)
        {
            LedTimer timer;
            if (bytes[13] == 0x0f)
                return new TurnOffLedTimer(bytes);
            
            byte pattern_code = bytes[8];
            switch (pattern_code)
            {
                case 0x00:
                    timer = new LedTimer(bytes);
                    break;
                case 0x61:
                    timer = new ColorLedTimer(bytes);
                    break;
                case 0xA1:
                    timer = new BuiltinTimerLedTimer(bytes);
                    break;
                case 0xA2:
                    timer = new BuiltinTimerLedTimer(bytes);
                    break;
                default:
                    {
                        if (Enum.IsDefined(typeof(PresetPattern), pattern_code))
                        {
                            timer = new PresetPatternLedTimer(bytes);
                        }
                        else if(bytes[12] != 0)
                        {
                            timer = new WarmWhiteLedTimer(bytes);
                        }
                        else
                        {
                            //TODO: Custom exception
                            throw new Exception();
                        }
                    }
                    break;
            }
            return timer;
        }

        protected LedTimer()
        {
            IsActive = false;

        }

        protected LedTimer(byte[] bytes)
        {
            /* timer are in six 14 - byte structs
               f0 0f 08 10 10 15 00 00 25 1f 00 00 00 f0 0f
               0  1  2  3  4  5  6  7  8  9 10 11 12 13 14
               0: f0 when active entry/ 0f when not active
               1: (0f = 15) year when no repeat, else 0
               2:  month when no repeat, else 0
               3:  dayofmonth when no repeat, else 0
               4: hour
               5: min
               6: 0
               7: repeat mask, Mo = 0x2, Tu = 0x04, We 0x8, Th = 0x10 Fr = 0x20, Sa = 0x40, Su = 0x80
               8:  61 for solid color or warm, or preset pattern code
               9:  r(or delay for preset pattern)
               10: g
               11: b
               12: warm white level
               13: 0f = off, f0 = on ?
           */

            if (bytes[0] == 0xf0)
                IsActive = true;
            else
                IsActive = false;

            Schedule = new DateTime(2000 + bytes[1], bytes[2], bytes[3], bytes[4], bytes[5], 0);
            RepeatDays = (Days)bytes[7];
        }

        public virtual byte[] ToBytes()
        {
            byte[] bytes = new byte[14];

            if (IsActive)
                bytes[0] = 0xf0;
            else
                bytes[0] = 0x0f;

            if(RepeatDays == Days.None)
            {
                bytes[1] = (byte)(Schedule.Year - 2000);
                bytes[2] = (byte)Schedule.Month;
                bytes[3] = (byte)Schedule.Day;
                bytes[4] = (byte)Schedule.Hour;
                bytes[5] = (byte)Schedule.Minute;
            }
            else
            {
                bytes[7] = (byte)RepeatDays;
            }

            bytes[13] = 0xf0; //Turn on light

            return bytes;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if (IsActive)
                sb.Append("[ACTIVE  ] ");
            else
                sb.Append("[INACTIVE] ");

            sb.AppendFormat("{0}:{1} ", Schedule.Hour.ToString("D2"), Schedule.Minute.ToString("D2"));

            if(RepeatDays == Days.None)
            {
                sb.AppendFormat("Once: {0}-{1}-{2} ", Schedule.Year.ToString("D4"), Schedule.Month.ToString("D2"), Schedule.Day.ToString("D2"));
            }
            else
            {
                sb.AppendFormat("Repeats {0} ", RepeatDays.ToString());
            }

            return sb.ToString();
        }
    }

    public class ColorLedTimer : LedTimer
    {
        public RGBColor Color
        {
            get; set;
        }

        public ColorLedTimer(RGBColor color)
        {
            Color = color;
        }

        internal ColorLedTimer(byte[] bytes) : base(bytes)
        {
            Color = new RGBColor(bytes[9], bytes[10], bytes[11]);
        }

        public override string ToString()
        {
            return base.ToString() + string.Format("Color: {0}", Color.ToString());
        }

        public override byte[] ToBytes()
        {
            byte[] bytes = base.ToBytes();
            RGBColor.RGBColorBytes colorBytes = Color.AsBytes();
            bytes[9] = colorBytes.r;
            bytes[10] = colorBytes.g;
            bytes[11] = colorBytes.b;
            return bytes;
        }
    }

    public class BuiltinTimerLedTimer : LedTimer
    {
        public BuiltInTimer BuiltInTimer
        {
            get; set;
        }

        public byte Duration
        {
            get; set;
        }

        private byte m_brightnessStart;
        public int BrightnessStart
        {
            get { return Utils.ConvertByteToPercent(m_brightnessStart); }
            set { m_brightnessStart = Utils.ConvertPercentToByte(value); }
        }

        private byte m_brightnessEnd;
        public int BrightnessEnd
        {
            get { return Utils.ConvertByteToPercent(m_brightnessEnd); }
            set { m_brightnessEnd = Utils.ConvertPercentToByte(value); }
        }

        public BuiltinTimerLedTimer()
        {
            BuiltInTimer = BuiltInTimer.Sunrise;
        }

        internal BuiltinTimerLedTimer(byte[] bytes) : base(bytes)
        {
            BuiltInTimer = (FluxLED.BuiltInTimer)bytes[8];
            Duration = bytes[9];
            m_brightnessStart = bytes[10];
            m_brightnessEnd = bytes[11];
        }

        public override byte[] ToBytes()
        {
            byte[] bytes = base.ToBytes();
            bytes[8] = (byte)BuiltInTimer;
            bytes[9] = Duration;
            bytes[10] = m_brightnessStart;
            bytes[11] = m_brightnessEnd;
            return bytes;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(base.ToString());
            sb.AppendFormat("{0} (Duration:{1} minutes, Brightness: {2}% -> {3}%)",
                BuiltInTimer,
                Duration,
                BrightnessStart,
                BrightnessEnd);
            return sb.ToString();
        }
    }

    public class PresetPatternLedTimer : LedTimer
    {
        public PresetPattern Pattern
        {
            get; set;
        }

        private byte m_delay;
        public int Speed
        {
            get { return Utils.ConvertDelayToSpeed(m_delay); }
            set { m_delay = Utils.ConvertSpeedToDelay(value); }
        }

        public PresetPatternLedTimer()
        {
            Pattern = PresetPattern.seven_color_cross_fade;
            Speed = 30;
        }

        internal PresetPatternLedTimer(byte[] bytes) : base(bytes)
        {
            Pattern = (PresetPattern)bytes[8];
            m_delay = bytes[9];
        }

        public override byte[] ToBytes()
        {
            byte[] bytes = base.ToBytes();
            bytes[8] = (byte)Pattern;
            bytes[9] = m_delay;
            return bytes;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(base.ToString());
            sb.AppendFormat("{0} (Speed:{1}%)", Pattern, Speed);
            return sb.ToString();
        }
    }

    public class WarmWhiteLedTimer : LedTimer
    {
        private byte m_warmthLevel;
        public int WarmthLevel
        {
            get { return Utils.ConvertByteToPercent(m_warmthLevel); }
            set { m_warmthLevel = Utils.ConvertPercentToByte(value); }
        }

        public WarmWhiteLedTimer()
        {
            WarmthLevel = 50;
        }

        internal WarmWhiteLedTimer(byte[] bytes) : base(bytes)
        {
            WarmthLevel = bytes[12];
        }

        public override byte[] ToBytes()
        {
            byte[] bytes = base.ToBytes();
            bytes[12] = m_warmthLevel;
            return bytes;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(base.ToString());
            sb.AppendFormat("Warm White: {0}%", WarmthLevel);
            return sb.ToString();
        }
    }

    public class TurnOffLedTimer : LedTimer
    {
        public TurnOffLedTimer()
        {

        }

        internal TurnOffLedTimer(byte[] bytes) : base(bytes)
        {
            //intentionally left blank
        }

        public override byte[] ToBytes()
        {
            byte[] bytes = base.ToBytes();
            bytes[13] = 0x0f;
            return bytes;
        }

        public override string ToString()
        {
            return base.ToString() + "Turn Off";
        }
    }
}
