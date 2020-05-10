using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MotionMonitor
{
    internal class HttpService: IDisposable
    {
        const string Protocol = "http";
        const string UrlSuffix = "ISAPI/Event/notification/alertStream";
        private CameraConfig _config = null;
        private long _id = 0;
        private Stream _httpStream;
        private StreamReader _reader;
        private HttpClient _httpClient;
        private object _lock = new object();

        internal HttpService(long id, CameraConfig config)
        {
            _config = config;
            _id = id;
        }

        ~HttpService()
        {
            Dispose();
        }

        internal async Task<bool> Start()
        {
            string fullAddress = $"{Protocol}://{_config.IpAddress}/{UrlSuffix}";

            try
            {
                _httpClient = new HttpClient();
                if (!string.IsNullOrWhiteSpace(_config.UserName) && !string.IsNullOrWhiteSpace(_config.Password))
                {
                    var byteArray = Encoding.ASCII.GetBytes($"{_config.UserName}:{_config.Password}");
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                }
                
                _httpStream = await _httpClient.GetStreamAsync(fullAddress);
                if (_httpStream != null)
                {
                    _reader = new StreamReader(_httpStream, System.Text.Encoding.ASCII, false);
                    return _reader != null;
                }

                return false;
            }
            catch (Exception e)
            {
                Logger.Error($"[HttpService:Init] failed to initialize http client for camera with IP address {_config.IpAddress} error: {e.Message}");
                return false;
            }
        }

        internal async Task<string> GetNextLineOfData()
        {
            if (!_reader.EndOfStream)
            {
                string line =  await _reader.ReadLineAsync();
                return line.Trim(new char[] { '\r', 'n', ' ' });
            }

            return null;
        }

        internal void Stop()
        {
            _httpClient.CancelPendingRequests();
            _reader.Close();
            _httpStream.Close();
        }

        public void Dispose()
        {
            _reader?.Dispose();
            _httpStream?.Dispose();
            _httpClient?.Dispose();
        }
    }
}
