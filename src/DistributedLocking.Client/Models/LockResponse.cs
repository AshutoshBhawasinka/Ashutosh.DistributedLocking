namespace DistributedLocking.Client.Models
{
    internal class LockResponse
    {
        public bool Success { get; set; }
        public string Status { get; set; }
        public string LockToken { get; set; }
        public string Message { get; set; }
    }
}
