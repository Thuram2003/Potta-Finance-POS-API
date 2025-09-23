using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using PottaAPI.Services;

namespace PottaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SyncController : ControllerBase
    {
        private readonly IDatabaseService _databaseService;

        public SyncController(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        /// <summary>
        /// Get sync information (counts and timestamps)
        /// </summary>
        [HttpGet("info")]
        public async Task<ActionResult<ApiResponseDto<SyncInfoDto>>> GetSyncInfo()
        {
            try
            {
                var syncInfo = await _databaseService.GetLastSyncInfoAsync();
                return Ok(new ApiResponseDto<SyncInfoDto>
                {
                    Success = true,
                    Message = "Sync information retrieved successfully",
                    Data = syncInfo
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve sync information",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get complete data for mobile app synchronization
        /// </summary>
        [HttpGet("full")]
        public async Task<ActionResult<ApiResponseDto<SyncDataDto>>> GetFullSyncData()
        {
            try
            {
                var syncData = new SyncDataDto
                {
                    MenuItems = await _databaseService.GetMenuItemsAsync(),
                    BundleItems = await _databaseService.GetBundleItemsAsync(),
                    ProductVariations = await _databaseService.GetProductVariationsAsync(),
                    Categories = await _databaseService.GetCategoriesAsync(),
                    Tables = await _databaseService.GetTablesAsync(),
                    Staff = await _databaseService.GetActiveStaffAsync(),
                    Customers = await _databaseService.GetCustomersAsync(),
                    SyncInfo = await _databaseService.GetLastSyncInfoAsync()
                };

                return Ok(new ApiResponseDto<SyncDataDto>
                {
                    Success = true,
                    Message = "Full sync data retrieved successfully",
                    Data = syncData
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve sync data",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Health check endpoint for mobile apps
        /// </summary>
        [HttpGet("health")]
        public async Task<ActionResult<ApiResponseDto<object>>> HealthCheck()
        {
            try
            {
                await _databaseService.TestConnectionAsync();
                var syncInfo = await _databaseService.GetLastSyncInfoAsync();
                
                return Ok(new ApiResponseDto<object>
                {
                    Success = true,
                    Message = "API is healthy and database is accessible",
                    Data = new 
                    {
                        Status = "Healthy",
                        DatabaseConnected = true,
                        TotalProducts = syncInfo.ProductCount,
                        TotalStaff = syncInfo.StaffCount,
                        TotalTables = syncInfo.TableCount,
                        PendingOrders = syncInfo.WaitingTransactionCount,
                        LastSync = syncInfo.LastSync
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Health check failed",
                    Details = ex.Message
                });
            }
        }
    }
}
