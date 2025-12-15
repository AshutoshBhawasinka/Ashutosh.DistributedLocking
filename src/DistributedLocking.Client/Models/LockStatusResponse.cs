namespace DistributedLocking.Client.Models
{
    internal class LockStatusResponse
    {
        public bool IsLocked { get; set; }
        public string ResourceName { get; set; }
    }
}
