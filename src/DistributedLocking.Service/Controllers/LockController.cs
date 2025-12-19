using Ashutosh.Common.Logger;
using DistributedLocking.Service.Models;
using DistributedLocking.Service.Services;
using Microsoft.AspNetCore.Mvc;

namespace DistributedLocking.Service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LockController : ControllerBase
    {
        private static readonly Logger Logger = new Logger(typeof(LockController));
        private readonly LockManager _lockManager;

        public LockController(LockManager lockManager)
        {
            _lockManager = lockManager;
            Logger.LogVerbose("LockController instantiated");
        }

        [HttpPost("acquire")]
        public IActionResult AcquireLock([FromBody] LockRequest request)
        {
            Logger.LogVerbose($"AcquireLock request received");

            if (request == null)
            {
                Logger.LogWarning("AcquireLock called with null request body");
                return BadRequest(new LockResponse
                {
                    Success = false,
                    Status = "BadRequest",
                    Message = "Request body is required"
                });
            }

            Logger.Log($"AcquireLock request for resource '{request.ResourceName}' from client '{request.ClientId}'");
            var response = _lockManager.TryAcquireLock(request.ResourceName, request.ClientId);

            if (response.Success)
            {
                Logger.Log($"Lock acquired for resource '{request.ResourceName}'");
                return Ok(response);
            }

            if (response.Status == "BadRequest")
            {
                Logger.LogWarning($"AcquireLock bad request for resource '{request.ResourceName}' from client '{request.ClientId}': {response.Message}");
                return BadRequest(response);
            }

            Logger.LogWarning($"Lock acquisition conflict for resource '{request.ResourceName}' from client '{request.ClientId}': {response.Message}");
            return Conflict(response);
        }

        [HttpPost("heartbeat")]
        public IActionResult Heartbeat([FromBody] HeartbeatRequest request)
        {
            Logger.LogVerbose($"Heartbeat request received");

            if (request == null)
            {
                Logger.LogWarning("Heartbeat called with null request body");
                return BadRequest(new LockResponse
                {
                    Success = false,
                    Status = "BadRequest",
                    Message = "Request body is required"
                });
            }

            Logger.LogVerbose($"Heartbeat for resource '{request.ResourceName}'");
            var response = _lockManager.SendHeartbeat(request.ResourceName, request.LockToken);

            if (response.Success)
            {
                Logger.LogVerbose($"Heartbeat acknowledged for resource '{request.ResourceName}'");
                return Ok(response);
            }

            if (response.Status == "BadRequest")
            {
                Logger.LogWarning($"Heartbeat bad request for resource '{request.ResourceName}': {response.Message}");
                return BadRequest(response);
            }

            if (response.Status == "NotFound")
            {
                Logger.LogWarning($"Heartbeat not found for resource '{request.ResourceName}'");
                return NotFound(response);
            }

            Logger.LogWarning($"Heartbeat conflict for resource '{request.ResourceName}': {response.Message}");
            return Conflict(response);
        }

        [HttpPost("release")]
        public IActionResult ReleaseLock([FromBody] UnlockRequest request)
        {
            Logger.LogVerbose($"ReleaseLock request received");

            if (request == null)
            {
                Logger.LogWarning("ReleaseLock called with null request body");
                return BadRequest(new LockResponse
                {
                    Success = false,
                    Status = "BadRequest",
                    Message = "Request body is required"
                });
            }

            Logger.Log($"ReleaseLock request for resource '{request.ResourceName}'");
            var response = _lockManager.ReleaseLock(request.ResourceName, request.LockToken);

            if (response.Success)
            {
                Logger.Log($"Lock released for resource '{request.ResourceName}'");
                return Ok(response);
            }

            if (response.Status == "BadRequest")
            {
                Logger.LogWarning($"ReleaseLock bad request for resource '{request.ResourceName}': {response.Message}");
                return BadRequest(response);
            }

            if (response.Status == "NotFound")
            {
                Logger.LogWarning($"Lock not found for resource '{request.ResourceName}'");
                return NotFound(response);
            }

            Logger.LogWarning($"ReleaseLock conflict for resource '{request.ResourceName}': {response.Message}");
            return Conflict(response);
        }

        [HttpGet("status/{resourceName}")]
        public IActionResult GetLockStatus(string resourceName)
        {
            Logger.LogVerbose($"GetLockStatus request for resource '{resourceName}'");
            var lockInfo = _lockManager.GetLockInfo(resourceName);

            if (lockInfo == null)
            {
                Logger.LogVerbose($"Resource '{resourceName}' is not locked");
                return Ok(new { IsLocked = false, ResourceName = resourceName });
            }

            Logger.LogVerbose($"Resource '{resourceName}' is locked by client '{lockInfo.ClientId}'");
            return Ok(new
            {
                IsLocked = true,
                ResourceName = lockInfo.ResourceName,
                ClientId = lockInfo.ClientId,
                AcquiredAt = lockInfo.AcquiredAt,
                LastHeartbeat = lockInfo.LastHeartbeat
            });
        }
    }
}
