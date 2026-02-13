namespace PottaAPI.Models
{
    // Network information for API discovery
    public class NetworkInfoDto
    {
        public List<string> LocalIpAddresses { get; set; } = new();
        public string HostName { get; set; } = "";
        public int Port { get; set; }
        public List<string> ApiBaseUrls { get; set; } = new();
        public string MachineName { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    // QR code data for mobile device connection
    public class QRCodeDataDto
    {
        public string ApiUrl { get; set; } = "";
        public string HostName { get; set; } = "";
        public string MachineName { get; set; } = "";
        public int Port { get; set; }
        public string Version { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    // Network interface information
    public class NetworkInterfaceDto
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Type { get; set; } = "";
        public string Status { get; set; } = "";
        public long Speed { get; set; }
        public string MacAddress { get; set; } = "";
        public List<string> IpAddresses { get; set; } = new();
    }

    // Test connection request from device
    public class TestConnectionDto
    {
        public string DeviceName { get; set; } = "";
        public string DeviceType { get; set; } = "";
        public string AppVersion { get; set; } = "";
    }
}
