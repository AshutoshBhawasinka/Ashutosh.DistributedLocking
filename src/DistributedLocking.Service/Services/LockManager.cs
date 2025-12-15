using DistributedLocking.Service.Models;
using System.Collections.Concurrent;

namespace DistributedLocking.Service.Services
{
    public class LockManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, LockInfo> _locks = new ConcurrentDictionary<string, LockInfo>();
        private readonly object _syncLock = new object();
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(45);
        private bool _disposed;

        public LockManager()
        {
            _cleanupTimer = new Timer(CleanupExpiredLocks, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        public LockResponse TryAcquireLock(string resourceName, string clientId)
        {
            if (string.IsNullOrWhiteSpace(resourceName))
            {
                return new LockResponse
                {
                    Success = false,
                    Status = "BadRequest",
                    Message = "Resource name is required"
                };
            }

            lock (_syncLock)
            {
                if (_locks.TryGetValue(resourceName, out LockInfo? existingLock))
                {
                    if (DateTime.UtcNow - existingLock.LastHeartbeat > _lockTimeout)
                    {
                        _locks.TryRemove(resourceName, out _);
                    }
                    else
                    {
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
                    return new LockResponse
                    {
                        Success = true,
                        Status = "Acquired",
                        LockToken = lockToken,
                        Message = "Lock acquired successfully"
                    };
                }

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
                        return new LockResponse
                        {
                            Success = true,
                            Status = "HeartbeatReceived",
                            LockToken = lockToken,
                            Message = "Heartbeat received successfully"
                        };
                    }

                    return new LockResponse
                    {
                        Success = false,
                        Status = "InvalidToken",
                        Message = "Lock token does not match"
                    };
                }

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
                        if (_locks.TryRemove(resourceName, out _))
                        {
                            return new LockResponse
                            {
                                Success = true,
                                Status = "Released",
                                Message = "Lock released successfully"
                            };
                        }
                    }

                    return new LockResponse
                    {
                        Success = false,
                        Status = "InvalidToken",
                        Message = "Lock token does not match"
                    };
                }

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

                foreach (var resourceName in expiredLocks)
                {
                    _locks.TryRemove(resourceName, out _);
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cleanupTimer.Dispose();
                _disposed = true;
            }
        }
    }
}
