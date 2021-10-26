// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}

namespace IotHubSync.AzureFunction
{
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.EventGrid.Models;
    using Microsoft.Azure.WebJobs.Extensions.EventGrid;
    using Microsoft.Extensions.Logging;
    using IotHubSync.Logic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;

    public static class IotHubSyncFunction
    {
        private static readonly string IotHubConnectionStringMasterKey = "IotHubConnectionStringMaster";
        private static readonly string IotHubConnectionStringSlaveKey = "IotHubConnectionStringSlave";
        private static readonly string EventGridIotHubDeviceCreatedEvent = "Microsoft.Devices.DeviceCreated";
        private static readonly string EventGridIotHubDeviceDeletedEvent = "Microsoft.Devices.DeviceDeleted";

        [FunctionName("EventGridDeviceCreatedOrDeleted")]
        public static async Task Run([EventGridTrigger]EventGridEvent eventGridEvent, ILogger logger, ExecutionContext context)
        {
            bool isSuccess = true;

            if (eventGridEvent.EventType == EventGridIotHubDeviceCreatedEvent || eventGridEvent.EventType == EventGridIotHubDeviceDeletedEvent)
            {
                var deviceSynchronizer = new DeviceSynchronizer(GetConnectionStrings(context), logger);

                if (eventGridEvent.EventType == EventGridIotHubDeviceCreatedEvent)
                {
                    logger.LogInformation($"EventGridDeviceCreatedOrDeleted function received IotHubDeviceCreated event from EventGrid.");
                    isSuccess = await deviceSynchronizer.CreateDeviceFromEventGridMessage(eventGridEvent.Data.ToString());
                }

                else if (eventGridEvent.EventType == EventGridIotHubDeviceDeletedEvent)
                {
                    logger.LogInformation($"EventGridDeviceCreatedOrDeleted function received IotHubDeviceDeleted event from EventGrid.");
                    isSuccess = await deviceSynchronizer.CreateDeviceFromEventGridMessage(eventGridEvent.Data.ToString());
                }

                if (isSuccess)
                {
                    logger.LogInformation($"EventGridDeviceCreatedOrDeleted function completed successfully.");
                }
                else
                {
                    logger.LogError($"EventGridDeviceCreatedOrDeleted function completed with errors.");
                }
            }
            else
            {
                logger.LogInformation($"EventGridDeviceCreatedOrDeleted function received unsupported event: {eventGridEvent.EventType}.");
            }
        }

        [FunctionName("TimerTriggerSyncIotHubs")]
        public static async Task TimerTriggerSyncIotHubs([TimerTrigger("0 0 * * * *")] TimerInfo myTimer, ILogger logger, ExecutionContext context)
        {
            logger.LogInformation($"TimerTriggerSyncIotHubs function started.");

            var deviceSynchronizer = new DeviceSynchronizer(GetConnectionStrings(context), logger);

            var isSuccess = await deviceSynchronizer.SyncIotHubsAsync();

            if (isSuccess)
            {
                logger.LogInformation($"TimerTriggerSyncIotHubs function completed successfully.");
            }
            else
            {
                logger.LogError($"TimerTriggerSyncIotHubs function completed with errors.");
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
