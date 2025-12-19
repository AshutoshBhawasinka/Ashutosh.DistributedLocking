using System;
using System.Net.Http;
using System.Text;
using Ashutosh.Common.Logger;
using Ashutosh.DistributedLocking.Client.Models;
using Newtonsoft.Json;

namespace Ashutosh.DistributedLocking.Client
{
    internal sealed class DistributedLockClient : IDistributedLock
    {
        private static readonly Logger Logger = new Logger(typeof(DistributedLockClient));
        private readonly string _serviceBaseUrl;
        private readonly HttpClient _httpClient;

        public DistributedLockClient(string serviceBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(serviceBaseUrl))
            {
                throw new ArgumentNullException(nameof(serviceBaseUrl));
            }

            _serviceBaseUrl = serviceBaseUrl.TrimEnd('/');
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            Logger.Log($"DistributedLockClient initialized with service URL: {_serviceBaseUrl}");
        }

        public DistributedLockClient(string serviceBaseUrl, HttpClient httpClient)
        {
            if (string.IsNullOrWhiteSpace(serviceBaseUrl))
            {
                throw new ArgumentNullException(nameof(serviceBaseUrl));
            }

            _serviceBaseUrl = serviceBaseUrl.TrimEnd('/');
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            Logger.Log($"DistributedLockClient initialized with service URL: {_serviceBaseUrl} and custom HttpClient");
        }

        public IDisposable AcquireLock(string resourceName)
        {
            return AcquireLock(resourceName, Guid.NewGuid().ToString("N"));
        }

        public IDisposable AcquireLock(string resourceName, string clientId)
        {
            if (string.IsNullOrWhiteSpace(resourceName))
            {
                throw new ArgumentNullException(nameof(resourceName));
            }

            if (string.IsNullOrWhiteSpace(clientId))
            {
                clientId = Guid.NewGuid().ToString("N");
            }

            Logger.LogVerbose($"Attempting to acquire lock for resource '{resourceName}' with clientId '{clientId}'");

            try
            {
                var request = new
                {
                    ResourceName = resourceName,
                    ClientId = clientId
                };

                string json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = _httpClient.PostAsync($"{_serviceBaseUrl}/api/lock/acquire", content).GetAwaiter().GetResult();

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    var lockResponse = JsonConvert.DeserializeObject<LockResponse>(responseBody);

                    if (lockResponse != null && lockResponse.Success && !string.IsNullOrEmpty(lockResponse.LockToken))
                    {
                        Logger.Log($"Lock acquired successfully for resource '{resourceName}' for client '{clientId}' with token '{lockResponse.LockToken}'");
                        return new LockHandle(_serviceBaseUrl, resourceName, lockResponse.LockToken, _httpClient);
                    }
                }

                Logger.LogWarning($"Failed to acquire lock for resource '{resourceName}' for client '{clientId}'. Status: {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Exception while acquiring lock for resource '{resourceName}' for client '{clientId}'");
                return null;
            }
        }

        public bool IsLocked(string resourceName)
        {
            if (string.IsNullOrWhiteSpace(resourceName))
            {
                throw new ArgumentNullException(nameof(resourceName));
            }

            Logger.LogVerbose($"Checking lock status for resource '{resourceName}'");

            try
            {
                var response = _httpClient.GetAsync($"{_serviceBaseUrl}/api/lock/status/{Uri.EscapeDataString(resourceName)}").GetAwaiter().GetResult();

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    var statusResponse = JsonConvert.DeserializeObject<LockStatusResponse>(responseBody);

                    bool isLocked = statusResponse != null && statusResponse.IsLocked;
                    Logger.LogVerbose($"Resource '{resourceName}' lock status: {(isLocked ? "locked" : "unlocked")}");
                    return isLocked;
                }

                Logger.LogWarning($"Failed to get lock status for resource '{resourceName}'. Status: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Exception while checking lock status for resource '{resourceName}'");
                return false;
            }
        }
    }
}
