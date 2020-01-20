// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace IotHubSync.TestConsoleApp
{
    using IotHubSync.Logic;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using System.IO;
    using System.Threading.Tasks;

    class Program
    {
        private static readonly string IotHubConnectionStringMasterKey = "IotHubConnectionStringMaster";
        private static readonly string IotHubConnectionStringSlaveKey = "IotHubConnectionStringSlave";

        private enum ExitCode : int
        {
            Error = -1,
            Success = 0,
            InvalidLogin = 1,
            InvalidFilename = 2,
            UnknownError = 10
        }

        static async Task<int> Main(string[] args)
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("LoggingConsoleApp.Program", LogLevel.Debug)
                    .SetMinimumLevel(LogLevel.Debug)
                    .AddConsole()
                    .AddDebug()
                    .AddEventLog();
            });
            ILogger logger = loggerFactory.CreateLogger<Program>();

            var deviceSynchronizer = new DeviceSynchronizer(GetConnectionStrings(), logger);

            var isSuccess = await deviceSynchronizer.SyncIotHubsAsync();

            if (isSuccess)
            {
                logger.LogInformation("IoT Hub synchronization completed successfully.");
                return (int)ExitCode.Success;
            }
            else
            {
                logger.LogError("IoT Hub synchronization completed with errors.");
                return (int)ExitCode.Error;
            }
        }

        private static ConnectionStrings GetConnectionStrings()
        {
            // Read configuration
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddEnvironmentVariables();

            IConfigurationRoot config = builder.Build();

            var connectionStrings = new ConnectionStrings(
                config.GetValue<string>(IotHubConnectionStringMasterKey),
                config.GetValue<string>(IotHubConnectionStringSlaveKey));
            return connectionStrings;
        }
    }
}
