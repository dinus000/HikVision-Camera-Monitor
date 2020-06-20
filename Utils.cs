using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MotionMonitor
{
    internal class Utils
    {
        internal static long IpAddressToLong(string addr)
        {
            return (long)(uint)IPAddress.NetworkToHostOrder((int)IPAddress.Parse(addr).Address);
        }

        internal static string LongToIpAddress(long address)
        {
            // return IPAddress.Parse(address.ToString()).ToString();
            return new IPAddress((uint)IPAddress.HostToNetworkOrder((int) address)).ToString();
        }

        internal static long GetTimeStampMs()
        {
            return (long)((double)Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency * 1000.0);
        }

        internal static string GenerateKey(CameraEvent cameraEvent)
        {
            return $"{cameraEvent.IpAddress}_{cameraEvent.EventType}";
        }

        internal static string GenerateKey(AutomationCommand command)
        {
            return  $"{command.Device}_{command.Event}";
        }

        internal static string GenerateKey(CameraEvent cameraEvent, long id)
        {
            return $"{id}_{cameraEvent.EventType}";
        }
    }
}
