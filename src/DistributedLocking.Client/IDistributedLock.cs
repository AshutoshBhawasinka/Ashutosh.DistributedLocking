using System;

namespace Ashutosh.DistributedLocking.Client
{
    public interface IDistributedLock
    {
        IDisposable AcquireLock(string resourceName);
        IDisposable AcquireLock(string resourceName, string clientId);
        bool IsLocked(string resourceName);
    }
}
