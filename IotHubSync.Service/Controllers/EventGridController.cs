// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace IotHubSync.Service.Controllers
{
    using System;
    using System.Threading.Tasks;
    using Azure.Messaging.EventGrid;
    using Azure.Messaging.EventGrid.SystemEvents;
    using IotHubSync.Service.Interfaces;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [Route("api"), AllowAnonymous]
    [ApiController]
    public class EventGridController : ControllerBase
    {
        private static readonly string EventGridIotHubDeviceCreatedEvent = "Microsoft.Devices.DeviceCreated";
        private static readonly string EventGridIotHubDeviceDeletedEvent = "Microsoft.Devices.DeviceDeleted";

        private readonly ILogger _logger;
        private readonly IDeviceSynchronizerSingleton _deviceSynchronizerSingleton;
        private readonly ISemaphoreSingleton _semaphoreSingleton;

        public EventGridController(
            ILogger<EventGridController> logger, 
            IDeviceSynchronizerSingleton deviceSynchronizerSingleton,
            ISemaphoreSingleton semaphoreSingleton)
        {
            _logger = logger;
            _deviceSynchronizerSingleton = deviceSynchronizerSingleton;
            _semaphoreSingleton = semaphoreSingleton;
        }

        // GET /api/test
        [HttpGet("test")]
        public ActionResult<string> Test()
        {
            return "IotHubSync.Service is up.";
        }

        // GET /api/sync
        [HttpGet("sync")]
        public async Task<ActionResult<string>> Sync()
        {
            bool isSuccess;
            _logger.LogInformation($"Api triggered IoT Hub synchronization has been triggered.");

            try
            {
                await _semaphoreSingleton.Semaphore.WaitAsync();

                _logger.LogDebug($"Api triggered IoT Hub synchronization is synchronizing IoT Hubs.");
                isSuccess = await _deviceSynchronizerSingleton.DeviceSynchronizer.SyncIotHubsAsync();
            }

            finally
            {
                _semaphoreSingleton.Semaphore.Release();
            }

            if (isSuccess)
            {
                var sResponse = $"Api triggered IoT Hub synchronization completed successfully.";
                _logger.LogInformation(sResponse);
                return Ok(sResponse);
            }
            else
            {
                var sResponse = $"Api triggered IoT Hub synchronization completed with errors.";
                _logger.LogError(sResponse);
                return BadRequest(sResponse);
            }
        }

        // POST /api/eventgrid_device_created_deleted
        // Set to AllowAnonymous so that Azure EventGrid can call it.
        // Will only execute if a valid EventGrid EventType is received. Returns Bad Request otherwise.
        [HttpPost("eventgrid_device_created_deleted"), AllowAnonymous]
        public async Task<IActionResult> EventGridDeviceCreatedOrDeleted([FromBody] object request)
        {
            var isSuccess = true;

            EventGridEvent[] eventGridEvents = EventGridEvent.ParseMany(BinaryData.FromString(request.ToString()));

            foreach (EventGridEvent eventGridEvent in eventGridEvents)
            {
                // Handle system events
                if (eventGridEvent.TryGetSystemEventData(out object eventData))
                {
                    // Handle the subscription validation event
                    if (eventData is SubscriptionValidationEventData subscriptionValidationEventData)
                    {
                        _logger.LogInformation($"Received SubscriptionValidation event data, validation code: {subscriptionValidationEventData.ValidationCode}, topic: {eventGridEvent.Topic}");

                        var responseData = new SubscriptionValidationResponse()
                        {
                            ValidationResponse = subscriptionValidationEventData.ValidationCode
                        };
                        return new OkObjectResult(responseData);
                    }

                    if (eventData is IotHubDeviceCreatedEventData deviceCreatedData)
                    {
                        try
                        {
                            await _semaphoreSingleton.Semaphore.WaitAsync();
                            _logger.LogInformation($"Received IotHubDeviceCreatedEventData event from EventGrid.");
                            isSuccess = await IoTHubDeviceCreated(isSuccess, deviceCreatedData);
                        }
                        finally
                        {
                            _semaphoreSingleton.Semaphore.Release();
                        }
                    }

                    if (eventData is IotHubDeviceDeletedEventData deviceDeletedData)
                    {
                        try
                        {
                            await _semaphoreSingleton.Semaphore.WaitAsync();
                            _logger.LogInformation($"Received IotHubDeviceCreatedEventData event from EventGrid.");
                            isSuccess = await IoTHubDeviceDeleted(isSuccess, deviceDeletedData);
                        }
                        finally
                        {
                            _semaphoreSingleton.Semaphore.Release();
                        }
                    }
                }
            }

            if (isSuccess)
            {
                return Ok();
            }
            else
            {
                return BadRequest();
            }
        }

        private async Task<bool> IoTHubDeviceDeleted(bool isSuccess, IotHubDeviceDeletedEventData deletedEventData)
        {

            _logger.LogInformation($"DeviceId: {deletedEventData.DeviceId}.");
            isSuccess = await _deviceSynchronizerSingleton.DeviceSynchronizer.DeleteDeviceFromDeviceId(deletedEventData.DeviceId);

            if (isSuccess)
            {
                _logger.LogInformation($"Triggered IoT Hub device deletion completed successfully.");
            }
            else
            {
                _logger.LogError($"Triggered IoT Hub device deletion completed with errors.");
            }

            return isSuccess;
        }

        private async Task<bool> IoTHubDeviceCreated(bool isSuccess, IotHubDeviceCreatedEventData createdEventData)
        {

            _logger.LogInformation($"DeviceId: {createdEventData.DeviceId}.");
            isSuccess = await _deviceSynchronizerSingleton.DeviceSynchronizer.CreateDeviceFromDeviceId(createdEventData.DeviceId);

            if (isSuccess)
            {
                _logger.LogInformation($"Triggered IoT Hub device creation completed successfully.");
            }
            else
            {
                _logger.LogError($"Triggered IoT Hub device creation completed with errors.");
            }

            return isSuccess;
        }
    }
}
