using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FluxLED
{
    //Based on python colorsys module as implemented here: https://github.com/kwlzn/python-sources/blob/master/Python-3.2.2/Lib/colorsys.py
    public struct RGBColor
    {
        public float r;
        public float g;
        public float b;

        public float Brightness
        {
            get
            {
                //flux_led calculates this using colorsys.rgb_to_hsv and then
                //just returning the v. Looking at the colorsys implementation code,
                //The v value is just the max of the r, g, and b values, so
                //we shortcut here and just return the max of r, g, and b.
                return Math.Max(Math.Max(r, g), b);
            }

            set
            {
                HSVColor hsv = AsHSV();
                hsv.v = value;
                RGBColor rgb = hsv.AsRGB();
                r = rgb.r;
                g = rgb.g;
                b = rgb.b;
            }
        }

        public byte BrightnessByte
        {
            get
            {
                return (byte)(Brightness * 255.0f);
            }

            set
            {
                Brightness = ((float)value / 255.0f);
            }
        }

        public RGBColor(float red, float green, float blue)
        {
            r = red;
            g = green;
            b = blue;
        }

        public RGBColor(byte red, byte green, byte blue)
        {
            r = red / 255.0f;
            g = green / 255.0f;
            b = blue / 255.0f;
        }

        public RGBColor(RGBColorBytes color) : this(color.r, color.g, color.b)
        {
        }

        public struct RGBColorBytes
        {
            public byte r;
            public byte g;
            public byte b;

            public RGBColorBytes(byte red, byte green, byte blue)
            {
                r = red;
                g = green;
                b = blue;
            }
        }

        public RGBColorBytes AsBytes()
        {
            return new RGBColorBytes((byte)(r * 255.0f), (byte)(g * 255.0f), (byte)(b * 255.0f));
        }

        internal HSVColor AsHSV()
        {
            float[] carray = new float[] { r, g, b };
            float maxc = carray.Max();
            float minc = carray.Min();

            float v = maxc;
            if (minc == maxc)
                return new HSVColor(0.0f, 0.0f, v);

            float s = (maxc - minc) / maxc;
            float rc = (maxc - r) / (maxc - minc);
            float gc = (maxc - g) / (maxc - minc);
            float bc = (maxc - b) / (maxc - minc);

            float h;
            if (r == maxc)
                h = bc - gc;
            else if (g == maxc)
                h = 2.0f + rc - bc;
            else
                h = 4.0f + gc - rc;

            h = (h / 6.0f) % 1.0f;

            return new HSVColor(h, s, v);
        }
    }

    class HSVColor
    {
        public float h;
        public float s;
        public float v;

        public HSVColor(float hue, float saturation, float value)
        {
            h = hue;
            s = saturation;
            v = value;
        }

        public HSVColor(byte hue, byte saturation, byte value)
        {
            h = hue / 255.0f;
            s = saturation / 255.0f;
            v = value / 255.0f;
        }

        public Tuple<byte, byte, byte> AsBytes()
        {
            return new Tuple<byte, byte, byte>((byte)(h * 255.0f), (byte)(s * 255.0f), (byte)(v * 255.0f));
        }

        public RGBColor AsRGB()
        {
            if (s == 0)
                return new RGBColor(v, v, v);

            int i = (int)(h * 6.0f);
            float f = (h * 6.0f) - i;
            float p = v * (1.0f - s);
            float q = v * (1.0f - s * f);
            float t = v * (1.0f - s * (1.0f - f));
            i = i % 6;
            switch (i)
            {
                case 0:
                    return new RGBColor(v, t, p);
                case 1:
                    return new RGBColor(q, v, p);
                case 2:
                    return new RGBColor(p, v, t);
                case 3:
                    return new RGBColor(p, q, v);
                case 4:
                    return new RGBColor(t, p, v);
                case 5:
                    return new RGBColor(v, p, q);
                default:
                    throw new InvalidOperationException("Unreachable code");
            }
        }
    }

    public struct WhiteColor
    {
        private byte m_warmWhite;
        public byte WarmWhite
        {
            get { return m_warmWhite; }
            set
            {
                m_warmWhite = value;
                if(ColdWhite == 0)
                {
                    ColdWhite = value;
                }
            }
        }

        public byte ColdWhite
        {
            get; set;
        }

        public WhiteColor(byte warm_white)
        {
            m_warmWhite = warm_white;
            ColdWhite = 0;
        }

        public WhiteColor(byte warm_white, byte cold_white)
        {
            m_warmWhite = warm_white;
            ColdWhite = cold_white;
        }
    }
}
