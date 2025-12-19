namespace Ashutosh.DistributedLocking.Service.Models
{
    public class HeartbeatRequest
    {
        public string ResourceName { get; set; } = string.Empty;
        public string LockToken { get; set; } = string.Empty;
    }
}
