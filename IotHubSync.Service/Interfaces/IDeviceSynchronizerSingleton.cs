using IotHubSync.Logic;

namespace IotHubSync.Service.Interfaces
{
    public interface IDeviceSynchronizerSingleton
    {
        DeviceSynchronizer DeviceSynchronizer { get; set; }
    }
}