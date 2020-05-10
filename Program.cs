using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MotionMonitor
{
    class Program
    {
        static void Main(string[] args)
        {
            PollingService service = null;
            try
            {
                service = new PollingService();
                if (!service.Start())
                {
                    Logger.Error("[Main] Listening service failed to start.");
                    return;
                }

                // The following command blocks the execution.
                service.Run();
            }
            catch (Exception ex)
            {
                Logger.Error($"[Main] General error: {ex.Message}");
            }
            finally
            {
                service?.Stop();
            }
        }
    }
}
