using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using Ashutosh.Common.Logger;
using Newtonsoft.Json;

namespace DistributedLocking.Client
{
    public sealed class LockHandle : IDisposable
    {
        private static readonly Logger Logger = new Logger(typeof(LockHandle));
        private readonly string _serviceBaseUrl;
        private readonly string _resourceName;
        private readonly string _lockToken;
        private readonly Timer _heartbeatTimer;
        private readonly HttpClient _httpClient;
        private readonly object _disposeLock = new object();
        private bool _disposed;

        internal LockHandle(string serviceBaseUrl, string resourceName, string lockToken, HttpClient httpClient)
        {
            _serviceBaseUrl = serviceBaseUrl.TrimEnd('/');
            _resourceName = resourceName;
            _lockToken = lockToken;
            _httpClient = httpClient;
            _disposed = false;

            _heartbeatTimer = new Timer(SendHeartbeat, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            Logger.Log($"LockHandle created for resource '{resourceName}' with token '{lockToken}'");
        }

        public string ResourceName
        {
            get { return _resourceName; }
        }

        public string LockToken
        {
            get { return _lockToken; }
        }

        private void SendHeartbeat(object state)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                var request = new
                {
                    ResourceName = _resourceName,
                    LockToken = _lockToken
                };

                string json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = _httpClient.PostAsync($"{_serviceBaseUrl}/api/lock/heartbeat", content).GetAwaiter().GetResult();
                
                if (response.IsSuccessStatusCode)
                {
                    Logger.LogVerbose($"Heartbeat sent successfully for resource '{_resourceName}'");
                }
                else
                {
                    Logger.LogWarning($"Heartbeat failed for resource '{_resourceName}'. Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Exception while sending heartbeat for resource '{_resourceName}': {ex.Message}");
            }
        }

        private void ReleaseLock()
        {
            Logger.LogVerbose($"Releasing lock for resource '{_resourceName}'");
            
            try
            {
                var request = new
                {
                    ResourceName = _resourceName,
                    LockToken = _lockToken
                };

                string json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = _httpClient.PostAsync($"{_serviceBaseUrl}/api/lock/release", content).GetAwaiter().GetResult();
                
                if (response.IsSuccessStatusCode)
                {
                    Logger.Log($"Lock released successfully for resource '{_resourceName}'");
                }
                else
                {
                    Logger.LogWarning($"Failed to release lock for resource '{_resourceName}'. Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Exception while releasing lock for resource '{_resourceName}': {ex.Message}");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            lock (_disposeLock)
            {
                if (_disposed)
                {
                    return;
                }

                Logger.LogVerbose($"Disposing LockHandle for resource '{_resourceName}'");

                if (disposing)
                {
                    _heartbeatTimer.Dispose();
                    ReleaseLock();
                }

                _disposed = true;
            }
        }

        ~LockHandle()
        {
            Dispose(false);
        }
    }
}
