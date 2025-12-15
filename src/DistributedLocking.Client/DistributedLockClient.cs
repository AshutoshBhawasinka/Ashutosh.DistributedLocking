using System;
using System.Net.Http;
using System.Text;
using DistributedLocking.Client.Models;
using Newtonsoft.Json;

namespace DistributedLocking.Client
{
    internal sealed class DistributedLockClient : IDistributedLock
    {
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
        }

        public DistributedLockClient(string serviceBaseUrl, HttpClient httpClient)
        {
            if (string.IsNullOrWhiteSpace(serviceBaseUrl))
            {
                throw new ArgumentNullException(nameof(serviceBaseUrl));
            }

            _serviceBaseUrl = serviceBaseUrl.TrimEnd('/');
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
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
                        return new LockHandle(_serviceBaseUrl, resourceName, lockResponse.LockToken, _httpClient);
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public bool IsLocked(string resourceName)
        {
            if (string.IsNullOrWhiteSpace(resourceName))
            {
                throw new ArgumentNullException(nameof(resourceName));
            }

            try
            {
                var response = _httpClient.GetAsync($"{_serviceBaseUrl}/api/lock/status/{Uri.EscapeDataString(resourceName)}").GetAwaiter().GetResult();

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    var statusResponse = JsonConvert.DeserializeObject<LockStatusResponse>(responseBody);

                    return statusResponse != null && statusResponse.IsLocked;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
