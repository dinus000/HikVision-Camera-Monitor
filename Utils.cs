using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MotionMonitor
{
    public class Utils
    {
        public static long IpAddressToLong(string addr)
        {
            return (long)(uint)IPAddress.NetworkToHostOrder((int)IPAddress.Parse(addr).Address);
        }

        public static string LongToIpAddress(long address)
        {
            // return IPAddress.Parse(address.ToString()).ToString();
            return new IPAddress((uint)IPAddress.HostToNetworkOrder((int) address)).ToString();
        }

        public static long GetTimeStampMs()
        {
            return (long)((double)Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency * 1000.0);
        }
    }
}
