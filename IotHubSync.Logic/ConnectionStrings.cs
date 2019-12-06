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
