using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PottaAPI.Configuration;
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
        private readonly ApiOptions _apiOptions;

        public NetworkController(IOptions<ApiOptions> apiOptions)
        {
            _apiOptions = apiOptions.Value;
        }

        /// <summary>Get QR code string for mobile app to scan and connect to this API server.</summary>
        /// <remarks>
        /// Returns a JSON string that encodes the server's IP, port, and URL.
        /// The mobile app scans this as a QR code to auto-configure its API base URL.
        ///
        /// If <c>ExternalUrl</c> is set in appsettings (e.g. an ngrok tunnel), that URL is used.
        /// Otherwise the server's local network IP is used.
        ///
        /// Sample response:
        ///
        ///     {
        ///       "success": true,
        ///       "qrString": "{\"type\":\"pottapos_api\",\"ip\":\"192.168.1.10\",\"port\":5001,\"url\":\"http://192.168.1.10:5001\"}",
        ///       "displayUrl": "http://192.168.1.10:5001",
        ///       "data": { "ip": "192.168.1.10", "port": 5001, "url": "...", "serverName": "DESKTOP-ABC", "apiVersion": "1.0.0" }
        ///     }
        /// </remarks>
        /// <response code="200">QR string generated successfully</response>
        [HttpGet("qr-string")]
        [ResponseCache(Duration = 10)]
        [ProducesResponseType(200)]
        public IActionResult GetQRCodeString()
        {
            // Use ExternalUrl (tunnel/proxy) if configured, otherwise fall back to local IP
            string baseUrl;
            string primaryIp;
            int port;

            if (!string.IsNullOrWhiteSpace(_apiOptions.ExternalUrl))
            {
                baseUrl = _apiOptions.ExternalUrl.TrimEnd('/');
                // Parse ip/port from external URL for informational fields
                primaryIp = baseUrl;
                port = baseUrl.StartsWith("https") ? 443 : 80;
            }
            else
            {
                primaryIp = GetPrimaryLocalIPAddress();
                port = HttpContext.Request.Host.Port ?? 5001;
                baseUrl = $"http://{primaryIp}:{port}";
            }
            
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
