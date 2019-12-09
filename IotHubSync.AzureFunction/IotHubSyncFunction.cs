// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace IotHubSync.AzureFunction
{
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.EventGrid.Models;
    using Microsoft.Azure.WebJobs.Extensions.EventGrid;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;
    using IotHubSync.Logic;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Azure.EventGrid;

    public static class IotHubSyncFunction
    {
        private static readonly string IotHubConnectionStringMasterKey = "IotHubConnectionStringMaster";
        private static readonly string IotHubConnectionStringSlaveKey = "IotHubConnectionStringSlave";

        [FunctionName("EventGridDeviceCreatedOrDeleted")]
        public static async Task EventGridDeviceCreatedOrDeleted([EventGridTrigger]EventGridEvent eventGridEvent, ILogger logger, ExecutionContext context)
        {
            bool isSuccess = true;

            if (eventGridEvent.EventType == EventTypes.IoTHubDeviceCreatedEvent ||
                eventGridEvent.EventType == EventTypes.IoTHubDeviceDeletedEvent)
            {
                var deviceSynchronizer = new DeviceSynchronizer(GetConnectionStrings(context), logger);

                if (eventGridEvent.EventType == EventTypes.IoTHubDeviceCreatedEvent)
                {
                    logger.LogInformation($"EventGridDeviceCreatedOrDeleted function received IotHubDeviceCreatedEventData event from EventGrid.");
                    isSuccess = await deviceSynchronizer.CreateDevice(eventGridEvent.Data.ToString());
                }

                else if (eventGridEvent.EventType == EventTypes.IoTHubDeviceDeletedEvent)
                {
                    logger.LogInformation($"EventGridDeviceCreatedOrDeleted function received IotHubDeviceDeletedEventData event from EventGrid.");
                    isSuccess = await deviceSynchronizer.DeleteDevice(eventGridEvent.Data.ToString());
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
        public static async Task TimerTriggerSyncIotHubs([TimerTrigger("0 0 * * * *")]TimerInfo myTimer, ILogger logger, ExecutionContext context)
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