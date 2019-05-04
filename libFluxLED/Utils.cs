using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FluxLED
{
    class Utils
    {
        private const byte MAX_DELAY = 0x1f;

        public static int ConvertByteToPercent(byte v)
        {
            return v * 100 / 255;
        }

        public static byte ConvertPercentToByte(int percent)
        {
            if (percent > 100 || percent < 0)
                throw new ArgumentOutOfRangeException("percent", string.Format("Percent must be between 0 and 100. Value passed: {0}", percent));

            return (byte)(percent * 255 / 100);
        }

        public static int ConvertDelayToSpeed(byte delay)
        {
            //speed is 0-100, delay is 1-31
            if (delay < 1 || delay > 31)
                throw new ArgumentOutOfRangeException("delay", string.Format("Delay must be between 1 and 31. Value passed: {0}", delay));

            //1st translate delay to 0-30
            delay -= 1;
            int inv_speed = (((int)delay * 100) / (MAX_DELAY - 1));
            int speed = 100 - inv_speed;
            return speed;
        }

        public static byte ConvertSpeedToDelay(int speed)
        {
            //speed is 0-100, delay is 1-31
            if (speed < 0 || speed > 100)
                throw new ArgumentOutOfRangeException("speed", string.Format("Speed must be between 0 and 100. Value passed: {0}", speed));

            int inv_speed = 100 - speed;
            byte delay = (byte)((inv_speed * (MAX_DELAY - 1)) / 100);
            //translate from 0-30 to 1-31
            delay += 1;
            return delay;
        }
    }
}
