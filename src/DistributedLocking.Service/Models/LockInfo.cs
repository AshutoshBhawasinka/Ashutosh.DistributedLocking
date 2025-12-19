namespace Ashutosh.DistributedLocking.Service.Models
{
    public class LockInfo
    {
        public string ResourceName { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string LockToken { get; set; } = string.Empty;
        public DateTime AcquiredAt { get; set; }
        public DateTime LastHeartbeat { get; set; }
    }
}
