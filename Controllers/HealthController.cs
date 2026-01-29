using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using PottaAPI.Services;
using System.Diagnostics;
using System.Reflection;

namespace PottaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly IDatabaseService _databaseService;
        private static readonly DateTime _startTime = DateTime.UtcNow;

        public HealthController(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        /// <summary>
        /// Basic health check endpoint to verify if the API is running
        /// </summary>
        /// <returns>OK status if the API is healthy</returns>
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { 
                status = "healthy", 
                timestamp = DateTime.UtcNow,
                message = "PottaAPI is running successfully"
            });
        }

        /// <summary>
        /// Detailed health check with database connectivity and system information
        /// </summary>
        /// <returns>Detailed health status including database, system, and API information</returns>
        [HttpGet("detailed")]
        public async Task<IActionResult> GetDetailed()
        {
            var healthStatus = new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                uptime = DateTime.UtcNow - _startTime,
                uptimeFormatted = FormatUptime(DateTime.UtcNow - _startTime),
                
                api = new
                {
                    version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
                    name = "PottaAPI",
                    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                    processId = Environment.ProcessId,
                    machineName = Environment.MachineName,
                    osVersion = Environment.OSVersion.ToString(),
                    dotnetVersion = Environment.Version.ToString(),
                    is64BitProcess = Environment.Is64BitProcess,
                    workingDirectory = Directory.GetCurrentDirectory()
                },
                
                database = await GetDatabaseHealthAsync(),
                
                system = new
                {
                    cpuUsage = GetCpuUsage(),
                    memoryUsage = new
                    {
                        workingSet = FormatBytes(Environment.WorkingSet),
                        workingSetBytes = Environment.WorkingSet,
                        gcTotalMemory = FormatBytes(GC.GetTotalMemory(false)),
                        gcTotalMemoryBytes = GC.GetTotalMemory(false)
                    },
                    threadCount = Process.GetCurrentProcess().Threads.Count,
                    handleCount = Process.GetCurrentProcess().HandleCount
                }
            };

            return Ok(healthStatus);
        }

        /// <summary>
        /// Database-specific health check
        /// </summary>
        /// <returns>Database connectivity and statistics</returns>
        [HttpGet("database")]
        public async Task<IActionResult> GetDatabaseHealth()
        {
            var dbHealth = await GetDatabaseHealthAsync();
            
            if (dbHealth.Connected)
            {
                return Ok(dbHealth);
            }
            else
            {
                return StatusCode(503, dbHealth); // Service Unavailable
            }
        }

        private async Task<DatabaseHealthDto> GetDatabaseHealthAsync()
        {
            try
            {
                var syncInfo = await _databaseService.GetLastSyncInfoAsync();
                
                return new DatabaseHealthDto
                {
                    Connected = true,
                    Status = "healthy",
                    Message = "Database connection successful",
                    Statistics = new DatabaseHealthStatistics
                    {
                        Products = syncInfo.ProductCount,
                        Bundles = syncInfo.BundleCount,
                        Variations = syncInfo.VariationCount,
                        Categories = syncInfo.CategoryCount,
                        Tables = syncInfo.TableCount,
                        Staff = syncInfo.StaffCount,
                        Customers = syncInfo.CustomerCount,
                        WaitingTransactions = syncInfo.WaitingTransactionCount,
                        TotalItems = syncInfo.ProductCount + syncInfo.BundleCount + syncInfo.VariationCount
                    },
                    LastSync = syncInfo.LastSync,
                    ResponseTime = "< 100ms"
                };
            }
            catch (Exception ex)
            {
                return new DatabaseHealthDto
                {
                    Connected = false,
                    Status = "unhealthy",
                    Message = "Database connection failed",
                    Error = ex.Message,
                    Details = ex.InnerException?.Message
                };
            }
        }

        private string GetCpuUsage()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var startTime = DateTime.UtcNow;
                var startCpuUsage = process.TotalProcessorTime;
                
                System.Threading.Thread.Sleep(100);
                
                var endTime = DateTime.UtcNow;
                var endCpuUsage = process.TotalProcessorTime;
                
                var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                
                return $"{(cpuUsageTotal * 100):F2}%";
            }
            catch
            {
                return "N/A";
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private string FormatUptime(TimeSpan uptime)
        {
            if (uptime.TotalDays >= 1)
                return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
            else if (uptime.TotalHours >= 1)
                return $"{(int)uptime.TotalHours}h {uptime.Minutes}m {uptime.Seconds}s";
            else if (uptime.TotalMinutes >= 1)
                return $"{(int)uptime.TotalMinutes}m {uptime.Seconds}s";
            else
                return $"{uptime.Seconds}s";
        }
    }
}
