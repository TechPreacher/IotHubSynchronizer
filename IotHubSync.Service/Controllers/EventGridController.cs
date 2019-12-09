// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace IotHubSync.Service.Controllers
{
    using System;
    using System.Threading.Tasks;
    using IotHubSync.Service.Interfaces;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.EventGrid;
    using Microsoft.Azure.EventGrid.Models;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json.Linq;

    [Route("api"), AllowAnonymous]
    [ApiController]
    public class EventGridController : ControllerBase
    {
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
        public async Task<IActionResult> EventGridDeviceCreatedOrDeleted([FromBody] EventGridEvent[] events)
        {
            var isSuccess = true;

            foreach (EventGridEvent receivedEvent in events)
            {
                if (receivedEvent.EventType == EventTypes.IoTHubDeviceCreatedEvent ||
                    receivedEvent.EventType == EventTypes.IoTHubDeviceDeletedEvent)
                {
                    try
                    {
                        await _semaphoreSingleton.Semaphore.WaitAsync();

                        if (receivedEvent.EventType == EventTypes.IoTHubDeviceCreatedEvent)
                        {
                            _logger.LogInformation($"Received IotHubDeviceCreatedEventData event from EventGrid.");
                            isSuccess = await IoTHubDeviceCreated(isSuccess, receivedEvent);
                        }

                        else if (receivedEvent.EventType == EventTypes.IoTHubDeviceDeletedEvent)
                        {
                            _logger.LogInformation($"Received IotHubDeviceDeletedEventData event from EventGrid.");
                            isSuccess = await IoTHubDeviceDeleted(isSuccess, receivedEvent);
                        }
                    }

                    finally
                    {
                        _semaphoreSingleton.Semaphore.Release();
                    }
                }

                else if (receivedEvent.EventType == EventTypes.EventGridSubscriptionValidationEvent)
                {
                    _logger.LogInformation($"Received SubscriptionValidation event from EventGrid.");

                    SubscriptionValidationEventData eventData;
                    try
                    {
                        if (receivedEvent.Data != null && receivedEvent.Data is JObject jObject)
                        {
                            eventData = jObject.ToObject<SubscriptionValidationEventData>();
                        }
                        else
                        {
                            throw new ArgumentException("Data is null or not a JObject", nameof(events));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Invalid data received from EventGrid.");
                        return BadRequest();
                    }

                    _logger.LogInformation($"Validation code: {eventData.ValidationCode}, topic: {receivedEvent.Topic}");

                    var response = new SubscriptionValidationResponse(eventData.ValidationCode);

                    return Ok(response);
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

        private async Task<bool> IoTHubDeviceDeleted(bool isSuccess, EventGridEvent receivedEvent)
        {
            IotHubDeviceDeletedEventData eventData = null;
            try
            {
                if (receivedEvent.Data is JObject jObject)
                {
                    eventData = jObject.ToObject<IotHubDeviceDeletedEventData>();
                }
                else
                {
                    throw new ArgumentException("Data is null or not a JObject", nameof(receivedEvent));
                }
            }
            catch (Exception ex)
            {
                isSuccess = false;
                _logger.LogError(ex, $"Invalid data received from EventGrid.");
            }

            if (isSuccess)
            {
                _logger.LogInformation($"DeviceId: {eventData.DeviceId}.");
                isSuccess = await _deviceSynchronizerSingleton.DeviceSynchronizer.DeleteDevice(receivedEvent.Data.ToString());
            }

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

        private async Task<bool> IoTHubDeviceCreated(bool isSuccess, EventGridEvent receivedEvent)
        {
            IotHubDeviceCreatedEventData eventData = null;
            try
            {
                if (receivedEvent.Data is JObject jObject)
                {
                    eventData = jObject.ToObject<IotHubDeviceCreatedEventData>();
                }
                else
                {
                    throw new ArgumentException("Data is null or not a JObject", nameof(receivedEvent));
                }
            }
            catch (Exception ex)
            {
                isSuccess = false;
                _logger.LogError(ex, $"Invalid data received from EventGrid.");
            }

            if (isSuccess)
            {
                _logger.LogInformation($"DeviceId: {eventData.DeviceId}.");
                isSuccess = await _deviceSynchronizerSingleton.DeviceSynchronizer.CreateDevice(receivedEvent.Data.ToString());
            }

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
