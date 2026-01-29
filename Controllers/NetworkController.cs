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
        /// <summary>
        /// Get network information for API discovery
        /// </summary>
        [HttpGet("info")]
        public IActionResult GetNetworkInfo()
        {
            var networkInfo = new NetworkInfoDto
            {
                LocalIpAddresses = GetLocalIPAddresses(),
                HostName = Dns.GetHostName(),
                Port = HttpContext.Request.Host.Port ?? 5001,
                ApiBaseUrls = GetApiBaseUrls(),
                MachineName = Environment.MachineName,
                Timestamp = DateTime.UtcNow
            };

            return Ok(new ApiResponseDto<NetworkInfoDto>
            {
                Success = true,
                Message = "Network information retrieved successfully",
                Data = networkInfo
            });
        }

        /// <summary>
        /// Get QR code data for mobile app connection
        /// </summary>
        [HttpGet("qr-data")]
        public IActionResult GetQRCodeData()
        {
            var primaryIp = GetPrimaryLocalIPAddress();
            var port = HttpContext.Request.Host.Port ?? 5001;
            
            var qrData = new QRCodeDataDto
            {
                ApiUrl = $"http://{primaryIp}:{port}",
                HostName = Dns.GetHostName(),
                MachineName = Environment.MachineName,
                Port = port,
                Version = "1.0.0",
                Timestamp = DateTime.UtcNow
            };

            return Ok(new ApiResponseDto<QRCodeDataDto>
            {
                Success = true,
                Message = "QR code data generated successfully",
                Data = qrData
            });
        }

        /// <summary>
        /// Get QR code data as JSON string (for QR code generation)
        /// </summary>
        [HttpGet("qr-string")]
        public IActionResult GetQRCodeString()
        {
            var primaryIp = GetPrimaryLocalIPAddress();
            var port = HttpContext.Request.Host.Port ?? 5001;
            
            var qrData = new
            {
                type = "pottapos_api",
                url = $"http://{primaryIp}:{port}",
                host = Dns.GetHostName(),
                machine = Environment.MachineName,
                port = port,
                version = "1.0.0",
                timestamp = DateTime.UtcNow.ToString("o")
            };

            var jsonString = JsonSerializer.Serialize(qrData);

            return Ok(new
            {
                success = true,
                message = "QR code string generated",
                qrString = jsonString,
                displayUrl = $"http://{primaryIp}:{port}"
            });
        }

        /// <summary>
        /// Ping endpoint for network discovery
        /// </summary>
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok(new
            {
                success = true,
                message = "pong",
                service = "PottaAPI",
                version = "1.0.0",
                timestamp = DateTime.UtcNow,
                ip = GetPrimaryLocalIPAddress(),
                port = HttpContext.Request.Host.Port ?? 5001
            });
        }

        /// <summary>
        /// Get network interfaces information
        /// </summary>
        [HttpGet("interfaces")]
        public IActionResult GetNetworkInterfaces()
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                .Select(ni => new NetworkInterfaceDto
                {
                    Name = ni.Name,
                    Description = ni.Description,
                    Type = ni.NetworkInterfaceType.ToString(),
                    Status = ni.OperationalStatus.ToString(),
                    Speed = ni.Speed,
                    MacAddress = ni.GetPhysicalAddress().ToString(),
                    IpAddresses = ni.GetIPProperties().UnicastAddresses
                        .Where(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        .Select(ip => ip.Address.ToString())
                        .ToList()
                })
                .ToList();

            return Ok(new ApiResponseDto<List<NetworkInterfaceDto>>
            {
                Success = true,
                Message = $"Retrieved {interfaces.Count} network interfaces",
                Data = interfaces
            });
        }

        /// <summary>
        /// Test connection from mobile device
        /// </summary>
        [HttpPost("test-connection")]
        public IActionResult TestConnection([FromBody] TestConnectionDto request)
        {
            return Ok(new
            {
                success = true,
                message = "Connection successful",
                clientIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
                serverIp = GetPrimaryLocalIPAddress(),
                receivedFrom = request.DeviceName,
                timestamp = DateTime.UtcNow
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

        private List<string> GetApiBaseUrls()
        {
            var urls = new List<string>();
            var port = HttpContext.Request.Host.Port ?? 5001;

            foreach (var ip in GetLocalIPAddresses())
            {
                urls.Add($"http://{ip}:{port}");
            }

            return urls;
        }
    }
}
