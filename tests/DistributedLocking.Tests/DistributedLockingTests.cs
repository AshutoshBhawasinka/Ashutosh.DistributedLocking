using Ashutosh.DistributedLocking.Client;
using Ashutosh.DistributedLocking.Service.Models;
using Ashutosh.DistributedLocking.Service.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ashutosh.DistributedLocking.Tests
{
    [TestClass]
    public class DistributedLockingTests
    {
        private static WebApplicationFactory<Program> _factory = null!;
        private static string _baseUrl = null!;
        private HttpClient _httpClient = null!;
        private IDistributedLock _lockClient = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _factory = new WebApplicationFactory<Program>();
            _baseUrl = _factory.Server.BaseAddress.ToString().TrimEnd('/');
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _factory?.Dispose();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _httpClient = _factory.CreateClient();
            _lockClient = DistributedLockFactory.Create(_baseUrl, _httpClient);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _httpClient?.Dispose();
        }

        #region Lock Acquisition Tests

        [TestMethod]
        public void AcquireLock_WithValidResource_ShouldSucceed()
        {
            // Arrange
            var resourceName = "test-resource-" + Guid.NewGuid().ToString("N");

            // Act
            using var lockHandle = _lockClient.AcquireLock(resourceName, "client-1");

            // Assert
            Assert.IsNotNull(lockHandle);
        }

        [TestMethod]
        public void AcquireLock_WithEmptyResourceName_ShouldThrowException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => _lockClient.AcquireLock("", "client-1"));
        }

        #endregion

        #region Lock Release Tests

        [TestMethod]
        public void ReleaseLock_ViaDispose_ShouldReleaseLock()
        {
            // Arrange
            var resourceName = "test-resource-release-" + Guid.NewGuid().ToString("N");

            // Act - Acquire and release via dispose
            using (_lockClient.AcquireLock(resourceName, "client-1"))
            {
                Assert.IsTrue(_lockClient.IsLocked(resourceName));
            }

            // Assert - Lock should be released after dispose
            Assert.IsFalse(_lockClient.IsLocked(resourceName));
        }

        [TestMethod]
        public void ReleaseLock_AfterDispose_NewClientCanAcquire()
        {
            // Arrange
            var resourceName = "test-resource-reacquire-" + Guid.NewGuid().ToString("N");

            // Act - First client acquires and releases
            using (_lockClient.AcquireLock(resourceName, "client-1"))
            {
                // Lock held
            }

            // Second client should be able to acquire
            using var secondLock = _lockClient.AcquireLock(resourceName, "client-2");

            // Assert
            Assert.IsNotNull(secondLock);
            Assert.IsTrue(_lockClient.IsLocked(resourceName));
        }

        #endregion

        #region Lock Status Tests

        [TestMethod]
        public void IsLocked_WhenLocked_ShouldReturnTrue()
        {
            // Arrange
            var resourceName = "test-resource-status-" + Guid.NewGuid().ToString("N");

            // Act
            using var lockHandle = _lockClient.AcquireLock(resourceName, "client-1");

            // Assert
            Assert.IsTrue(_lockClient.IsLocked(resourceName));
        }

        [TestMethod]
        public void IsLocked_WhenNotLocked_ShouldReturnFalse()
        {
            // Arrange
            var resourceName = "unlocked-resource-" + Guid.NewGuid().ToString("N");

            // Act & Assert
            Assert.IsFalse(_lockClient.IsLocked(resourceName));
        }

        #endregion

        #region Concurrent Lock Contention Tests

        [TestMethod]
        public void AcquireLock_WhenAlreadyLocked_ShouldReturnNull()
        {
            // Arrange
            var resourceName = "contested-resource-" + Guid.NewGuid().ToString("N");

            // First client acquires lock
            using var firstLock = _lockClient.AcquireLock(resourceName, "client-1");
            Assert.IsNotNull(firstLock);

            // Act - Second client tries to acquire same lock
            var secondLock = _lockClient.AcquireLock(resourceName, "client-2");

            // Assert - Should return null (lock not acquired)
            Assert.IsNull(secondLock);
        }

        [TestMethod]
        public void AcquireLock_AfterRelease_ShouldSucceedForNewClient()
        {
            // Arrange
            var resourceName = "released-resource-" + Guid.NewGuid().ToString("N");

            // First client acquires and releases lock
            using (_lockClient.AcquireLock(resourceName, "client-1"))
            {
                // Lock held by client-1
            }

            // Act - Second client acquires the released lock
            using var secondLock = _lockClient.AcquireLock(resourceName, "client-2");

            // Assert
            Assert.IsNotNull(secondLock);
        }

        [TestMethod]
        public void AcquireLock_MultipleResources_ShouldAllSucceed()
        {
            // Arrange & Act
            var resource1 = "resource-1-" + Guid.NewGuid().ToString("N");
            var resource2 = "resource-2-" + Guid.NewGuid().ToString("N");
            var resource3 = "resource-3-" + Guid.NewGuid().ToString("N");

            using var lock1 = _lockClient.AcquireLock(resource1, "client-1");
            using var lock2 = _lockClient.AcquireLock(resource2, "client-1");
            using var lock3 = _lockClient.AcquireLock(resource3, "client-1");

            // Assert
            Assert.IsNotNull(lock1);
            Assert.IsNotNull(lock2);
            Assert.IsNotNull(lock3);
            Assert.IsTrue(_lockClient.IsLocked(resource1));
            Assert.IsTrue(_lockClient.IsLocked(resource2));
            Assert.IsTrue(_lockClient.IsLocked(resource3));
        }

        #endregion

        #region Lock Expiration Tests (Missed Heartbeat)

        [TestMethod]
        public void AcquireLock_AfterExpiration_ShouldSucceedForNewClient()
        {
            // This test requires direct access to LockManager to manipulate timeouts
            using var scope = _factory.Services.CreateScope();
            var lockManager = scope.ServiceProvider.GetRequiredService<LockManager>();

            // Arrange - Acquire lock directly through LockManager
            var resourceName = "expiring-resource-" + Guid.NewGuid().ToString("N");
            var firstLock = lockManager.TryAcquireLock(resourceName, "client-1");
            Assert.IsTrue(firstLock.Success);

            // Simulate expiration by manipulating the lock's LastHeartbeat
            var lockInfo = lockManager.GetLockInfo(resourceName);
            Assert.IsNotNull(lockInfo);

            // Use reflection to set LastHeartbeat to expired time (more than 45 seconds ago)
            var lastHeartbeatProperty = typeof(LockInfo).GetProperty("LastHeartbeat");
            Assert.IsNotNull(lastHeartbeatProperty);
            lastHeartbeatProperty.SetValue(lockInfo, DateTime.UtcNow.AddSeconds(-50));

            // Act - New client tries to acquire the expired lock via client library
            using var secondLock = _lockClient.AcquireLock(resourceName, "client-2");

            // Assert - Should succeed because the lock has expired
            Assert.IsNotNull(secondLock);
        }

        [TestMethod]
        public void Lock_WithActiveHandle_ShouldBlockOtherClients()
        {
            // Arrange
            var resourceName = "active-lock-" + Guid.NewGuid().ToString("N");

            // Act - Acquire lock and hold it
            using var lockHandle = _lockClient.AcquireLock(resourceName, "client-1");
            Assert.IsNotNull(lockHandle);

            // Assert - Second client should be blocked
            var secondLock = _lockClient.AcquireLock(resourceName, "client-2");
            Assert.IsNull(secondLock);

            // Verify lock is still active
            Assert.IsTrue(_lockClient.IsLocked(resourceName));
        }

        [TestMethod]
        public void Lock_ExpiredByMissedHeartbeat_BecomesAvailable()
        {
            // This test verifies that when a lock expires due to missed heartbeats,
            // another client can acquire it

            using var scope = _factory.Services.CreateScope();
            var lockManager = scope.ServiceProvider.GetRequiredService<LockManager>();

            // Arrange - Simulate a lock that has expired
            var resourceName = "missed-heartbeat-" + Guid.NewGuid().ToString("N");
            var initialLock = lockManager.TryAcquireLock(resourceName, "client-1");
            Assert.IsTrue(initialLock.Success);

            // Simulate missed heartbeat by setting LastHeartbeat to past
            var lockInfo = lockManager.GetLockInfo(resourceName);
            Assert.IsNotNull(lockInfo);

            var lastHeartbeatProperty = typeof(LockInfo).GetProperty("LastHeartbeat");
            Assert.IsNotNull(lastHeartbeatProperty);
            lastHeartbeatProperty.SetValue(lockInfo, DateTime.UtcNow.AddSeconds(-50));

            // Act - Different client acquires the expired lock
            using var newLock = _lockClient.AcquireLock(resourceName, "client-2");

            // Assert - New client should successfully acquire the lock
            Assert.IsNotNull(newLock);
            Assert.IsTrue(_lockClient.IsLocked(resourceName));
        }

        #endregion

        #region Heartbeat Renewal Tests

        [TestMethod]
        public void LockHandle_KeepsLockAlive_WhileHeld()
        {
            // Arrange
            var resourceName = "heartbeat-test-" + Guid.NewGuid().ToString("N");

            // Act - Acquire lock
            using var lockHandle = _lockClient.AcquireLock(resourceName, "client-1");
            Assert.IsNotNull(lockHandle);

            // Verify lock is held
            Assert.IsTrue(_lockClient.IsLocked(resourceName));

            // Small delay (heartbeat is every 30s, so this just verifies lock is still active)
            Thread.Sleep(100);

            // Assert - Lock should still be active
            Assert.IsTrue(_lockClient.IsLocked(resourceName));

            // Other clients should still be blocked
            var secondLock = _lockClient.AcquireLock(resourceName, "client-2");
            Assert.IsNull(secondLock);
        }

        #endregion
    }
}
