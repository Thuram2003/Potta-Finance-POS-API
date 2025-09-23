using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using PottaAPI.Services;

namespace PottaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MenuController : ControllerBase
    {
        private readonly IDatabaseService _databaseService;

        public MenuController(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        /// <summary>
        /// Get all active menu items (products)
        /// </summary>
        [HttpGet("items")]
        public async Task<ActionResult<ApiResponseDto<List<MenuItemDto>>>> GetMenuItems()
        {
            try
            {
                var menuItems = await _databaseService.GetMenuItemsAsync();
                return Ok(new ApiResponseDto<List<MenuItemDto>>
                {
                    Success = true,
                    Message = $"Retrieved {menuItems.Count} menu items",
                    Data = menuItems
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve menu items",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get all active categories
        /// </summary>
        [HttpGet("categories")]
        public async Task<ActionResult<ApiResponseDto<List<CategoryDto>>>> GetCategories()
        {
            try
            {
                var categories = await _databaseService.GetCategoriesAsync();
                return Ok(new ApiResponseDto<List<CategoryDto>>
                {
                    Success = true,
                    Message = $"Retrieved {categories.Count} categories",
                    Data = categories
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve categories",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get all active bundle items
        /// </summary>
        [HttpGet("bundles")]
        public async Task<ActionResult<ApiResponseDto<List<BundleItemDto>>>> GetBundleItems()
        {
            try
            {
                var bundles = await _databaseService.GetBundleItemsAsync();
                return Ok(new ApiResponseDto<List<BundleItemDto>>
                {
                    Success = true,
                    Message = $"Retrieved {bundles.Count} bundle items",
                    Data = bundles
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve bundle items",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get all active product variations
        /// </summary>
        [HttpGet("variations")]
        public async Task<ActionResult<ApiResponseDto<List<ProductVariationDto>>>> GetProductVariations()
        {
            try
            {
                var variations = await _databaseService.GetProductVariationsAsync();
                return Ok(new ApiResponseDto<List<ProductVariationDto>>
                {
                    Success = true,
                    Message = $"Retrieved {variations.Count} product variations",
                    Data = variations
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve product variations",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get complete menu data for mobile app sync
        /// </summary>
        [HttpGet("sync")]
        public async Task<ActionResult<ApiResponseDto<SyncDataDto>>> GetMenuSyncData()
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
                    Message = "Menu sync data retrieved successfully",
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
    }
}
