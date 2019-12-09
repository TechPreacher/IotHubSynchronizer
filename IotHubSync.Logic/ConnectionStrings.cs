// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace IotHubSync.Logic
{
    public class ConnectionStrings
    {
        public string ConnectionStringMaster { get; set; }
        public string ConnectionStringSlave { get; set; }

        public ConnectionStrings(string connectionStringMaster, string connectionStringSlave)
        {
            ConnectionStringMaster = connectionStringMaster;
            ConnectionStringSlave = connectionStringSlave;
        }
    }
}
