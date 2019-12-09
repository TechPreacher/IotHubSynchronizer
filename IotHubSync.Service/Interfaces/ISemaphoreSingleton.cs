// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace IotHubSync.Service.Interfaces
{
    using System.Threading;

    public interface ISemaphoreSingleton
    {
        SemaphoreSlim Semaphore { get; set; }
    }
}