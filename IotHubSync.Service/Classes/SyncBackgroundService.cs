// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace IotHubSync.Service.Classes
{
    using IotHubSync.Service.Interfaces;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class SyncBackgroundService : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;
        private readonly IDeviceSynchronizerSingleton _deviceSynchronizerSingleton;
        private readonly ISemaphoreSingleton _semaphoreSingleton;

        public SyncBackgroundService(
            IConfiguration config, 
            ILogger<SyncBackgroundService> logger, 
            IDeviceSynchronizerSingleton deviceSynchronizerSingleton,
            ISemaphoreSingleton semaphoreSingleton)
        {
            _config = config;
            _logger = logger;
            _deviceSynchronizerSingleton = deviceSynchronizerSingleton;
            _semaphoreSingleton = semaphoreSingleton;
        }

        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug($"SyncBackgroundService task is starting.");

            stoppingToken.Register(() => _logger.LogDebug($"SyncBackgroundService task is stopping."));

            while (!stoppingToken.IsCancellationRequested)
            {
                await _semaphoreSingleton.Semaphore.WaitAsync();

                _logger.LogDebug($"SyncBackgroundService is synchronizing IoT Hubs.");

                var isSuccess = await _deviceSynchronizerSingleton.DeviceSynchronizer.SyncIotHubsAsync();

                _semaphoreSingleton.Semaphore.Release();

                if (isSuccess)
                {
                    _logger.LogInformation($"{Helpers.GetTimestamp()}: Timed IoT Hub synchronization completed successfully.");
                }
                else
                {
                    _logger.LogInformation($"{Helpers.GetTimestamp()}: Timed IoT Hub synchronization completed with errors.");
                }

                _logger.LogDebug($"SyncBackgroundService is sleeping.");

                await Task.Delay(
                    (int)TimeSpan.FromMinutes(_config.GetValue<int>(Constants.SyncTimerMinutes)).TotalMilliseconds, 
                    stoppingToken);
            }

            _logger.LogDebug($"SyncBackgroundService task is stopping.");
        }
    }
}
