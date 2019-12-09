// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace IotHubSync.Service.Classes
{
    using IotHubSync.Service.Interfaces;
    using System.Threading;

    public class SemaphoreSingleton : ISemaphoreSingleton
    {
        public SemaphoreSlim Semaphore { get; set; }

        public SemaphoreSingleton()
        {
            Semaphore = new SemaphoreSlim(1, 1);
        }
    }
}
