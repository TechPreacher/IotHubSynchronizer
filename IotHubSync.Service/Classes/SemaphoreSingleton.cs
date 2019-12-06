namespace IotHubSync.Service.Classes
{
    using IotHubSync.Service.Interfaces;
    using System.Threading;
    public class SemaphoreSingleton : ISemaphoreSingleton
    {
        public SemaphoreSlim Semaphore { get; set; }

        public SemaphoreSingleton()
        {
            Semaphore = new SemaphoreSlim(1, 1);
        }
    }
}
