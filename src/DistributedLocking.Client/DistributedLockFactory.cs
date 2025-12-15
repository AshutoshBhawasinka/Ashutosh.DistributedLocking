using System;
using System.Net.Http;

namespace DistributedLocking.Client
{
    public static class DistributedLockFactory
    {
        public static IDistributedLock Create(string serviceBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(serviceBaseUrl))
            {
                throw new ArgumentNullException(nameof(serviceBaseUrl));
            }

            return new DistributedLockClient(serviceBaseUrl);
        }

        public static IDistributedLock Create(string serviceBaseUrl, HttpClient httpClient)
        {
            if (string.IsNullOrWhiteSpace(serviceBaseUrl))
            {
                throw new ArgumentNullException(nameof(serviceBaseUrl));
            }

            if (httpClient == null)
            {
                throw new ArgumentNullException(nameof(httpClient));
            }

            return new DistributedLockClient(serviceBaseUrl, httpClient);
        }
    }
}
