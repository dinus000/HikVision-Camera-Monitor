using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MotionMonitor
{
    struct OperationResult
    {
        public bool Success { get; }
        public string ErrorMessage { get; }

        public OperationResult(bool success, string message)
        {
            Success = success;
            ErrorMessage = message;
        }
    }

    internal class PollingService
    {
        internal bool ServiceRunning { get; set; }
        const int ServiceInitTmieoutMs = 10000;
        IDictionary<long, HttpService> clientServices = new Dictionary<long, HttpService>();
        private IEnumerable<CameraConfig> _camConfig = AppConfig.GetCameraConfig();
        internal bool Start()
        {
            if (_camConfig == null)
            {
                Logger.Error("[PollingService:Init] Failed to get application gonfiguration.");
                return false;
            }
            else if (!_camConfig.Any())
            {
                Logger.Error("[PollingService:Init] Application configuratino does not contain camera settings.");
                return false;
            }

            try
            {
                foreach(var config in _camConfig)
                {
                    var result = StartClientService(config);
                    if (!result.Success)
                    {
                        continue;
                    }
                }
                if (!clientServices.Any())
                { 
                    Logger.Error("[PollingService:Start] Could not initialize any of the monitoring services.");
                    return false;
                }

                ServiceRunning = true;

                return true;
            }
            catch(Exception ex)
            {
                Logger.Error($"[PollingService:Start] message: {ex.Message}.");
                return false;
            }
        }

        internal void Run()
        {
            var eventsConfig = AppConfig.GetSubscriptinoEventsConfig();
            if (!eventsConfig.Any())
            {
                // AppConfig.GetSubscriptinoEventsConfig should take care of logging respective errors if needed.
                return;
            }
            var dispatcher = new Dispatcher(eventsConfig);

            while (ServiceRunning)
            {
                foreach (var service in clientServices)
                {
                    // var data = new StreamData();
                    var dataLine = string.Empty;
                    service.Value.GetNextLineOfData().ContinueWith(t => dataLine = t.Result).Wait();
                    if (dataLine == null)
                    {
                        string ipAddress = Utils.LongToIpAddress(service.Key);
                        Logger.Error($"[PollingService:Run] The stream for the caera with IP Address: {ipAddress} failed. Attempting to restart it.");
                    }
                    dispatcher.Process(dataLine, service.Key);
                }
            }
            dispatcher.Dispose();
            dispatcher = null;
        }

        internal void Stop()
        {
            ServiceRunning = false;
            foreach(var service in clientServices.Values)
            {
                service.Stop();
            }
        }

        private OperationResult StartClientService(CameraConfig config)
        {
            bool success = false;
            long id = Utils.IpAddressToLong(config.IpAddress);
            if (clientServices.ContainsKey(id))
            {
                return ErrorOperationResult($"[StartClientService:Init] More than one camera configuration with same IP address: {config.IpAddress}. Skiping the duplicate instance.");
            }

            var clientService = new HttpService(id, config); // HttpServiec constructor should throw in case of any issues.
            clientService.Start().ContinueWith(t => success = t.Result).Wait(ServiceInitTmieoutMs);  //Wait should re-throw any exceptions during initialization.
            if (success)
            {
                clientServices.Add(id, clientService);
            }
            else
            {
                return ErrorOperationResult($"[StartClientService:Init] Could not initialize monitoring service for camera with IP address {config.IpAddress}.");
            }

            return SuccessOperationResult();
        }

        private void StopClientService(HttpService service)
        {
            service.Stop();
        }

        private OperationResult SuccessOperationResult()
        {
            return new OperationResult(true, string.Empty);
        }

        private OperationResult ErrorOperationResult(string message)
        {
            return new OperationResult(false, message);
        }
    }
}
