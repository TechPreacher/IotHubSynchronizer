// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace IotHubSync.Logic
{
    using Microsoft.Azure.Devices;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class DeviceSynchronizer
    {
        private readonly ILogger _logger;
        private readonly bool _isValidConnectionStrings;
        private readonly string _connectionStringMaster, _connectionStringSlave;

        public DeviceSynchronizer(ConnectionStrings connectionStrings, ILogger logger)
        {
            _connectionStringMaster = connectionStrings.ConnectionStringMaster;
            _connectionStringSlave = connectionStrings.ConnectionStringSlave;
            _logger = logger;

            // Validate Connection Strings
            _isValidConnectionStrings = ValidateConnectionString(_connectionStringMaster, IotHubType.Master)
                && ValidateConnectionString(_connectionStringSlave, IotHubType.Slave);
        }

        public async Task<bool> CreateDevice(string eventGridData)
        {
            bool isSuccess = true;

            // Only run if connection strings are valid
            if (!_isValidConnectionStrings)
            {
                throw new InvalidOperationException("Can not run with invalid connection strings.");
            }

            string deviceIdMaster;

            try
            {
                var jobjectTwinMaster = JObject.Parse(eventGridData);
                deviceIdMaster = (string)jobjectTwinMaster["twin"]["deviceId"];
            }
            catch (JsonReaderException ex)
            {
                _logger.LogError(ex, $"Invalid information received from Event Grid.");
                return false;
            }

            if (!ConnectRegistryManager(out RegistryManager registryManagerMaster, IotHubType.Master) ||
                !ConnectRegistryManager(out RegistryManager registryManagerSlave, IotHubType.Slave))
            {
                return false;
            }

            // Get Device from Master IoT Hub
            var deviceListMaster = await GetDeviceListFromIotHub(registryManagerMaster, deviceIdMaster, IotHubType.Master);

            if (deviceListMaster.Count == 0)
            {
                _logger.LogError($"{deviceIdMaster}: Can not find device in Master IoT Hub.");
                isSuccess = false;
            }

            // Add device to Slave IoT Hub
            if (isSuccess)
            {
                isSuccess = await AddDeviceToIotHub(true, registryManagerSlave, deviceListMaster[0], IotHubType.Slave);
            }

            await registryManagerMaster.CloseAsync();
            await registryManagerSlave.CloseAsync();

            return isSuccess;
        }

        public async Task<bool> DeleteDevice(string eventGridData)
        {
            bool isSuccess = true;

            // Only run if connection strings are valid
            if (!_isValidConnectionStrings)
            {
                throw new InvalidOperationException("Can not run with invalid connection strings.");
            }

            string deviceIdMaster;
            try
            {
                var jobjectTwinMaster = JObject.Parse(eventGridData);
                deviceIdMaster = (string)jobjectTwinMaster["twin"]["deviceId"];
            }
            catch (JsonReaderException ex)
            {
                _logger.LogError(ex, $"Invalid information received from Event Grid.");
                return false;
            }

            if (!ConnectRegistryManager(out RegistryManager registryManagerSlave, IotHubType.Slave))
            {
                return false;
            }

            // Remove device from Slave IoT Hub
            try
            {
                await registryManagerSlave.RemoveDeviceAsync(deviceIdMaster);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{deviceIdMaster}: Can not remove device from Slave IoT Hub.");
                isSuccess = false;
            }
            finally
            {
                await registryManagerSlave.CloseAsync();
            }

            return isSuccess;
        }

        public async Task<bool> SyncIotHubsAsync()
        {
            bool isSuccess = true;

            // Only run if connection strings are valid
            if (!_isValidConnectionStrings)
            {
                _logger.LogError($"Can not run with invalid connection strings.");
                return false;
            }

            if (!ConnectRegistryManager(out RegistryManager registryManagerMaster, IotHubType.Master) ||
                !ConnectRegistryManager(out RegistryManager registryManagerSlave, IotHubType.Slave))
            {
                return false;
            }

            _logger.LogInformation($"IoT Hub synchronization started.");

            // Get Master device list
            var deviceListMaster = await GetDeviceListFromIotHub(registryManagerMaster, null, IotHubType.Master);
            _logger.LogInformation($"Found {deviceListMaster.Count} devices in Master IoT Hub.");

            // Get Slave device list
            var deviceListSlave = await GetDeviceListFromIotHub(registryManagerSlave, null, IotHubType.Slave);
            _logger.LogInformation($"Found {deviceListSlave.Count} devices in Slave IoT Hub.");

            foreach (var deviceInfoMaster in deviceListMaster)
            {
                var deviceInfoSlave = deviceListSlave.Find(r => r.Device.Id == deviceInfoMaster.Device.Id);

                // Device does not exist in Slave: Add
                if (deviceInfoSlave == null)
                {
                    isSuccess = await AddDeviceToIotHub(isSuccess, registryManagerSlave, deviceInfoMaster, IotHubType.Slave);
                }

                // Device exists in Slave: Verify and Update
                else
                {
                    isSuccess = await VerifyAndUpdateDeviceInIotHub(isSuccess, deviceListSlave, deviceInfoMaster, deviceInfoSlave, registryManagerSlave);
                    isSuccess = await CompareTwinDesiredPropertiesInIotHub(isSuccess, deviceInfoMaster, deviceInfoSlave, registryManagerSlave);
                }
            }

            // Delete the devices that no longer exist in Master
            isSuccess = await DeleteObsoleteDevicesFromIotHub(isSuccess, deviceListSlave, registryManagerSlave);

            if (isSuccess)
            {
                _logger.LogDebug($"Master/Slave IoT Hub synchronization completed with no errors.");
            }
            else
            {
                _logger.LogError($"Master/Slave IoT Hub synchronization completed with errors.");
            }

            await registryManagerMaster.CloseAsync();
            await registryManagerSlave.CloseAsync();

            return isSuccess;
        }

        private bool ValidateConnectionString(string connectionString, IotHubType type)
        {
            // Validate Master connection string
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError($"{type} IoT Hub connection string not found in configuration.");
                return false;
            }
            else
            {
                // Just show Master IoT Hub Hostname
                if (TryGetHostFromConnectionString(connectionString, out string hostName))
                {
                    _logger.LogDebug($"Using {type} IoT Hub: {hostName}");
                }
                else
                {
                    _logger.LogError($"Invalid {type} IoT Hub connection string in configuration. Can not find \"HostName=\".");
                    return false;
                }
            }
            return true;
        }

        private bool ConnectRegistryManager(out RegistryManager registryManager, IotHubType type)
        {
            string connectionString;

            if (type == IotHubType.Master)
            {
                connectionString = _connectionStringMaster;
            }
            else
            {
                connectionString = _connectionStringSlave;
            }

            try
            {
                registryManager = RegistryManager.CreateFromConnectionString(connectionString);
                _logger.LogDebug($"Connected to {type} IoT Hub.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Can not connect to {type} IoT Hub.");
                registryManager = null;
                return false;
            }

            return true;
        }

        private async Task<List<DeviceInfo>> GetDeviceListFromIotHub(RegistryManager registryManager, string deviceId, IotHubType type)
        {
            var deviceInfoList = new List<DeviceInfo>();
            var queryString = "SELECT * FROM devices";

            if (!string.IsNullOrEmpty(deviceId))
            {
                queryString += $"  WHERE deviceId = '{deviceId}'";
            }

            try
            {
                var query = registryManager.CreateQuery(queryString);

                while (query.HasMoreResults)
                {
                    var twinList = await query.GetNextAsTwinAsync();

                    foreach (var twin in twinList)
                    {
                        var device = await registryManager.GetDeviceAsync(twin.DeviceId);

                        device.ETag = null;

                        deviceInfoList.Add(new DeviceInfo(device, twin, false));
                        await PreventIotHubThrottling();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed loading devices from {type} IoT Hub.");
                deviceInfoList = new List<DeviceInfo>();
                if (ExceptionHandler.IsFatal(ex))
                {
                    throw;
                }
            }

            return deviceInfoList;
        }

        private async Task<bool> AddDeviceToIotHub(bool isSuccess, RegistryManager registryManager, DeviceInfo deviceInfo, IotHubType type)
        {
            var deviceInfoSlave = new DeviceInfo();

            // Prepare Scope
            if (!string.IsNullOrEmpty(deviceInfo.Device.Scope))
            {
                string edgeDeviceIdSlave = GetDeviceIdFromScope(deviceInfo.Device.Scope);

                if (string.IsNullOrEmpty(edgeDeviceIdSlave))
                {
                    _logger.LogError($"{deviceInfo.Device.Id} has invalid Edge device scope: {deviceInfo.Device.Scope}.");
                }

                else if (deviceInfo.Device.Id != edgeDeviceIdSlave)
                {
                    Device edgeDeviceInfoSlave = null;
                    try
                    {
                        edgeDeviceInfoSlave = await registryManager.GetDeviceAsync(edgeDeviceIdSlave);
                        await PreventIotHubThrottling();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"{edgeDeviceIdSlave}: Failed loading device from {type} IoT Hub.");
                    }

                    if (edgeDeviceInfoSlave != null)
                    {
                        deviceInfo.Device.Scope = edgeDeviceInfoSlave.Scope;
                    }
                    else
                    {
                        _logger.LogWarning($"{deviceInfo.Device.Id}: Device has Edge device parent of {edgeDeviceIdSlave} but that device has not been created in Slave IoT Hub yet. It will be added at next run.");
                    }
                }
            }

            // Add device to IoT Hub
            try
            {
                deviceInfoSlave.Device = await registryManager.AddDeviceAsync(deviceInfo.Device);
                await PreventIotHubThrottling();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{deviceInfo.Device.Id}: Can not add device to {type} IoT hub.");
                isSuccess = false;
            }

            // Get Device Twin from IoT Hub
            try
            {
                deviceInfoSlave.Twin = await registryManager.GetTwinAsync(deviceInfo.Device.Id);
                await PreventIotHubThrottling();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{deviceInfo.Device.Id}: Can not read device Twin from {type} IoT Hub.");
                isSuccess = false;
            }

            // Update Device Twin in IoT Hub
            try
            {
                deviceInfoSlave.Twin = await registryManager.UpdateTwinAsync(deviceInfo.Device.Id, deviceInfo.Twin, deviceInfoSlave.Twin.ETag);
                await PreventIotHubThrottling();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{deviceInfo.Device.Id}: Can not update device Twin in {type} IoT Hub.");
                isSuccess = false;
            }

            return isSuccess;
        }

        private async Task<bool> VerifyAndUpdateDeviceInIotHub(bool isSuccess, List<DeviceInfo> deviceListSlave, DeviceInfo deviceInfoMaster, DeviceInfo deviceInfoSlave, RegistryManager registryManagerSlave)
        {
            var isStale = false;

            // Mark as existing in Master
            deviceInfoSlave.ExistsInMaster = true;

            // Update Authentication Symmetric Keys if not matching
            if (deviceInfoMaster.Device.Authentication.SymmetricKey.PrimaryKey != deviceInfoSlave.Device.Authentication.SymmetricKey.PrimaryKey
                || deviceInfoMaster.Device.Authentication.SymmetricKey.SecondaryKey != deviceInfoSlave.Device.Authentication.SymmetricKey.SecondaryKey)
            {
                deviceInfoSlave.Device.Authentication.SymmetricKey = deviceInfoMaster.Device.Authentication.SymmetricKey;
                isStale = true;
            }

            // Update Device Status Info if not matching
            if (deviceInfoMaster.Device.Status != deviceInfoSlave.Device.Status)
            {
                deviceInfoSlave.Device.Status = deviceInfoMaster.Device.Status;
                isStale = true;
            }

            // Compare Device Scope
            isStale = CompareDeviceScopeInIotHub(deviceListSlave, deviceInfoMaster, deviceInfoSlave, isStale);

            // Do we need to write the Slave data?
            if (isStale)
            {
                try
                {
                    await registryManagerSlave.UpdateDeviceAsync(deviceInfoSlave.Device, true);
                    await PreventIotHubThrottling();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"{deviceInfoMaster.Device.Id}: Can not update device info in Slave IoT Hub.");
                    isSuccess = false;
                }
            }

            return isSuccess;
        }

        private bool CompareDeviceScopeInIotHub(List<DeviceInfo> deviceListSlave, DeviceInfo deviceInfoMaster, DeviceInfo deviceInfoSlave, bool isStale)
        {
            if (!string.IsNullOrEmpty(deviceInfoMaster.Device.Scope))
            {
                var edgeDeviceIdMaster = GetDeviceIdFromScope(deviceInfoMaster.Device.Scope);

                if (string.IsNullOrEmpty(edgeDeviceIdMaster))
                {
                    _logger.LogError($"{deviceInfoMaster.Device.Id} has invalid Edge device scope: {deviceInfoMaster.Device.Scope}.");
                }

                else if (deviceInfoMaster.Device.Id != edgeDeviceIdMaster)
                {
                    var edgeDeviceInfoSlave = deviceListSlave.Find(r => r.Device.Id == edgeDeviceIdMaster);

                    if (edgeDeviceInfoSlave != null)
                    {
                        var scopeEdgeSlave = edgeDeviceInfoSlave.Device.Scope;

                        // Update Device Scope Info
                        if (scopeEdgeSlave != deviceInfoSlave.Device.Scope)
                        {
                            deviceInfoSlave.Device.Scope = scopeEdgeSlave;
                            isStale = true;
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"{deviceInfoMaster.Device.Id}: Device has Edge device parent of {edgeDeviceIdMaster} but that device has not been created in Slave IoT Hub yet. It will be added at next run.");
                    }
                }
            }

            return isStale;
        }

        private async Task<bool> CompareTwinDesiredPropertiesInIotHub(bool isSuccess, DeviceInfo deviceInfoMaster, DeviceInfo deviceInfoSlave, RegistryManager registryManagerSlave)
        {
            // Compare Twin Desired Properties
            JObject jsonTwinMaster = JObject.Parse(deviceInfoMaster.Twin.Properties.Desired.ToJson());
            jsonTwinMaster.Remove("$metadata");
            jsonTwinMaster.Remove("$version");

            JObject jsonTwinSlave = JObject.Parse(deviceInfoSlave.Twin.Properties.Desired.ToJson());
            jsonTwinSlave.Remove("$metadata");
            jsonTwinSlave.Remove("$version");


            // Update Device Twin desired properties
            if (!JToken.DeepEquals(jsonTwinMaster, jsonTwinSlave))
            {
                deviceInfoSlave.Twin.Properties.Desired = deviceInfoMaster.Twin.Properties.Desired;

                try
                {
                    await registryManagerSlave.UpdateTwinAsync(deviceInfoSlave.Device.Id, deviceInfoSlave.Twin, deviceInfoSlave.Twin.ETag);
                    await PreventIotHubThrottling();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"{deviceInfoMaster.Device.Id}: Can not update device Twin in Slave IoT Hub.");
                    isSuccess = false;
                }
            }

            return isSuccess;
        }

        private async Task<bool> DeleteObsoleteDevicesFromIotHub(bool isSuccess, List<DeviceInfo> deviceListSlave, RegistryManager registryManagerSlave)
        {
            List<Device> devicesToRemove = new List<Device>();

            foreach (var deviceInfoSlave in deviceListSlave)
            {
                if (!deviceInfoSlave.ExistsInMaster)
                {
                    devicesToRemove.Add(deviceInfoSlave.Device);
                }
            }

            // Bulk delete extra/stale devices from Slave IoT Hub.
            if (devicesToRemove.Count > 0)
            {
                var devicesToRemoveSplit = SplitList(devicesToRemove, Constants.SplitListSize);

                foreach (var splitList in devicesToRemoveSplit)
                {
                    try
                    {
                        BulkRegistryOperationResult result;
                        result = await registryManagerSlave.RemoveDevices2Async(splitList, true, new System.Threading.CancellationToken());
                        await PreventIotHubThrottling();

                        foreach (var error in result.Errors)
                        {
                            _logger.LogError($"{error.DeviceId}: {error.ErrorStatus}");
                            isSuccess = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Can not remove devices.");
                        isSuccess = false;
                    }
                }
            }

            return isSuccess;
        }

        private static string GetDeviceIdFromScope(string scope)
        {
            var startPoint = scope.LastIndexOf("/", StringComparison.OrdinalIgnoreCase);
            var endPoint = scope.LastIndexOf("-", StringComparison.OrdinalIgnoreCase);

            if (startPoint == -1 || endPoint == -1 || startPoint - endPoint >= 0)
            {
                return null;
            }
            else
            {
                return scope.Substring(startPoint + 1, endPoint - startPoint - 1);
            }
        }

        private IEnumerable<List<T>> SplitList<T>(List<T> input, int nSize)
        {
            for (int i = 0; i < input.Count; i += nSize)
            {
                yield return input.GetRange(i, Math.Min(nSize, input.Count - i));
            }
        }

        private bool TryGetHostFromConnectionString(string connectionString, out string hostName)
        {
            hostName = null;

            const string hostNameInConnectionString = "HostName=";
            const string iotHubFqdnInConnectionString = "azure-devices.net";

            var from = connectionString.IndexOf(hostNameInConnectionString, StringComparison.OrdinalIgnoreCase);
            if (from == -1)
            {
                return false;
            }

            var fromOffset = hostNameInConnectionString.Length;

            var to = connectionString.IndexOf(iotHubFqdnInConnectionString, StringComparison.OrdinalIgnoreCase);
            if (to == -1)
            {
                return false;
            }

            var length = to - from - fromOffset + "azure-devices.net".Length;

            if (from + fromOffset + length < connectionString.Length)
            {
                hostName = connectionString.Substring(from + fromOffset, length);
            }

            return hostName != null;

        }

        private static async Task PreventIotHubThrottling()
        {
            // Wait for 500ms to prevent IoT Hub throttling.
            await Task.Delay(Constants.IotHubThrottlingTimeoutInMilliseconds);
        }
    }
}
