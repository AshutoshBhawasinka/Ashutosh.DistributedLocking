namespace Ashutosh.DistributedLocking.Service.Models
{
    public class LockResponse
    {
        public bool Success { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? LockToken { get; set; }
        public string? Message { get; set; }
    }
}
