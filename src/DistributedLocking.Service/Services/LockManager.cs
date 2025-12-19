using Ashutosh.Common.Logger;
using System.Collections.Concurrent;
using Ashutosh.DistributedLocking.Service.Models;

namespace Ashutosh.DistributedLocking.Service.Services
{
    public class LockManager : IDisposable
    {
        private static readonly Logger Logger = new Logger(typeof(LockManager));
        private readonly ConcurrentDictionary<string, LockInfo> _locks = new ConcurrentDictionary<string, LockInfo>();
        private readonly object _syncLock = new object();
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(45);
        private bool _disposed;

        public LockManager()
        {
            _cleanupTimer = new Timer(CleanupExpiredLocks, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
            Logger.Log("LockManager initialized with cleanup interval of 10 seconds and lock timeout of 45 seconds");
        }

        public LockResponse TryAcquireLock(string resourceName, string clientId)
        {
            if (string.IsNullOrWhiteSpace(resourceName))
            {
                Logger.LogWarning("TryAcquireLock called with empty resource name");
                return new LockResponse
                {
                    Success = false,
                    Status = "BadRequest",
                    Message = "Resource name is required"
                };
            }

            Logger.LogVerbose($"TryAcquireLock called for resource '{resourceName}' by client '{clientId}'");

            lock (_syncLock)
            {
                if (_locks.TryGetValue(resourceName, out LockInfo? existingLock))
                {
                    if (DateTime.UtcNow - existingLock.LastHeartbeat > _lockTimeout)
                    {
                        Logger.Log($"Removing expired lock for resource '{resourceName}' (held by client '{existingLock.ClientId}')");
                        _locks.TryRemove(resourceName, out _);
                    }
                    else
                    {
                        Logger.LogVerbose($"Resource '{resourceName}' is already locked by client '{existingLock.ClientId}'");
                        return new LockResponse
                        {
                            Success = false,
                            Status = "Busy",
                            Message = $"Resource '{resourceName}' is currently locked by another client"
                        };
                    }
                }

                string lockToken = Guid.NewGuid().ToString("N");
                var lockInfo = new LockInfo
                {
                    ResourceName = resourceName,
                    ClientId = clientId,
                    LockToken = lockToken,
                    AcquiredAt = DateTime.UtcNow,
                    LastHeartbeat = DateTime.UtcNow
                };

                if (_locks.TryAdd(resourceName, lockInfo))
                {
                    Logger.Log($"Lock acquired for resource '{resourceName}' by client '{clientId}' with token '{lockToken}'");
                    return new LockResponse
                    {
                        Success = true,
                        Status = "Acquired",
                        LockToken = lockToken,
                        Message = "Lock acquired successfully"
                    };
                }

                Logger.LogWarning($"Failed to acquire lock for resource '{resourceName}' due to concurrent access");
                return new LockResponse
                {
                    Success = false,
                    Status = "Busy",
                    Message = "Failed to acquire lock due to concurrent access"
                };
            }
        }

        public LockResponse SendHeartbeat(string resourceName, string lockToken)
        {
            if (string.IsNullOrWhiteSpace(resourceName) || string.IsNullOrWhiteSpace(lockToken))
            {
                Logger.LogWarning("SendHeartbeat called with empty resource name or lock token");
                return new LockResponse
                {
                    Success = false,
                    Status = "BadRequest",
                    Message = "Resource name and lock token are required"
                };
            }

            lock (_syncLock)
            {
                if (_locks.TryGetValue(resourceName, out LockInfo? lockInfo))
                {
                    if (lockInfo.LockToken == lockToken)
                    {
                        lockInfo.LastHeartbeat = DateTime.UtcNow;
                        Logger.LogVerbose($"Heartbeat received for resource '{resourceName}'");
                        return new LockResponse
                        {
                            Success = true,
                            Status = "HeartbeatReceived",
                            LockToken = lockToken,
                            Message = "Heartbeat received successfully"
                        };
                    }

                    Logger.LogWarning($"Heartbeat rejected for resource '{resourceName}': invalid token");
                    return new LockResponse
                    {
                        Success = false,
                        Status = "InvalidToken",
                        Message = "Lock token does not match"
                    };
                }

                Logger.LogWarning($"Heartbeat rejected for resource '{resourceName}': lock not found");
                return new LockResponse
                {
                    Success = false,
                    Status = "NotFound",
                    Message = "Lock not found for the specified resource"
                };
            }
        }

        public LockResponse ReleaseLock(string resourceName, string lockToken)
        {
            if (string.IsNullOrWhiteSpace(resourceName) || string.IsNullOrWhiteSpace(lockToken))
            {
                Logger.LogWarning("ReleaseLock called with empty resource name or lock token");
                return new LockResponse
                {
                    Success = false,
                    Status = "BadRequest",
                    Message = "Resource name and lock token are required"
                };
            }

            Logger.LogVerbose($"ReleaseLock called for resource '{resourceName}'");

            lock (_syncLock)
            {
                if (_locks.TryGetValue(resourceName, out LockInfo? lockInfo))
                {
                    if (lockInfo.LockToken == lockToken)
                    {
                        if (_locks.TryRemove(resourceName, out _))
                        {
                            Logger.Log($"Lock released for resource '{resourceName}'");
                            return new LockResponse
                            {
                                Success = true,
                                Status = "Released",
                                Message = "Lock released successfully"
                            };
                        }
                    }

                    Logger.LogWarning($"ReleaseLock rejected for resource '{resourceName}': invalid token");
                    return new LockResponse
                    {
                        Success = false,
                        Status = "InvalidToken",
                        Message = "Lock token does not match"
                    };
                }

                Logger.LogWarning($"ReleaseLock rejected for resource '{resourceName}': lock not found");
                return new LockResponse
                {
                    Success = false,
                    Status = "NotFound",
                    Message = "Lock not found for the specified resource"
                };
            }
        }

        public LockInfo? GetLockInfo(string resourceName)
        {
            Logger.LogVerbose($"GetLockInfo called for resource '{resourceName}'");
            if (_locks.TryGetValue(resourceName, out LockInfo? lockInfo))
            {
                return lockInfo;
            }
            return null;
        }

        private void CleanupExpiredLocks(object? state)
        {
            lock (_syncLock)
            {
                var expiredLocks = _locks
                    .Where(kvp => DateTime.UtcNow - kvp.Value.LastHeartbeat > _lockTimeout)
                    .Select(kvp => kvp.Key)
                    .ToList();

                if (expiredLocks.Count > 0)
                {
                    Logger.Log($"Cleaning up {expiredLocks.Count} expired lock(s)");
                }

                foreach (var resourceName in expiredLocks)
                {
                    if (_locks.TryRemove(resourceName, out var removedLock))
                    {
                        Logger.LogVerbose($"Removed expired lock for resource '{resourceName}' (was held by client '{removedLock.ClientId}')");
                    }
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Logger.Log("LockManager disposing");
                _cleanupTimer.Dispose();
                _disposed = true;
            }
        }
    }
}
