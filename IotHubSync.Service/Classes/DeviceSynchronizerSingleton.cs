namespace IotHubSync.Service.Classes
{
    using IotHubSync.Logic;
    using IotHubSync.Service.Interfaces;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class DeviceSynchronizerSingleton : IDeviceSynchronizerSingleton
    {
        public DeviceSynchronizer DeviceSynchronizer { get; set; }

        public DeviceSynchronizerSingleton(IConfiguration config, ILogger<DeviceSynchronizerSingleton> logger)
        {
            var connectionStrings = new ConnectionStrings(
                config.GetValue<string>(Constants.IotHubConnectionStringMaster),
                config.GetValue<string>(Constants.IotHubConnectionStringSlave));

            DeviceSynchronizer = new DeviceSynchronizer(connectionStrings, logger);
        }
    }
}
