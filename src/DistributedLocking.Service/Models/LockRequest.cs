namespace Ashutosh.DistributedLocking.Service.Models
{
    public class LockRequest
    {
        public string ResourceName { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
    }
}
