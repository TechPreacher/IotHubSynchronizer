using System.Threading;

namespace IotHubSync.Service.Interfaces
{
    public interface ISemaphoreSingleton
    {
        SemaphoreSlim Semaphore { get; set; }
    }
}