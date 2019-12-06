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
