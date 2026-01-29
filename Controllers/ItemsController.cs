using Microsoft.AspNetCore.Mvc;
using PottaAPI.Models;
using PottaAPI.Services;

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
        /// Get all active items (products, bundles, recipes)
        /// </summary>
        /// <returns>List of active items</returns>
        [HttpGet]
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
        /// </summary>
        /// <param name="id">Item ID</param>
        /// <returns>Item details if found</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponseDto<ItemDto>>> GetItemById(string id)
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

                return Ok(new ApiResponseDto<ItemDto>
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

        /// <summary>
        /// Search items (matches Desktop Dashboard search behavior exactly)
        /// 
        /// Search Behavior:
        /// - Searches: name, SKU, description, categories
        /// - Returns: Active items only (status = 1)
        /// - Excludes: Ingredients (isIngredient = 1)
        /// - Excludes: Product variations (returns parent products only)
        /// - Includes: Products, Bundles, and Recipes
        /// - Sorting: Alphabetically by name
        /// 
        /// If no searchTerm provided, returns all active items (excluding ingredients)
        /// </summary>
        /// <param name="searchTerm">Search term to match against name, SKU, description, categories (optional)</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 50, max: 100)</param>
        /// <returns>Paginated search results</returns>
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
        /// <returns>Item statistics and insights</returns>
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
        /// <returns>List of active products</returns>
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

        /// <summary>
        /// Get product by ID with variations
        /// </summary>
        /// <param name="id">Product ID</param>
        /// <returns>Product details with variations if found</returns>
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
        /// <param name="categoryId">Category ID</param>
        /// <returns>List of products in category</returns>
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
        /// <returns>List of products with low stock</returns>
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

        /// <summary>
        /// Get variations for a product
        /// </summary>
        /// <param name="productId">Parent product ID</param>
        /// <returns>List of product variations</returns>
        [HttpGet("products/{productId}/variations")]
        public async Task<ActionResult<ApiResponseDto<List<ProductVariationDto>>>> GetProductVariations(string productId)
        {
            try
            {
                var variations = await _itemService.GetProductVariationsAsync(productId);
                return Ok(new ApiResponseDto<List<ProductVariationDto>>
                {
                    Success = true,
                    Message = $"Retrieved {variations.Count} variations for product",
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
        /// Get variation by ID
        /// </summary>
        /// <param name="id">Variation ID</param>
        /// <returns>Variation details if found</returns>
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

        #region Bundle Operations

        /// <summary>
        /// Get all active bundles
        /// </summary>
        /// <returns>List of active bundles</returns>
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
        /// <param name="id">Bundle ID</param>
        /// <returns>Bundle details with components if found</returns>
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
        /// <returns>List of recipes</returns>
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
        /// <returns>List of categories</returns>
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
        /// <param name="id">Category ID</param>
        /// <returns>Category details if found</returns>
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
    }
}