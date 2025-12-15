using DistributedLocking.Service.Models;
using DistributedLocking.Service.Services;
using Microsoft.AspNetCore.Mvc;

namespace DistributedLocking.Service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LockController : ControllerBase
    {
        private readonly LockManager _lockManager;

        public LockController(LockManager lockManager)
        {
            _lockManager = lockManager;
        }

        [HttpPost("acquire")]
        public IActionResult AcquireLock([FromBody] LockRequest request)
        {
            if (request == null)
            {
                return BadRequest(new LockResponse
                {
                    Success = false,
                    Status = "BadRequest",
                    Message = "Request body is required"
                });
            }

            var response = _lockManager.TryAcquireLock(request.ResourceName, request.ClientId);

            if (response.Success)
            {
                return Ok(response);
            }

            if (response.Status == "BadRequest")
            {
                return BadRequest(response);
            }

            return Conflict(response);
        }

        [HttpPost("heartbeat")]
        public IActionResult Heartbeat([FromBody] HeartbeatRequest request)
        {
            if (request == null)
            {
                return BadRequest(new LockResponse
                {
                    Success = false,
                    Status = "BadRequest",
                    Message = "Request body is required"
                });
            }

            var response = _lockManager.SendHeartbeat(request.ResourceName, request.LockToken);

            if (response.Success)
            {
                return Ok(response);
            }

            if (response.Status == "BadRequest")
            {
                return BadRequest(response);
            }

            if (response.Status == "NotFound")
            {
                return NotFound(response);
            }

            return Conflict(response);
        }

        [HttpPost("release")]
        public IActionResult ReleaseLock([FromBody] UnlockRequest request)
        {
            if (request == null)
            {
                return BadRequest(new LockResponse
                {
                    Success = false,
                    Status = "BadRequest",
                    Message = "Request body is required"
                });
            }

            var response = _lockManager.ReleaseLock(request.ResourceName, request.LockToken);

            if (response.Success)
            {
                return Ok(response);
            }

            if (response.Status == "BadRequest")
            {
                return BadRequest(response);
            }

            if (response.Status == "NotFound")
            {
                return NotFound(response);
            }

            return Conflict(response);
        }

        [HttpGet("status/{resourceName}")]
        public IActionResult GetLockStatus(string resourceName)
        {
            var lockInfo = _lockManager.GetLockInfo(resourceName);

            if (lockInfo == null)
            {
                return Ok(new { IsLocked = false, ResourceName = resourceName });
            }

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
