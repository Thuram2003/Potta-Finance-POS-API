using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using PottaAPI.Models.Common;
using PottaAPI.Services;
using PottaAPI.Services.Interfaces;

namespace PottaAPI.Controllers
{
    /// <summary>
    /// Controller for item-related operations (products, bundles, recipes, variations)
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ItemsController : ControllerBase
    {
        private readonly IItemService _itemService;

        public ItemsController(IItemService itemService)
        {
            _itemService = itemService;
        }

        #region General Item Operations

        /// <summary>
        /// Invalidate the server-side item cache. Call this after creating, updating, or deleting
        /// any product, bundle, recipe, or category so the next fetch reflects the latest data.
        /// </summary>
        [HttpPost("cache/invalidate")]
        public ActionResult InvalidateCache()
        {
            if (_itemService is ItemService svc)
                svc.InvalidateCache();

            return Ok(new ApiResponseDto<string>
            {
                Success = true,
                Message = "Item cache invalidated",
                Data = "ok"
            });
        }

        /// <summary>
        /// Get all active items (products, bundles, recipes)
        /// </summary>
        [HttpGet]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new string[] { })]
        public async Task<ActionResult<ApiResponseDto<List<ItemDto>>>> GetAllItems()
        {
            try
            {
                var items = await _itemService.GetAllItemsAsync();
                return Ok(new ApiResponseDto<List<ItemDto>>
                {
                    Success = true,
                    Message = $"Retrieved {items.Count} items",
                    Data = items
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve items",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get item by ID (works for products, bundles, recipes)
        /// Returns the full derived type (ProductDto or BundleDto) with all properties
        /// </summary>
        [HttpGet("{id}")]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new string[] { "id" })]
        public async Task<ActionResult<ApiResponseDto<object>>> GetItemById(string id)
        {
            try
            {
                var item = await _itemService.GetItemByIdAsync(id);
                
                if (item == null)
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Item not found",
                        Details = $"No item found with ID: {id}"
                    });
                }

                // Return the actual derived type (ProductDto or BundleDto) to preserve all properties
                return Ok(new ApiResponseDto<object>
                {
                    Success = true,
                    Message = "Item retrieved successfully",
                    Data = item
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve item",
                    Details = ex.Message
                });
            }
        }

        /// <summary>Search active products, bundles, and recipes by name, SKU, description, or category. Returns parent items only (no ingredients or variations).</summary>
        [HttpGet("search")]
        public async Task<ActionResult<ApiResponseDto<ItemSearchResponseDto>>> SearchItems(
            [FromQuery] string? searchTerm = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                // Validate parameters
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 50;
                if (pageSize > 100) pageSize = 100;

                var searchRequest = new ItemSearchDto
                {
                    SearchTerm = searchTerm ?? "",
                    Type = "", // Not used - searches all types
                    Category = "", // Not used - searches all categories
                    IncludeInactive = false, // Always false - only active items
                    IncludeVariations = false, // Always false - only parent products
                    Page = page,
                    PageSize = pageSize
                };

                var results = await _itemService.SearchItemsAsync(searchRequest);

                return Ok(new ApiResponseDto<ItemSearchResponseDto>
                {
                    Success = true,
                    Message = $"Found {results.TotalCount} items (showing page {page} of {results.TotalPages})",
                    Data = results
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to search items",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get item statistics
        /// </summary>
        [HttpGet("statistics")]
        public async Task<ActionResult<ApiResponseDto<ItemStatisticsDto>>> GetItemStatistics()
        {
            try
            {
                var statistics = await _itemService.GetItemStatisticsAsync();
                return Ok(new ApiResponseDto<ItemStatisticsDto>
                {
                    Success = true,
                    Message = "Item statistics retrieved successfully",
                    Data = statistics
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve item statistics",
                    Details = ex.Message
                });
            }
        }

        #endregion

        #region Product Operations

        /// <summary>
        /// Get all active products
        /// </summary>
        [HttpGet("products")]
        public async Task<ActionResult<ApiResponseDto<List<ProductDto>>>> GetAllProducts()
        {
            try
            {
                var products = await _itemService.GetAllProductsAsync();
                return Ok(new ApiResponseDto<List<ProductDto>>
                {
                    Success = true,
                    Message = $"Retrieved {products.Count} products",
                    Data = products
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve products",
                    Details = ex.Message
                });
            }
        }

        /// <summary>Get a product by ID including its variations.</summary>
        [HttpGet("products/{id}")]
        public async Task<ActionResult<ApiResponseDto<ProductDto>>> GetProductById(string id)
        {
            try
            {
                var product = await _itemService.GetProductByIdAsync(id);
                
                if (product == null)
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Product not found",
                        Details = $"No product found with ID: {id}"
                    });
                }

                return Ok(new ApiResponseDto<ProductDto>
                {
                    Success = true,
                    Message = "Product retrieved successfully",
                    Data = product
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve product",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get products by category
        /// </summary>
        [HttpGet("products/category/{categoryId}")]
        public async Task<ActionResult<ApiResponseDto<List<ProductDto>>>> GetProductsByCategory(string categoryId)
        {
            try
            {
                var products = await _itemService.GetProductsByCategoryAsync(categoryId);
                return Ok(new ApiResponseDto<List<ProductDto>>
                {
                    Success = true,
                    Message = $"Retrieved {products.Count} products in category",
                    Data = products
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve products by category",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get low stock products
        /// </summary>
        [HttpGet("products/low-stock")]
        public async Task<ActionResult<ApiResponseDto<List<ProductDto>>>> GetLowStockProducts()
        {
            try
            {
                var products = await _itemService.GetLowStockProductsAsync();
                return Ok(new ApiResponseDto<List<ProductDto>>
                {
                    Success = true,
                    Message = $"Retrieved {products.Count} low stock products",
                    Data = products
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve low stock products",
                    Details = ex.Message
                });
            }
        }

        #endregion

        #region Product Variation Operations

        /// <summary>Get all variations for a product with their attributes and selectable values (e.g. Color → Red/Blue, Size → S/M/L).</summary>
        [HttpGet("products/{productId}/variations")]
        public async Task<ActionResult<ApiResponseDto<ProductVariationsWithAttributesDto>>> GetProductVariations(string productId)
        {
            try
            {
                var result = await _itemService.GetProductVariationsWithAttributesAsync(productId);
                
                return Ok(new ApiResponseDto<ProductVariationsWithAttributesDto>
                {
                    Success = true,
                    Message = $"Retrieved {result.Variations.Count} variations with {result.Attributes.Count} attributes",
                    Data = result
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
        /// Get variation by ID
        /// </summary>
        [HttpGet("variations/{id}")]
        public async Task<ActionResult<ApiResponseDto<ProductVariationDto>>> GetVariationById(string id)
        {
            try
            {
                var variation = await _itemService.GetVariationByIdAsync(id);
                
                if (variation == null)
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Variation not found",
                        Details = $"No variation found with ID: {id}"
                    });
                }

                return Ok(new ApiResponseDto<ProductVariationDto>
                {
                    Success = true,
                    Message = "Variation retrieved successfully",
                    Data = variation
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve variation",
                    Details = ex.Message
                });
            }
        }

        #endregion

        #region Service Operations

        /// <summary>
        /// Get all service items
        /// </summary>
        [HttpGet("services")]
        public async Task<ActionResult<ApiResponseDto<List<ProductDto>>>> GetAllServices()
        {
            try
            {
                var services = await _itemService.GetAllServicesAsync();
                return Ok(new ApiResponseDto<List<ProductDto>>
                {
                    Success = true,
                    Message = $"Retrieved {services.Count} services",
                    Data = services
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve services",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get service by ID
        /// </summary>
        [HttpGet("services/{id}")]
        public async Task<ActionResult<ApiResponseDto<ProductDto>>> GetServiceById(string id)
        {
            try
            {
                var service = await _itemService.GetProductByIdAsync(id);
                
                if (service == null || service.Type != "Service")
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Service not found",
                        Details = $"No service found with ID: {id}"
                    });
                }

                return Ok(new ApiResponseDto<ProductDto>
                {
                    Success = true,
                    Message = "Service retrieved successfully",
                    Data = service
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve service",
                    Details = ex.Message
                });
            }
        }

        #endregion

        #region Bundle Operations

        /// <summary>
        /// Get all active bundles
        /// </summary>
        [HttpGet("bundles")]
        public async Task<ActionResult<ApiResponseDto<List<BundleDto>>>> GetAllBundles()
        {
            try
            {
                var bundles = await _itemService.GetAllBundlesAsync();
                return Ok(new ApiResponseDto<List<BundleDto>>
                {
                    Success = true,
                    Message = $"Retrieved {bundles.Count} bundles",
                    Data = bundles
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve bundles",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get bundle by ID with components
        /// </summary>
        [HttpGet("bundles/{id}")]
        public async Task<ActionResult<ApiResponseDto<BundleDto>>> GetBundleById(string id)
        {
            try
            {
                var bundle = await _itemService.GetBundleByIdAsync(id);
                
                if (bundle == null)
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Bundle not found",
                        Details = $"No bundle found with ID: {id}"
                    });
                }

                return Ok(new ApiResponseDto<BundleDto>
                {
                    Success = true,
                    Message = "Bundle retrieved successfully",
                    Data = bundle
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve bundle",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get all recipes
        /// </summary>
        [HttpGet("recipes")]
        public async Task<ActionResult<ApiResponseDto<List<BundleDto>>>> GetAllRecipes()
        {
            try
            {
                var recipes = await _itemService.GetAllRecipesAsync();
                return Ok(new ApiResponseDto<List<BundleDto>>
                {
                    Success = true,
                    Message = $"Retrieved {recipes.Count} recipes",
                    Data = recipes
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve recipes",
                    Details = ex.Message
                });
            }
        }

        #endregion

        #region Category Operations

        /// <summary>
        /// Get all categories with item counts
        /// </summary>
        [HttpGet("categories")]
        public async Task<ActionResult<ApiResponseDto<List<CategoryDto>>>> GetAllCategories()
        {
            try
            {
                var categories = await _itemService.GetAllCategoriesAsync();
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
        /// Get category by ID
        /// </summary>
        [HttpGet("categories/{id}")]
        public async Task<ActionResult<ApiResponseDto<CategoryDto>>> GetCategoryById(string id)
        {
            try
            {
                var category = await _itemService.GetCategoryByIdAsync(id);
                
                if (category == null)
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Category not found",
                        Details = $"No category found with ID: {id}"
                    });
                }

                return Ok(new ApiResponseDto<CategoryDto>
                {
                    Success = true,
                    Message = "Category retrieved successfully",
                    Data = category
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve category",
                    Details = ex.Message
                });
            }
        }

        #endregion

        #region Modifier Operations

        /// <summary>
        /// Get all active modifiers
        /// </summary>
        [HttpGet("modifiers")]
        public async Task<ActionResult<ApiResponseDto<List<ModifierDto>>>> GetAllModifiers()
        {
            try
            {
                var modifiers = await _itemService.GetAllModifiersAsync();
                return Ok(new ApiResponseDto<List<ModifierDto>>
                {
                    Success = true,
                    Message = $"Retrieved {modifiers.Count} modifiers",
                    Data = modifiers
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve modifiers",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Get modifier by ID
        /// </summary>
        [HttpGet("modifiers/{id}")]
        public async Task<ActionResult<ApiResponseDto<ModifierDto>>> GetModifierById(string id)
        {
            try
            {
                var modifier = await _itemService.GetModifierByIdAsync(id);
                
                if (modifier == null)
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Error = "Modifier not found",
                        Details = $"No modifier found with ID: {id}"
                    });
                }

                return Ok(new ApiResponseDto<ModifierDto>
                {
                    Success = true,
                    Message = "Modifier retrieved successfully",
                    Data = modifier
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve modifier",
                    Details = ex.Message
                });
            }
        }

        #endregion

        #region Multi-Unit Pricing Operations

        /// <summary>Get available package/unit pricing options for a product (e.g. sell by bottle or by crate).</summary>
        [HttpGet("products/{productId}/unit-pricing")]
        public async Task<ActionResult<ApiResponseDto<List<ProductUnitPricingDto>>>> GetProductUnitPricing(string productId)
        {
            try
            {
                var unitPricing = await _itemService.GetProductUnitPricingAsync(productId);
                return Ok(new ApiResponseDto<List<ProductUnitPricingDto>>
                {
                    Success = true,
                    Message = $"Retrieved {unitPricing.Count} unit pricing options",
                    Data = unitPricing
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve unit pricing",
                    Details = ex.Message
                });
            }
        }

        /// <summary>Get available package/unit pricing options for a specific variation.</summary>
        [HttpGet("variations/{variationId}/unit-pricing")]
        public async Task<ActionResult<ApiResponseDto<List<ProductUnitPricingDto>>>> GetVariationUnitPricing(string variationId)
        {
            try
            {
                var unitPricing = await _itemService.GetVariationUnitPricingAsync(variationId);
                return Ok(new ApiResponseDto<List<ProductUnitPricingDto>>
                {
                    Success = true,
                    Message = $"Retrieved {unitPricing.Count} unit pricing options",
                    Data = unitPricing
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponseDto
                {
                    Error = "Failed to retrieve unit pricing",
                    Details = ex.Message
                });
            }
        }

        #endregion
    }
}