namespace IotHubSync.AzureFunction
{
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.EventGrid.Models;
    using Microsoft.Azure.WebJobs.Extensions.EventGrid;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;
    using System.Globalization;
    using IotHubSync.Logic;
    using Microsoft.Extensions.Configuration;

    public static class IotHubSyncFunction
    {
        private static readonly string IotHubConnectionStringMasterKey = "IotHubConnectionStringMaster";
        private static readonly string IotHubConnectionStringSlaveKey = "IotHubConnectionStringSlave";

        [FunctionName("DeviceCreated")]
        public static async Task DeviceCreated([EventGridTrigger]EventGridEvent eventGridEvent, ILogger logger, ExecutionContext context)
        {
            logger.LogInformation($"DeviceCreated function processed a request at {System.DateTime.UtcNow.ToString(new CultureInfo("en-US"))}.");

            var deviceSynchronizer = new DeviceSynchronizer(GetConnectionStrings(context), logger);

            var isSuccess = await deviceSynchronizer.CreateDevice(eventGridEvent.Data.ToString());

            if (isSuccess)
            {
                logger.LogInformation($"DeviceCreated function completed successfully.");
            }
            else
            {
                logger.LogError($"DeviceCreated function completed with errors.");
            }
        }

        [FunctionName("DeviceDeleted")]
        public static async Task DeviceDeleted([EventGridTrigger]EventGridEvent eventGridEvent, ILogger logger, ExecutionContext context)
        {
            logger.LogInformation($"DeviceDeleted function processed a request at {System.DateTime.UtcNow.ToString(new CultureInfo("en-US"))}.");

            var deviceSynchronizer = new DeviceSynchronizer(GetConnectionStrings(context), logger);

            var isSuccess = await deviceSynchronizer.DeleteDevice(eventGridEvent.Data.ToString());

            if (isSuccess)
            {
                logger.LogInformation($"DeviceDeleted function completed successfully.");
            }
            else
            {
                logger.LogError($"DeviceDeleted function completed with errors.");
            }
        }

        [FunctionName("SyncIotHubs")]
        public static async Task SyncIotHubs([TimerTrigger("0 0 * * * *")]TimerInfo myTimer, ILogger logger, ExecutionContext context)
        {
            logger.LogInformation($"SyncIotHubs function processed a request {System.DateTime.UtcNow.ToString(new CultureInfo("en-US"))}.");

            var deviceSynchronizer = new DeviceSynchronizer(GetConnectionStrings(context), logger);

            var isSuccess = await deviceSynchronizer.SyncIotHubsAsync();

            if (isSuccess)
            {
                logger.LogInformation($"SyncIotHubs function completed successfully.");
            }
            else
            {
                logger.LogError($"SyncIotHubs function completed with errors.");
            }
        }

        private static ConnectionStrings GetConnectionStrings(ExecutionContext context)
        {
            var config = new ConfigurationBuilder()
               .SetBasePath(context.FunctionAppDirectory)
               .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
               .AddEnvironmentVariables()
               .Build();

            var connectionStrings = new ConnectionStrings(
                config.GetValue<string>(IotHubConnectionStringMasterKey),
                config.GetValue<string>(IotHubConnectionStringSlaveKey));
            return connectionStrings;
        }
    }
}