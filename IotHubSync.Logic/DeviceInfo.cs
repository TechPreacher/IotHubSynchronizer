// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace IotHubSync.Logic
{
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;

    class DeviceInfo
    {
        public Device Device { get; set; }
        public Twin Twin { get; set; }
        public bool ExistsInMaster { get; set; }

        public DeviceInfo()
        {
        }

        public DeviceInfo(Device device, Twin twin, bool existsInMaster)
        {
            Device = device;
            Twin = twin;
            ExistsInMaster = existsInMaster;
        }
    }
}
