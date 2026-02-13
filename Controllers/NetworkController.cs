using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace PottaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NetworkController : ControllerBase
    {
        /// <summary>Get QR code for mobile app connection</summary>
        [HttpGet("qr-string")]
        [ResponseCache(Duration = 10)]
        public IActionResult GetQRCodeString()
        {
            var primaryIp = GetPrimaryLocalIPAddress();
            var port = HttpContext.Request.Host.Port ?? 5001;
            var baseUrl = $"http://{primaryIp}:{port}";
            
            // Clean structure for QR code content (what mobile app will scan)
            var qrData = new
            {
                type = "pottapos_api",
                ip = primaryIp,
                port = port,
                url = baseUrl,
                apiVersion = "1.0.0",
                serverName = Environment.MachineName,
                timestamp = DateTime.UtcNow.ToString("o")
            };

            var jsonString = JsonSerializer.Serialize(qrData, new JsonSerializerOptions 
            { 
                WriteIndented = false // Compact JSON for QR code
            });

            // Response for desktop app
            return Ok(new
            {
                success = true,
                message = "QR code data generated",
                qrString = jsonString,
                displayUrl = baseUrl,
                // Also provide parsed data for convenience
                data = new
                {
                    ip = primaryIp,
                    port = port,
                    url = baseUrl,
                    serverName = Environment.MachineName,
                    apiVersion = "1.0.0"
                }
            });
        }

        private List<string> GetLocalIPAddresses()
        {
            var ipAddresses = new List<string>();

            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipAddresses.Add(ip.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting IP addresses: {ex.Message}");
            }

            return ipAddresses;
        }

        private string GetPrimaryLocalIPAddress()
        {
            try
            {
                // Try to get the IP address by connecting to an external address
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint? endPoint = socket.LocalEndPoint as IPEndPoint;
                    if (endPoint != null)
                    {
                        return endPoint.Address.ToString();
                    }
                }
            }
            catch
            {
                // Fallback to first non-loopback address
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }

            return "127.0.0.1";
        }
    }
}
