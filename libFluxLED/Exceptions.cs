using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FluxLED
{
    public class UnknownProtocolException : Exception
    {
        public UnknownProtocolException() : base()
        {

        }

        public UnknownProtocolException(string message) : base(message)
        {

        }
    }

    public class UnknownModeException : Exception
    {
        public UnknownModeException() : base()
        {

        }
    }
}
