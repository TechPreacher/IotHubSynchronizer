// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace IotHubSync.Service.Classes
{
    using System;
    using System.Globalization;

    public static class Helpers
    {
        public static string GetTimestamp()
        {
            return DateTime.UtcNow.ToString(new CultureInfo("en-US"));
        }
    }
}
